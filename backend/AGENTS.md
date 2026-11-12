# backend/AGENTS.md

Backend de l'explorateur de quartiers : API, données, matching, ETL. Lis aussi `../AGENTS.md`, `../docs/data.md`, `../docs/matching.md`, `../docs/clef.md`, `../docs/isochrones.md`, `../docs/infrastructure.md`, `../docs/verification.md`.

Ce fichier décrit **tout ce que le backend doit fournir**. Le frontend (`../frontend/AGENTS.md`) en dépend : le contrat d'API est la frontière entre les deux.

## Responsabilités

- Entités géographiques (hiérarchie administrative), métriques, sources de données.
- Normalisation, scores, classements, matching, comparaison, tendances.
- Isochrones (via un fournisseur externe, derrière une abstraction).
- Analyse de préférences en langage naturel (optionnelle, derrière une abstraction).
- Ingestion et calculs lourds **hors du chemin utilisateur** (ETL).

Le backend est la **seule** source de logique métier. Le frontend n'implémente jamais de score ni de normalisation.

## Stack

C#, ASP.NET Core (API REST), EF Core là où il est pertinent (requêtes spatiales complexes : SQL/Npgsql + NetTopologySuite si plus simple), PostgreSQL + PostGIS, xUnit, FsCheck (tests de propriétés), Testcontainers (tests d'intégration avec PostGIS réel).

## Organisation : un dossier par feature

```
backend/
├── openapi/openapi.v1.json         # contrat versionné, source de vérité
├── src/
│   ├── Api/                        # démarrage, DI, middleware, ProblemDetails
│   ├── Features/
│   │   ├── Geography/              # villes, districts, quartiers, GeoJSON
│   │   ├── Metrics/                # définitions, valeurs, normalisation
│   │   ├── Matching/               # scoring pur + endpoint /match
│   │   ├── Comparison/
│   │   ├── Isochrones/             # IIsochroneProvider + cache + précalcul
│   │   ├── PreferenceParsing/      # questions, interprétation, IDecisionModel + implémentations (docs/clef.md)
│   │   ├── Accounts/               # Identity, profil, favoris, j'aime (docs/accounts.md)
│   │   ├── Reviews/                # avis et modération (ReviewModeration, tâche de fond)
│   │   ├── Places/                 # pins community, file de validation, places/map, places/zone, photo
│   │   └── Trending/               # événements anonymes, popularité, tendances hors ligne
│   ├── Etl/                        # application console d'ingestion (voir docs/data.md)
│   └── Infrastructure/             # DbContext, migrations, HTTP clients
└── tests/
```

Règles : noms de fichiers explicites (`NeighborhoodScoreCalculator.cs`, `MatchRequestValidator.cs`), un type principal par fichier, pas de `Utils`/`Helpers`/`Common`. Le **cœur de scoring et de normalisation est du code pur** (aucune dépendance à la base, au réseau, à l'horloge, à l'aléatoire) pour être testable par propriétés.

## Modèle de données

La hiérarchie administrative turque est gérée **explicitement** : `province (il)` > `district (ilçe)` > `neighborhood (mahalle)`.

```
AdminArea
---------
id, parent_id, level (province | district | neighborhood), country_code,
name, official_name, official_code (nullable), geometry, display_geometry (nullable), centroid, valid_from, valid_to

`display_geometry` : contour destiné à la carte (province = union de ses ilçe, sans les eaux de la limite officielle ; mahalle = géométrie simplifiée par l'ETL) ; l'API expose `COALESCE(display_geometry, geometry)`, la géométrie officielle reste la référence pour les contrôles.

Les tables dérivées (`metric_value`, `worldpop_neighborhood_sum`, `worldpop_district_coefficient`, `staging_ibb_buildings_2017`) référencent `admin_area` avec `ON DELETE CASCADE` : réimporter les limites administratives efface les valeurs dérivées devenues obsolètes, sans bloquer sur une clé étrangère.

La vue matérialisée des dernières valeurs n'est pas retenue : à 964 mahalles, la dernière valeur par couple (métrique, mahalle) se lit avec l'index `(metric_id, admin_area_id, observed_at DESC)` et évite un `REFRESH` à maintenir. La requête `metric-values` part des métriques et utilise un `CROSS JOIN LATERAL … ORDER BY observed_at DESC LIMIT 1` (mesuré à p95 233 ms pour deux métriques, voir `../docs/infrastructure.md`). La normalisation percentile est calculée en C# (`PercentileRank`), pas en SQL, et testée contre `PERCENT_RANK()`.

MetricDefinition
----------------
id, key (unique), category, unit,
native_level (province | district | neighborhood : niveau où la source publie la valeur),
polarity (higher_is_better | lower_is_better | neutral),
normalization_method, normalization_version, display_precision, source_id

MetricTranslation      -- textes par langue (fr, en ; tr = bêta)
-----------------
metric_id, locale, name, description,
subject (complément de phrase utilisé dans les questions de CLEF, voir ../docs/clef.md)
PK (metric_id, locale)

MetricValue            -- source de vérité (format EAV)
-----------
admin_area_id, metric_id, observed_at, raw_value, normalized_value,
quality_flag (ok | estimated | stale | low_coverage | inherited), computed_at
PK (admin_area_id, metric_id, observed_at)

DataSource
----------
id, name, organization, url, license, attribution_text, methodology,
retrieved_at, source_version

UserPreferenceProfile   -- phase 3, séparé des métriques
InteractionEvent        -- anonyme, pour trending (voir « Vie privée »)
```

Règles :
- `raw_value` et `normalized_value` sont **deux colonnes distinctes** et ne se mélangent jamais. `raw_value` est affiché (avec unité) ; `normalized_value` (0..1, percentile par défaut) sert au score. La méthode est versionnée (`normalization_version`).
- **Le format EAV est la source de vérité, pas le format de lecture.** Pour servir la carte, créer une vue matérialisée `neighborhood_metric_latest` (dernière observation par quartier et métrique) et un payload compact `metric-values` par ville, rafraîchis par l'ETL.
- Contraintes en base (CHECK, FK, UNIQUE) pour tout ce qui peut l'être : `normalized_value BETWEEN 0 AND 1`, géométries valides (`ST_IsValid`), `level` cohérent avec le parent.
- `inherited` : la source ne publie qu'au niveau `native_level` (ex. district) ; les mahalles en dessous reçoivent la valeur du parent. Le frontend l'affiche comme « valeur du district », jamais comme une mesure du mahalle. Sources et métriques concernées : `../docs/data-istanbul.md`.
- Toute migration est revue par l'humain (voir `../AGENTS.md`).
- Les préférences utilisateur et les interactions sociales ne se mélangent pas aux métriques.

## Contrat d'API (v1)

REST versionné, JSON, **OpenAPI à jour dans `openapi/openapi.v1.json`**. Le frontend génère ses types depuis ce fichier.

Règles transverses :
- Erreurs : `application/problem+json` (RFC 9457) avec un `code` stable.
- Pagination pour toute liste non bornée (`limit`, `cursor`).
- Lecture : `ETag` + `Cache-Control` ; les endpoints de métriques et de géométrie sont cacheables.
- Les erreurs « fonctionnalité indisponible » (IA off, quota isochrone épuisé, base injoignable) renvoient `503` avec `code` explicite (`ai_disabled`, `ai_unavailable`, `ai_busy`, `isochrone_quota_exceeded`, `database_unavailable`), jamais `500`. `ai_timeout` → `504`. Texte hors sujet → `422 not_a_neighborhood_request`.
- Aucun endpoint ne dépend de l'IA sauf `/preferences/parse` et `/chat/turn`.

| Endpoint | Rôle |
|---|---|
| `GET /api/v1/cities` | Villes disponibles |
| `GET /api/v1/cities/{cityId}/neighborhoods` | GeoJSON `application/geo+json` des mahalles avec `COALESCE(display_geometry, geometry)`, `ETag` + `Cache-Control: max-age=86400` |
| `GET /api/v1/metrics` | Définitions de métriques + source + date |
| `GET /api/v1/cities/{cityId}/metric-values?metrics=a,b` | `{ metricKey: { neighborhoodId: { raw, normalized, qualityFlag } } }` compact |
| `GET /api/v1/neighborhoods/{id}` | Détail (hiérarchie, géométrie complète, centroïde) |
| `GET /api/v1/neighborhoods/{id}/metrics` | Toutes les métriques du quartier, avec date et source |
| `POST /api/v1/compare` | 2 à 4 `neighborhoodIds` → tableau de métriques |
| `POST /api/v1/match` | Préférences → classement **expliqué** (voir ci-dessous) |
| `POST /api/v1/chat/turn` | **Optionnel (phase IA)** : `{sessionId,text,locale,mapContext}` → `{message, actions[]}`. `message` est composé par **gabarits** à partir de résultats calculés ; `actions[]` = commandes typées de carte (`setCriteria`, `showMatches`, `fitBounds`, `highlight`, `openCard`, `ask`, `setLayer`). Réutilise le service de `/preferences/parse` (intention, besoins, portes) ; mêmes erreurs `ai_*` |
| `POST /api/v1/preferences/parse` | **Optionnel** : texte libre (fr/en) `{cityId,text,locale}` → préférences validées, quartier de référence, ajustements, avertissements |
| `GET /api/v1/neighborhoods/{id}/isochrone?minutes=&mode=` | Isochrone précalculée + comptage de POI |
| `POST /api/v1/isochrones` | Isochrone pour un point arbitraire (cache + quota) |
| `GET /api/v1/trending?period=7d` | Tendances (croissance, jamais un classement de popularité ; `trends-v1`) |
| `POST /api/v1/events` | Événement anonyme (avec consentement) |
| `GET /api/v1/health` | Santé (base, version des données, `ai: disabled \| warming \| ready \| unavailable \| unauthorized`) |

`display_geometry` des mahalles est précalculé par l'ETL (`ST_SimplifyPreserveTopology(geometry, 0.0005)`, ≈55 m) : l'API sert cette colonne, jamais une simplification à la volée. Chaque valeur de métrique expose `qualityFlag` (`ok | estimated | stale | low_coverage | inherited`) ; `inherited` signifie que la valeur vient du district et doit être étiquetée comme telle côté frontend.

`POST /api/v1/match` (forme, détail dans `../docs/matching.md`) :

```jsonc
// requête
{
  "cityId": 34,
  "preferences": [
    { "metricKey": "transport_accessibility", "weight": 0.9, "target": "high" },
    { "metricKey": "housing_affordability",   "weight": 1.0, "target": "high" },
    { "metricKey": "noise",                   "weight": 0.6, "target": "low"  }
  ],
  "limit": 20
}
// réponse
{
  "results": [{
    "neighborhoodId": 123,
    "score": 0.94,
    "coverage": 0.92,
    "contributions": [
      { "metricKey": "transport_accessibility", "contribution": 0.31, "normalized": 0.97 }
    ],
    "strengths": ["transport_accessibility"],
    "weaknesses": ["housing_affordability"]
  }],
  "methodVersion": "match-v1"
}
```

Le moteur renvoie des **contributions par critère** : l'explication est calculée, jamais inventée par un modèle.

## Comptes, avis, pins (phases 4a à 4e)

Spécification complète dans `../docs/accounts.md` ; ce fichier n'en garde que les règles qui s'imposent au code.
- ASP.NET Core Identity, cookie de session ; `FakeEmailSender` ; utilisateurs de démonstration (`is_demo`) refusés en production.
- `Review.is_visible` est la **seule** colonne que le public lit. `ReviewModeration` (et `PlaceModeration`) : **append-only**, une ligne par décision, écrite dans la même transaction que `is_visible`.
- Pins : un pin `pending` n'apparaît jamais dans `places/map`, `places/zone`, les tendances ni la popularité d'autrui ; l'envoi est **idempotent** (`clientId`) ; le quota de 5 pins est contrôlé à l'envoi (`409`), sans jamais supprimer le pin local de l'auteur ; une modification d'un pin publié est visible aussitôt, CLEF la re-lit en fond et l'administrateur peut la masquer.
- `places/map` : zones = tuiles Web Mercator au zoom `min(zoom + 2, 18)` (clé `z/x/y`), `leader` = lieu de popularité maximale (égalité : `community` publié avant OSM, puis identifiant) ; `places/zone/{zoneKey}` renvoie tous les lieux de la zone, triés de la même façon, paginés.
- `Place.popularity` : `3 × j'aime + 4 × avis visibles` (l'enregistrement n'y entre pas), personnes distinctes, comptes vérifiés, **hors ligne** (jamais dans une requête utilisateur) ; n'entre jamais dans le matching.
- Dépendances de l'ETL : accord donné le 18 octobre 2026 pour la liste de `../docs/dependencies.md` ; lire la licence de chaque paquet avant de l'ajouter et la consigner.
- Photo d'un pin : une seule, réencodée côté serveur, EXIF supprimé, contenu reconnu ; **aucune bibliothèque d'image ajoutée sans l'accord de l'humain** (licence à valider).

## Matching

Détails dans `../docs/matching.md`. Points impératifs pour l'implémentation :
- Fonction pure `ScoreNeighborhoods(preferences, metricValues) -> results`.
- Poids explicites ; les poids sont renormalisés (somme = 1) avant le calcul.
- Valeurs manquantes : le critère est **exclu** pour ce quartier et les poids restants sont renormalisés ; `coverage` est renvoyé ; sous un seuil de couverture le quartier est signalé, pas masqué silencieusement.
- Tri total et déterministe (égalité de score → `neighborhoodId` croissant).
- Le moteur ne connaît ni l'IA, ni la base, ni HTTP.

## IA optionnelle (CLEF-Flash)

CLEF-Flash est un **modèle de décision** : il reçoit un état (ici le texte brut de l'utilisateur) et des questions typées en langage naturel, et renvoie des probabilités ; il **ne génère pas de texte**. Détail complet (format, questions, interprétation, exécution) dans `../docs/clef.md`.

```csharp
public interface IDecisionModel
{
    Task<DecisionAnswers> DecideAsync(DecisionRequest request, CancellationToken cancellationToken);
}
```

L'interface est générique ; la logique « préférences » vit dans trois composants **purs** : `PreferenceQuestionCatalog`, `PreferenceAnswerInterpreter` (`preferences-v2`) et `NeighborhoodNameMatcher`. Implémentations : `FakeDecisionModel` (par défaut en dev et tests, règles déterministes, mêmes formes de réponse), `ClefLlamaServerDecisionModel` (HTTP vers `llama-server`, `/v1/systemone`).

Règles :
- **La sortie d'un modèle est une entrée non fiable.** La valider (zod côté front, schémas stricts ici) : clés connues dans `MetricDefinition`, bornes, pas de doublon. Invalide → `ProblemDetails`, jamais de correction silencieuse.
- Le texte de l'utilisateur est non fiable (injection de prompt) : il n'entre que dans l'état du modèle, qui ne renvoie que des probabilités sur **nos** questions ; le modèle ne déclenche aucune action et n'écrit aucune phrase. Les messages du chat viennent de **gabarits**.
- Budget interactif `AI_TIMEOUT_SECONDS = 10` ; file bornée (une requête à la fois, deux en attente, sinon `503 ai_busy`) ; limitation de débit ; texte jamais journalisé en production ; requête d'échauffement au démarrage ; `GET /health` indique `ai: disabled | warming | ready | unavailable | unauthorized`.
- `AI_ENABLED=false` : `/preferences/parse` et `/chat/turn` renvoient `503 ai_disabled` ; tout le reste fonctionne, **modération comprise** (avis publiés sans contrôle, motif `ai_unavailable`, voir `../docs/accounts.md` §4).
- **Modération (phase 4b)** : tâche de fond `BackgroundService`, un élément à la fois, `MODERATION_TIMEOUT_SECONDS = 180`, attente de 30 s si la file est vide et de 60 s si CLEF est absent ; le chat passe avant. Avis : publier d'abord, re-modérer ensuite. **Pins : CLEF ne publie ni ne refuse jamais** ; il classe la file de l'administrateur.
- CLEF-Flash local est mesuré, pas supposé rapide (médiane 42 s, requête de modération environ 78 s sur la machine de test) : voir `../docs/clef.md` « Mesures ». Profil Docker `ai` uniquement.

## Isochrones

**Reporté le 11 novembre 2026** : rapport coût/pertinence défavorable, les densités déjà calculées suffisent (décision dans `../docs/isochrones.md`). Les endpoints et le dossier `Isochrones/` ci-dessus restent la cible **si la fonctionnalité est rouverte** ; ne rien implémenter sans décision explicite.

Pas de serveur de routage auto-hébergé : on s'appuierait sur un **service hébergé** derrière `IIsochroneProvider`. Stratégie complète (précalcul, cache, quotas, repli) dans `../docs/isochrones.md`.

## Données et ETL

Détails dans `../docs/data.md`. Impératifs :
- Chaque source a un manifeste (source, version, date de téléchargement, licence, étapes, sortie) ; dossiers `raw/` → `processed/` → `normalized/`.
- ETL reproductible : même entrée → même sortie ; aucune modification manuelle de dataset sans transformation versionnée.
- Aucune métrique sans `DataSource` renseignée (licence et attribution comprises).
- Les calculs lourds (densités, accessibilité, entropie d'usage du sol, isochrones précalculées) vivent dans l'ETL, jamais dans une requête utilisateur.
- TÜİK ADNKS : la table `tuik_adnks_totals` (39 ilçe + `TR100`) est une **référence de contrôle** et, pour `population_density` uniquement, la référence de recalage des sommes WorldPop par ilçe (décision du 31 octobre 2026, implémentée le 5 novembre 2026). Elle n'est jamais exposée comme métrique ; `TUIK_API_KEY` est lue à l'exécution et n'est jamais journalisée.
- WorldPop : le raster 2025 (CC BY 4.0) est importé dans `worldpop_raster`, la somme zonale est conservée dans `worldpop_neighborhood_sum`, le coefficient par ilçe dans `worldpop_district_coefficient`, et la métrique `population_density` est écrite dans `metric_value` avec `quality_flag = estimated`. La provenance (URL, version, année, licence, SHA-256) est dans le manifeste et le rapport de contrôle.

## Vie privée et sécurité

- Comptes facultatifs (voir `../docs/accounts.md`) : la lecture ne demande jamais de connexion. Les lieux enregistrés (bibliothèque privée) sont gardés sur l'appareil jusqu'à la connexion, puis fusionnés sans doublon.
- Trending : événements **anonymes** (type, identifiant de quartier, horodatage arrondi, identifiant de session aléatoire non persistant ni lié à une personne). Ne pas stocker d'adresse IP. Activer la collecte seulement avec consentement ; la loi turque KVKK s'applique aux utilisateurs en Turquie, le RGPD aux utilisateurs européens.
- Secrets uniquement par variables d'environnement ; `.env.example` à jour.
- CORS restreint ; limitation de débit sur `/preferences/parse`, `/isochrones` et `/events`.

## Configuration

```
DATABASE_CONNECTION_STRING
TUIK_API_KEY          # clé d'API TÜİK (ETL uniquement ; vide = arrêt de la source tuik-adnks)
AI_ENABLED            # false par défaut
AI_PROVIDER           # fake | clef-server (serveur CLEF distant, voir AI_ENDPOINT)
AI_USERNAME / AI_PASSWORD   # authentification HTTP Basic du serveur CLEF (facultatives ; secrets : jamais dans le dépôt ni les journaux)
AI_ENDPOINT           # URL du serveur CLEF ; si AI_ENABLED=true et qu'elle est vide, l'API refuse de démarrer
AI_ENDPOINT
AI_MODEL              # clef-flash
AI_TIMEOUT_SECONDS    # 10 par défaut
MODERATION_ON_AI_UNAVAILABLE   # publish (défaut) | hold
MODERATION_TIMEOUT_SECONDS     # 180
ISOCHRONE_PROVIDER    # fake | geoapify | openrouteservice | valhalla
ISOCHRONE_API_KEY
ISOCHRONE_DAILY_BUDGET
```

Environnements : `Development` (doit rester extrêmement simple), `Test`, `Production`.

## Observabilité

Logs structurés, `GET /api/v1/health`, métriques de latence par endpoint. Journaliser la consommation du quota de l'API d'isochrones.

## Tests

Voir `../docs/verification.md` pour la stratégie complète.
- **Unitaires + propriétés (FsCheck)** sur le cœur pur : normalisation, scoring, validation de `UserPreferences`.
- **Intégration (Testcontainers + PostGIS)** : requêtes spatiales, contraintes, vues matérialisées.
- **Contrat** : l'API respecte `openapi.v1.json` (validation de schéma ; tests de propriétés sur l'API possibles avec Schemathesis).
- **IA** : `PreferenceQuestionCatalog`, `PreferenceAnswerInterpreter` et `NeighborhoodNameMatcher` testés par propriétés ; la suite par défaut utilise `FakeDecisionModel`. Aucun test de la suite par défaut ne dépend d'un modèle local ou d'un service externe ; la validation sur le vrai serveur est un banc séparé (`../benchmarks/clef/`).
- **Isochrones** : toujours `FakeIsochroneProvider` dans les tests.

## Commandes

```bash
docker compose up                      # db + api
docker compose --profile etl run --rm etl <source>   # ingestion
dotnet build
dotnet test
dotnet format --verify-no-changes
dotnet run --project src/Etl -- <source>   # ingestion d'une source
```

## Définition de « terminé » (backend)

`dotnet test` et `dotnet format` passent ; OpenAPI à jour ; migration validée par l'humain ; provenance documentée ; calcul reproductible ; fonctionne avec `AI_ENABLED=false` et sans clé d'isochrones (`fake`).
