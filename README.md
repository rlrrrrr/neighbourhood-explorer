# Neighbourhood Explorer

Application web d'exploration des quartiers (*mahalle*) de Turquie, démarrée sur Istanbul.
**La carte est le produit** : les métriques et graphiques servent à expliquer ce qu'on voit sur la carte.

Projet étudiant, non commercial. Le cadre du projet et la liste des documentations sont dans [AGENTS.md](AGENTS.md).

## Prérequis

- [mise](https://mise.jdx.dev) — outils épinglés + tâches (`winget install jdx.mise`)
- [Docker](https://docs.docker.com/desktop/) avec Docker Compose
- Rien d'autre : `mise install` fournit Node, le SDK .NET, Python et `vp` (Vite+)

## Démarrage

```bash
mise install              # outils épinglés par mise.toml
cp .env.example .env      # configuration locale (jamais commité)
mise bootstrap            # dépendances backend + frontend
mise start               # PostgreSQL/PostGIS + API (docker compose)
mise dev:web              # serveur de dev frontend (vp dev)
```

L'API répond sur `http://localhost:5080` (`GET /api/v1/health`), le frontend sur `http://localhost:5173`.

## Tâches utiles

```bash
mise tasks                 # liste complète
mise dev:api               # API en local (dotnet run) au lieu du conteneur
mise etl -- <source>       # ETL data/raw/<source> → PostGIS (profil "etl")
mise clef                  # CLEF-Flash seul (profil "ai", optionnel)
mise check                 # format + lint (frontend) et format (backend)
mise test                  # suites de tests frontend + backend
mise build                 # builds de production
mise smoke                 # smoke test CLEF /v1/systemone (CLEF_URL : http://localhost:8090/v1/systemone)
```

## Structure

```
/
├── AGENTS.md            # cadre projet et règles non négociables
├── mise.toml            # outils épinglés + tâches
├── docker-compose.yml   # db + api ; profils "etl" et "ai"
├── infra/clef/          # llama-server + CLEF-Flash (optionnel)
├── data/                # raw/ processed/ normalized/ (ETL) ; sources/ = manifestes (commités)
├── docs/                # documentation produit et technique
├── benchmarks/          # benchmarks séparés (benchmarks/clef/)
├── frontend/            # React + TypeScript + Vite+ (vp)
└── backend/             # ASP.NET Core + PostGIS ; openapi/openapi.v1.json = contrat
```

## Documentation

Sommaire dans [AGENTS.md](AGENTS.md) (table « Où lire quoi »). Point d'entrée produit : [`docs/product.md`](docs/product.md).

## Licence et attributions

Données et fond de carte © [OpenStreetMap](https://www.openstreetmap.org/copyright) contributors.
Projet étudiant, non commercial. Licences des jeux de données : `docs/data.md` et `docs/data-istanbul.md`.
