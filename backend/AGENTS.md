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
│   │   ├── Trending/               # événements anonymes + agrégats
│   │   └── SavedNeighborhoods/     # phase 3
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
name, official_name, official_code (nullable), geometry, centroid, valid_from, valid_to

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
- Les erreurs « fonctionnalité indisponible » (IA off, quota isochrone épuisé) renvoient `503` avec `code` explicite (`ai_disabled`, `ai_unavailable`, `ai_busy`, `isochrone_quota_exceeded`), jamais `500`. `ai_timeout` → `504`. Texte hors sujet → `422 not_a_neighborhood_request`.
- Aucun endpoint ne dépend de l'IA sauf `/preferences/parse` et `/chat/turn`.

| Endpoint | Rôle |
|---|---|
| `GET /api/v1/cities` | Villes disponibles |
| `GET /api/v1/cities/{cityId}/neighborhoods` | GeoJSON simplifié (précision réduite), `Cache-Control` long |
| `GET /api/v1/metrics` | Définitions de métriques + source + date |
| `GET /api/v1/cities/{cityId}/metric-values?metrics=a,b` | `{ metricKey: { neighborhoodId: { raw, normalized } } }` compact |
| `GET /api/v1/neighborhoods/{id}` | Détail (hiérarchie, géométrie complète, centroïde) |
| `GET /api/v1/neighborhoods/{id}/metrics` | Toutes les métriques du quartier, avec date et source |
| `POST /api/v1/compare` | 2 à 4 `neighborhoodIds` → tableau de métriques |
| `POST /api/v1/match` | Préférences → classement **expliqué** (voir ci-dessous) |
| `POST /api/v1/chat/turn` | **Optionnel (phase IA)** : `{sessionId,text,locale,mapContext}` → `{message, actions[]}`. `message` est composé par **gabarits** à partir de résultats calculés ; `actions[]` = commandes typées de carte (`setCriteria`, `showMatches`, `fitBounds`, `highlight`, `openCard`, `ask`, `setLayer`). Réutilise le service de `/preferences/parse` (intention, besoins, portes) ; mêmes erreurs `ai_*` |
| `POST /api/v1/preferences/parse` | **Optionnel** : texte libre (fr/en) `{cityId,text,locale}` → préférences validées, quartier de référence, ajustements, avertissements |
| `POST /api/v1/chat/turn` | **Optionnel** : barre de commande. `{sessionId,text,locale,mapContext}` → `{message (gabarit), actions[]}` ; enchaîne CLEF, le moteur et les gabarits (`../docs/product-direction.md`, `../docs/clef.md`) |
| `GET /api/v1/neighborhoods/{id}/isochrone?minutes=&mode=` | Isochrone précalculée + comptage de POI |
| `POST /api/v1/isochrones` | Isochrone pour un point arbitraire (cache + quota) |
| `GET /api/v1/trending?period=7d` | Classement par popularité |
| `POST /api/v1/events` | Événement anonyme (avec consentement) |
| `GET /api/v1/health` | Santé (base, version des données) |

`POST /api/v1/match` (forme, détail dans `../docs/matching.md`) :

```jsonc
// requête
{
  "cityId": 34,
  "preferences": [
    { "metricKey": "transport_accessibility", "weight": 0.9, "target": "high" },
    { "metricKey": "education_accessibility",   "weight": 1.0, "target": "high" },
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
    "weaknesses": ["education_accessibility"]
  }],
  "methodVersion": "match-v1"
}
```

Le moteur renvoie des **contributions par critère** : l'explication est calculée, jamais inventée par un modèle.

## Matching

Détails dans `../docs/matching.md`. Points impératifs pour l'implémentation :
- Fonction pure `ScoreNeighborhoods(preferences, metricValues) -> results`.
- Poids explicites ; les poids sont renormalisés (somme = 1) avant le calcul.
- Valeurs manquantes : le critère est **exclu** pour ce quartier et les poids restants sont renormalisés ; `coverage` est renvoyé ; sous un seuil de couverture le quartier est signalé, pas masqué silencieusement.
- Tri total et déterministe (égalité de score → `neighborhoodId` croissant).
- Le moteur ne connaît ni l'IA, ni la base, ni HTTP.

## IA optionnelle

CLEF-Flash est un **modèle de décision** : il reçoit un état (ici **le texte libre brut de l'utilisateur**) et des questions typées en langage naturel, et renvoie des probabilités sur des options ; il ne génère pas de texte. Détail complet (format, questions, interprétation, exécution) dans `../docs/clef.md`.

```csharp
public interface IDecisionModel
{
    Task<DecisionAnswers> DecideAsync(DecisionRequest request, CancellationToken cancellationToken);
}
```

L'interface est **générique** (état + questions → probabilités). La logique « préférences » est séparée en composants **déterministes** : `PreferenceQuestionCatalog` (`MetricDefinition` + `MetricTranslation` + langue → questions), `PreferenceAnswerInterpreter` (réponses → préférences, `preferences-v2`) et `NeighborhoodNameMatcher` (nom de quartier cité → identifiant, normalisation turque). Le service `/preferences/parse` les enchaîne autour de `IDecisionModel` en deux passes (questions générales, puis ajustements si un quartier de référence est reconnu), sous un budget global `AI_TIMEOUT_SECONDS`.

Implémentations : `FakeDecisionModel` (dev et tests, règles déterministes par mots-clés, même forme de réponse), `ClefLlamaServerDecisionModel` (HTTP vers `llama-server`, `POST /v1/systemone`). Le reste du code ne dépend que de l'interface.

Préférences (schéma strict) : liste de `{ metricKey, weight ∈ [0,1], target ∈ {high, low, any}, source }` (8 critères au plus), éventuellement un quartier de référence et des ajustements. Formes exactes dans `../docs/clef.md`.

Règles :
- **La sortie d'un modèle est une entrée non fiable.** La valider : clés connues dans `MetricDefinition`, poids bornés, pas de doublon, taille max. Si invalide → erreur `ProblemDetails`, jamais de correction silencieuse.
- Le texte utilisateur est non fiable (injection de prompt) : il n'entre que dans l'état du modèle, qui ne déclenche aucune action et ne renvoie que des probabilités sur des options que **nous** définissons.
- `llama-server` traite **une requête à la fois** : file d'attente bornée (2 en attente au plus, sinon `503 ai_busy`), délai maximal `AI_TIMEOUT_SECONDS`, limitation de débit par client, texte limité à 500 caractères.
- `AI_ENABLED=false` : `/preferences/parse` renvoie `503 ai_disabled` ; tout le reste fonctionne.
- Le modèle ne produit jamais de score, de classement ni d'explication : ces éléments viennent du moteur de matching.
- CLEF-Flash local est **expérimental** : benchmark séparé (`benchmarks/clef/`), jamais supposé rapide. Profil Docker `ai` uniquement.
- Texte utilisateur et probabilités brutes ne sont jamais journalisés en production.

## Isochrones

Pas de serveur de routage auto-hébergé : on s'appuie sur un **service hébergé** derrière `IIsochroneProvider`. Stratégie complète (précalcul, cache, quotas, repli) dans `../docs/isochrones.md`.

## Données et ETL

Détails dans `../docs/data.md`. Impératifs :
- Chaque source a un manifeste (source, version, date de téléchargement, licence, étapes, sortie) ; dossiers `raw/` → `processed/` → `normalized/`.
- ETL reproductible : même entrée → même sortie ; aucune modification manuelle de dataset sans transformation versionnée.
- Aucune métrique sans `DataSource` renseignée (licence et attribution comprises).
- Les calculs lourds (densités, accessibilité, entropie d'usage du sol, isochrones précalculées) vivent dans l'ETL, jamais dans une requête utilisateur.

## Vie privée et sécurité

- Pas de compte utilisateur en MVP. Favoris stockés côté client (localStorage) jusqu'à la phase 3.
- Trending : événements **anonymes** (type, identifiant de quartier, horodatage arrondi, identifiant de session aléatoire non persistant ni lié à une personne). Ne pas stocker d'adresse IP. Activer la collecte seulement avec consentement ; la loi turque KVKK s'applique aux utilisateurs en Turquie, le RGPD aux utilisateurs européens.
- Secrets uniquement par variables d'environnement ; `.env.example` à jour.
- CORS restreint ; limitation de débit sur `/preferences/parse`, `/isochrones` et `/events`.

## Configuration

```
DATABASE_CONNECTION_STRING
AI_ENABLED            # false par défaut
AI_PROVIDER           # fake | clef-local
AI_ENDPOINT
AI_MODEL              # clef-flash
AI_TIMEOUT_SECONDS    # 10 par défaut ; budget global de /preferences/parse
ISOCHRONE_PROVIDER    # fake | geoapify | openrouteservice | valhalla
ISOCHRONE_API_KEY
ISOCHRONE_DAILY_BUDGET
```

Environnements : `Development` (doit rester extrêmement simple), `Test`, `Production`.

## Observabilité

- Logs **structurés émis à la frontière du cas d'usage** (une unité de travail = un événement de fin) : champs typés, identifiant de corrélation, charges bornées, données sensibles masquées. Pas de journalisation dans les adaptateurs (client HTTP, dépôt) qui dupliquerait ou brouillerait la responsabilité ; pas d'événements d'audit en double.
- `GET /api/v1/health` (base, version des données, `ai: disabled | warming | ready | unavailable`), métriques de latence par endpoint, consommation du quota de l'API d'isochrones.
- Ne jamais journaliser de secret, de texte utilisateur complet ni de réponse brute d'un modèle en production.

## Style de code (C#)

En plus des règles communes de `../AGENTS.md` :
- Fichiers et types en PascalCase (convention C#, la règle kebab-case ne vaut que pour le frontend), un type principal par fichier, nom de fichier = nom du type.
- **Un objet unique en paramètre** (un `record` d'entrée nommé) dès qu'il y a plus d'un argument métier ; `CancellationToken` reste un paramètre séparé.
- Membres à expression et fonctions locales quand ils rendent le code plus court sans le rendre moins lisible. Noms descriptifs, pas d'abréviations.
- Pas de commentaires par défaut ; refactorer plutôt que commenter *ce que* fait le code.
- Validation **déclarative et unique, à la frontière** (DTO d'entrée validés une fois) ; le reste du code manipule des types de domaine déjà valides. Ne pas ajouter de bibliothèque de validation sans demander.
- **DTO de sortie énumérant explicitement leurs champs** : ne jamais renvoyer une entité EF ni copier une entité ou une requête entrante dans un DTO par réflexion/mapping automatique générique.
- Pas de `internal`/`public` inutiles : ne rendre publique que l'API réellement utilisée hors de son assembly.
- Pas de dossiers fourre-tout : le code partagé n'existe que s'il a plusieurs appelants réels et une sémantique stable, au niveau de feature le plus étroit.

## Investigation base de données

- Investiguer **en lecture seule d'abord** : schéma, index, contraintes, données représentatives bornées (`LIMIT`), plans de requête (`EXPLAIN`) avant de toucher au code de base ou aux migrations.
- Les migrations EF sont dérivées du modèle : modifier le modèle, générer, **inspecter le SQL produit**, valider. Ne pas éditer une migration à la main pour masquer un problème de modèle ; toute migration est revue par l'humain.

## Tests

Voir `../docs/verification.md` pour la stratégie complète.
- **Éviter les mocks** : pas de bibliothèque de mocking (Moq, NSubstitute…). Tester l'implémentation réelle ; pour la base, **PostGIS réel** via Testcontainers.
- **Unitaires + propriétés (FsCheck)** sur le cœur pur : normalisation, scoring, validation de `UserPreferences`.
- **Intégration (Testcontainers + PostGIS)** : requêtes spatiales, contraintes, vues matérialisées.
- **Contrat** : l'API respecte `openapi.v1.json` (validation de schéma ; tests de propriétés sur l'API possibles avec Schemathesis).
- **IA** : `PreferenceQuestionBuilder` et `PreferenceAnswerInterpreter` sont testés par propriétés ; la suite par défaut utilise `FakeDecisionModel`, implémentation réelle de l'interface à sortie déterministe. Aucun test de la suite par défaut ne dépend d'un modèle local ou d'un service externe. Un **test de fumée réel** contre `llama-server` existe, lancé seulement sur demande (`CLEF_SMOKE=1`, profil `ai` actif).
- **Isochrones** : toujours `FakeIsochroneProvider` dans les tests.
- Déboguer avec un seul test ou une seule classe (`dotnet test --filter`), puis la suite complète **une seule fois avant le résumé final**.
- Bug : le reproduire par un test qui échoue, énoncer une hypothèse de cause falsifiable, corriger, garder le test.

## Revue de sécurité (avant de terminer)

Autorisation, validation des entrées, injection (SQL : requêtes paramétrées uniquement), exposition de secrets, fuite de données sensibles, exécution de processus, risque lié aux dépendances ajoutées.

## Commandes

```bash
docker compose up                      # db + api
docker compose --profile etl run --rm etl <source>   # ingestion
docker compose --profile ai up -d clef               # modèle de décision (optionnel)
dotnet build
dotnet test
dotnet format --verify-no-changes
dotnet run --project src/Etl -- <source>   # ingestion d'une source
```

## Définition de « terminé » (backend)

`dotnet test` et `dotnet format` passent ; OpenAPI à jour ; migration validée par l'humain ; provenance documentée ; calcul reproductible ; fonctionne avec `AI_ENABLED=false` et sans clé d'isochrones (`fake`).
