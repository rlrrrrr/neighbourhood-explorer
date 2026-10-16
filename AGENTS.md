# AGENTS.md

Application web d'exploration des quartiers (*mahalle*) de Turquie, démarrée sur Istanbul.

## Contexte du projet

Projet **étudiant, non commercial, non destiné à être maintenu à long terme**. Conséquences :

- **La rigueur technique reste entière** : qualité du code, tests, sécurité, reproductibilité des données, provenance, explicabilité. « C'est un projet étudiant » n'est jamais une excuse pour bâcler.
- **Seul l'aspect commercial est relâché** : pas de monétisation, pas de SLA, pas de montée en charge, pas d'achat de licence commerciale, pas d'optimisation de coûts d'exploitation. Ne pas bâtir de fonctionnalité pour un usage commercial.
- **Les obligations légales restent** même sans but commercial : attribution OpenStreetMap et des fournisseurs, respect des conditions d'utilisation des API et des licences de données, vie privée (KVKK/RGPD).
- Périmètre réduit : pas de microservices, pas de sur-ingénierie, pas d'anticipation de maintenance sur plusieurs années. Une dépendance jeune (ex. pré-alpha) est acceptable si sa version est épinglée et son usage isolé.

**La carte est le produit.** On ne construit pas un dashboard : la carte est la lentille qui donne un sens à la donnée, les métriques et graphiques servent à expliquer ce qu'on voit dessus. L'application a trois vues (Chat, Carte, Données) qui partagent un seul état de critères ; chaque résultat se projette sur la carte (« Voir sur la carte »). Voir `docs/product-direction.md`.

## Où lire quoi

| Tu travailles sur… | Lis d'abord |
|---|---|
| Le frontend (`frontend/`) | `frontend/AGENTS.md` (contient un résumé du contrat backend) |
| Le backend, l'ETL, la base (`backend/`) | `backend/AGENTS.md` |
| Vision, UX, phases, décisions ouvertes | `docs/product.md` |
| Direction validée : trois vues, carte, palette, rôle de CLEF | `docs/product-direction.md` |
| Score de matching | `docs/matching.md` |
| CLEF-Flash : format, rôle dans le matching, exécution | `docs/clef.md` |
| Docker, services, CI, hébergement | `docs/infrastructure.md` |
| Isochrones (« quartier à 10 min ») | `docs/isochrones.md` |
| Données, sources, licences, métriques | `docs/data.md` |
| Sources open data d'Istanbul par métrique | `docs/data-istanbul.md` |
| Stratégie de test, invariants, vérification formelle | `docs/verification.md` |

Si ce fichier et un sous-fichier se contredisent, le sous-fichier le plus proche du code modifié gagne. Signale la contradiction.

## Règles non négociables

**Toujours**
- Faire de la carte la destination de toute donnée : chaque résultat (chat, classement, fiche) a un accès « Voir sur la carte » ; dans la vue Carte, elle reste visible et dominante.
- Faire fonctionner l'application **sans IA et sans GPU**. L'IA est une couche optionnelle, jamais dans le chemin critique (carte, métriques, filtres, comparaison, navigation).
- Documenter la provenance de chaque donnée : source, méthodologie, licence, date, transformation, normalisation (voir `docs/data.md`).
- Rendre les calculs reproductibles et exécutés hors du chemin utilisateur (ETL → PostGIS → API).
- Séparer valeur brute et valeur normalisée, et conserver la date d'observation.
- Garder le domaine générique : ne jamais coder Istanbul « en dur » dans la logique métier.
- Rendre le score explicable : poids explicites, contributions par critère.

**Jamais**
- Dépendre d'un modèle d'IA, d'une API payante ou d'un service externe pour afficher la carte.
- Afficher un « meilleur quartier » universel ou un score global arbitraire.
- Afficher une précision artificielle (`8.2 cafés/km²`, pas `8.237491`).
- Présenter les indicateurs de risque (séisme, inondation…) comme des garanties de sécurité.
- Commiter un secret (clé API, token, mot de passe). Utiliser des variables d'environnement et `.env.example`.
- Construire des interactions entre utilisateurs : messagerie, abonnements, commentaires, contenu publié par les utilisateurs. Autorisés : les formes de consommation (fil *Explorer*, classements calculés, tendances issues des données, sélection locale, partage par lien) et le chat avec CLEF, qui n'est pas une messagerie. Comptes, likes et tendances d'audience : phase ultérieure (`docs/product-direction.md`).
- Créer 50 métriques avant d'avoir une bonne UX : MVP = 11 métriques (`docs/data.md`).

**Arbitrage** : en cas de conflit entre sophistication technique et expérience utilisateur, choisir l'expérience et la simplicité.

## Hiérarchie administrative (important)

En Turquie : `province (il)` > `district (ilçe)` > `quartier (mahalle)`.
Kadıköy est un **ilçe** ; Moda est un **mahalle**. Le modèle de données gère les niveaux explicitement (voir `backend/AGENTS.md`). Les exemples de la doc qui parlent de « quartier Kadıköy » sont des raccourcis.

## Structure du dépôt

```
/
├── AGENTS.md
├── docker-compose.yml          # db + api ; profils "etl" et "ai" (frontend lancé avec vp en local)
├── .env.example
├── infra/
│   └── clef/Dockerfile         # llama-server + CLEF-Flash (optionnel)
├── data/                       # raw/ processed/ normalized/ (ETL), hors Git sauf manifestes
├── docs/                       # documentation produit et technique
├── benchmarks/                 # benchmarks séparés (ex. benchmarks/clef/)
├── frontend/                   # React + TypeScript + Vite+  (voir frontend/AGENTS.md)
└── backend/                    # ASP.NET Core + PostGIS      (voir backend/AGENTS.md)
    ├── openapi/                # contrat d'API versionné (source de vérité)
    ├── src/
    └── tests/
```

## Commandes

À ajuster au scaffold réel ; mettre ce bloc à jour dès qu'une commande change. **Utiliser Vite+ (`vp`) pour tout le frontend** ; ne pas appeler `pnpm`/`npm`/`vite`/`vitest` directement pour ces tâches. Les outils (Node, SDK .NET 9, Python, `vp`) sont épinglés par `mise.toml` : après un clone, `mise install` puis `mise bootstrap`.

```bash
# Tâches unifiées (mise, depuis la racine)
mise bootstrap        # dépendances backend + frontend
mise start             # docker compose up -d --build (db + api)
mise stop              # docker compose down
mise dev:web          # vp dev (serveur de dev frontend)
mise dev:api          # API locale dotnet (http://localhost:5080)
mise etl -- <source>  # docker compose --profile etl run --rm etl <source>
mise clef             # CLEF-Flash seul (profil "ai")
mise check            # vp check + dotnet format --verify-no-changes
mise test             # vp test + dotnet test
mise build            # vp build + dotnet build
mise smoke            # benchmarks/clef/smoke-test.py
mise i18n             # fbtee : chaînes sources + catalogues de traduction (frontend)
mise tasks            # liste complète

# Infrastructure (PostgreSQL + PostGIS + API)
docker compose up
docker compose --profile etl run --rm etl <source>   # ingestion d'une source
docker compose --profile ai up -d clef   # CLEF-Flash seul (premier démarrage : téléchargement du modèle, plusieurs minutes)
python benchmarks/clef/smoke-test.py     # test minimal de /v1/systemone (CLEF_URL pour changer l'adresse)

# Frontend (depuis frontend/)
vp install
vp dev                                # serveur de dev
vp check                              # format + lint (+ types si `typeCheck` est activé dans `lint.options` de vite.config.ts)
vp check --fix                        # corrige ce qui peut l'être
vp test run <chemin/complet.test.ts>   # un seul fichier, une exécution (voir « Tests »)
vp test                               # toute la suite
vp build                              # build de production
mise i18n                             # fbtee : collecte les chaînes source + compile les catalogues (voir frontend/AGENTS.md « Interface et textes »)

# Backend (depuis backend/)
dotnet build
dotnet test
dotnet format --verify-no-changes
```

## Budgets de performance (testables)

- Changement de métrique sur la carte : **< 100 ms** (sans requête réseau si les données sont déjà chargées).
- Premier affichage de la carte : **< 2 s** sur connexion standard.
- Pas de requête réseau à chaque `mousemove` ; debounce sur les sliders.
- Réponse API de lecture (métriques, quartiers) : **< 300 ms** p95 en local.

## Définition de « terminé »

Une tâche est terminée quand :
1. les vérifications finales passent **une fois, avant le résumé final** (pas après chaque modification) : `vp test` et `vp check` (frontend) ; `dotnet test` et `dotnet format --verify-no-changes` (backend) ;
2. les tests couvrent le comportement ajouté, et tout bug corrigé a un test de non-régression ;
3. aucune donnée ni secret n'a été ajouté sans provenance / sans `.env.example` ;
4. le contrat d'API (`backend/openapi/`) est à jour si l'API a changé, et le frontend a régénéré ses types ;
5. la documentation concernée (`docs/`, `AGENTS.md`) est mise à jour ;
6. la fonctionnalité fonctionne sans IA et sans GPU.

## Autonomie

**Tu peux faire seul** : créer des fichiers dans une feature existante, ajouter des tests, refactorer à l'intérieur d'une feature, corriger des bugs, mettre à jour la doc.

**Demande avant de** :
- ajouter une dépendance (npm ou NuGet) ;
- créer ou modifier une migration de base de données ;
- modifier le contrat d'API de façon incompatible ;
- ajouter une source de données (vérifier sa licence) ;
- brancher un service externe à quota (isochrones, IA) ;
- modifier les seuils ou formules de matching (`docs/matching.md`) ;
- supprimer des données ;
- introduire de la vérification formelle (voir `docs/verification.md`).

## Réponses et retours à l'utilisateur

### Questions ouvertes : « la suite ? », « autre chose ? »

Quand l'utilisateur termine une tâche par une invite ouverte (« next steps ? », « anything else ? », « something else ? », « anything remaining ? », « what's next ? », ou en français « la suite ? », « autre chose ? », « il reste quoi ? »), c'est une vraie invitation à proposer des **améliorations, refactorings, tests, docs ou cas limites** qui valent la peine. Les regrouper **tous dans un seul message**, jamais un par un.

Quand il demande « des questions ? » ou « besoin de quelque chose de ma part ? » : rassembler toutes les inconnues en suspens et les poser dans **un seul message groupé**. Si une question peut être résolue en explorant le code, le faire plutôt que de la poser. S'il n'y a rien : « Nothing else — all good. »

### Format des sections

Quand tu proposes du travail, demandes un retour ou présentes des changements :

- Utiliser des éléments **indexés** pour que l'utilisateur réponde précisément : `1`, `2`, `3` dans une seule section ; `A1`, `A2`, `B1` s'il y a plusieurs sections.
- Mettre les changements terminés ou proposés sous une section **`SHOWCASE`** clairement nommée.
- Ajouter une section **`FEEDBACK NEEDED`** uniquement quand une vraie question ou décision est nécessaire. Toutes les questions y vont, indexées, séparées de `SHOWCASE`. Omettre la section s'il n'y a pas de question.

## Investigation et frontières

- Préférer le **code local au domaine** aux fourre-tout génériques. Ne créer du code partagé que lorsque plusieurs appelants réels ont une sémantique ou un invariant stable en commun, et le placer à la frontière de domaine existante la plus étroite.
- **Bibliothèque inconnue** : identifier la version installée et vérifier l'API exacte dans la documentation officielle de **cette version** avant d'implémenter. Particulièrement pour TanStack Charts (pré-alpha), mapcn, Vite+.
- **Artefacts générés** (schémas, migrations, clients générés, snapshots) : sortie dérivée. Modifier la source de vérité, régénérer, inspecter le diff, valider le résultat. Ne jamais éditer à la main un fichier généré pour masquer un problème de source.
- **Base de données** : investiguer d'abord **en lecture seule** : schéma, index, contraintes, données représentatives bornées, plans de requête, avant de toucher au code de base de données ou aux migrations.
- **Comportement navigateur/runtime** : investiguer sur l'application qui tourne. Corréler les étapes de reproduction avec erreurs console, requêtes réseau, état du DOM et styles calculés, au lieu de se fier à la seule lecture du code.
- **Bugs** : reproduire avant de corriger, énoncer une hypothèse de cause racine falsifiable, ajouter un test de non-régression.
- **Revue de sécurité** de chaque changement : autorisation, validation des entrées, injection, exposition de secrets, fuite de données sensibles, exécution de processus dangereuse, risque lié aux dépendances. Ne jamais afficher de valeurs sensibles pendant l'investigation.
- **Preuves de validation compactes** : lancer la commande utile la plus étroite, conserver les journaux complets dans un fichier si nécessaire, signaler les échecs nouveaux séparément des échecs déjà connus.
- **Observabilité structurée** à la frontière du cas d'usage / de l'unité de travail : champs typés, identifiants de corrélation, charges bornées, masquage des données sensibles. Pas d'événements d'audit en double ; pas de journalisation au niveau des adaptateurs qui brouille la responsabilité.
- **Données externes** : décoder **une seule fois à la frontière** ; les API internes manipulent des types nommés et validés.
- Éviter les assertions en chaîne, les contrats larges `object`/`unknown`, et les assertions sur du JSON de réponse brut.
- Ne pas propager (`spread`) des lignes de base de données ou des données de transport contrôlées par l'appelant dans les objets de sortie : énumérer les champs qui franchissent la frontière.

## Tests (règles communes)

- **Éviter les mocks : tester l'implémentation réelle.** Pas de bibliothèque de mocking. Pour les seules frontières externes non déterministes ou à quota (modèle d'IA, fournisseur d'isochrones), utiliser une **implémentation de remplacement réelle de l'interface** (`FakeDecisionModel`, `FakeIsochroneProvider`) qui renvoie des données déterministes, jamais un mock de comportement.
- **Déboguer en lançant un seul fichier**, pas toute la suite. Utiliser le chemin complet et `vp test run <fichier>` (une exécution, sans mode watch) pour un retour rapide ; ajouter `.only` pour isoler un test en échec (le retirer avant de terminer).
- Lancer la suite complète et les vérifications **une seule fois avant le résumé final**.
- Voir `docs/verification.md` pour les invariants et les tests de propriétés.

## Style de code commun

- **Pas de commentaires par défaut.** Un commentaire n'est ajouté que si le *pourquoi* n'est pas évident.
- Commentaires inacceptables : ceux qui répètent ce que fait le code, le code commenté, les commentaires évidents, les commentaires à la place d'un bon nommage. Si un commentaire est nécessaire pour expliquer *ce que* fait le code, **refactorer** : le code doit se documenter lui-même.
- Noms de variables **descriptifs**, pas d'abréviations.
- Supprimer le code inutilisé, éviter la répétition.
- Éviter les arguments multiples : préférer un **objet unique** en paramètre.
- N'exporter que ce qui est destiné à un usage externe.
- Les règles propres à TypeScript sont dans `frontend/AGENTS.md`, celles de C# dans `backend/AGENTS.md`.

## Conventions

- Code et identifiants : **anglais**. Documentation : français.
- Noms de fichiers explicites : le nom dit ce que contient le fichier. Pas de `utils`, `helpers`, `common`, `misc`. Casse : **kebab-case côté frontend**, convention C# (PascalCase) côté backend.
- Commits : [Conventional Commits](https://www.conventionalcommits.org/) (`feat:`, `fix:`, `docs:`, `test:`, `refactor:`).
- Simple > astucieux. Données reproductibles > données manuelles. Méthodologie explicite > score mystérieux.
