# Neighbourhood Explorer

A student, non-commercial web application for exploring the neighbourhoods (_mahalle_) of Istanbul on a map. The map is the product: metrics, criteria and filtered lists exist to explain what you see on it.

The backend is ASP.NET Core 9 + PostgreSQL/PostGIS. The frontend is React 19 + TypeScript + Vite+ (`vp`). The AI layer is optional and disabled by default; both workflows below work without it and without a GPU.

This README is self-contained. Read section 2 to install the dependencies, section 3 to start the application, then follow workflow 1 (section 4) and workflow 2 (section 5).

---

## 1. Repository layout

```
/
├── backend/           ASP.NET Core API, EF Core migrations, ETL
│   └── openapi/       versioned API contract (source of truth)
├── frontend/          React + TypeScript + Vite+ application
│   └── src/generated/ API types generated from the OpenAPI contract
├── data/
│   ├── sources/       ETL manifests (committed)
│   ├── raw/           raw downloads (empty in this archive)
│   ├── processed/     ETL reports
│   └── outbox/        confirmation emails written by the dev mail sender
├── scripts/           database target selection, Compose helpers
├── docker-compose.yml db + api services, plus the ETL profile
├── .env.example       safe, committed configuration template
├── mise.toml          pinned tool versions and task shortcuts
└── AGENTS.md          project rules and documentation index
```

---

## 2. Prerequisites (everything the teacher needs)

Choose **one** of the three run modes. Section 3 gives the complete commands for each.

| Mode                            | What must be installed on the machine                                                               | Where |
| ------------------------------- | --------------------------------------------------------------------------------------------------- | ----- |
| **A — with mise (recommended)** | only [mise](https://mise.jdx.dev) and Docker; mise installs Node, .NET and `vp`                     | 3.1   |
| **B — without mise**            | Node.js 22, pnpm 12.11.2, .NET SDK 9, and Docker for the database (or a local PostgreSQL + PostGIS) | 3.2   |
| **C — Docker only**             | Docker + Compose only; no Node and no .NET on the host                                              | 3.3   |

| Tool                           | Version                                               | Notes                                                                               |
| ------------------------------ | ----------------------------------------------------- | ----------------------------------------------------------------------------------- |
| mise                           | any recent version                                    | `curl -fsSL https://mise.run \| sh`, or `winget install jdx.mise` — only for mode A |
| Node.js                        | 22                                                    | nodejs.org, or `mise install`                                                       |
| pnpm                           | 12.11.2                                               | `npm install -g pnpm@12.11.2`, or `mise install` installs `vp` instead — see below  |
| .NET SDK                       | 9.0.319 (`global.json` allows the latest 9.0 feature) | dotnet.microsoft.com/download, or `mise install`                                    |
| Python                         | 3.11                                                  | only needed for the optional CLEF benchmark                                         |
| Vite+ (`vp`)                   | 1.2.0                                                 | comes with `mise install`, or with the `vite-plus` dependency installed by pnpm     |
| Docker Engine / Docker Desktop | Compose v2 (`docker compose`)                         | required for modes A and C, optional for mode B                                     |
| PostgreSQL + PostGIS           | 16 + 3.4                                              | provided by the `db` Compose service, or installed locally for mode B               |
| `osm2pgsql`, GDAL              | —                                                     | provided by the backend Docker image (for the optional ETL)                         |

Ports **5173** (frontend), **5080** (API) and **5432** (database) must be free. Stop any local PostgreSQL instance before starting, otherwise the Compose database cannot bind port 5432.

---

## 3. First run — choose one mode

In **all three modes**, start by creating the local configuration from the repository root (the committed `.env.example` contains no secrets):

```bash
cp .env.example .env
```

(Because we uploaded the source code directly for the homework the **.env** is already made and will be used)

The API must be reachable at `http://localhost:5080` and the frontend at `http://localhost:5173`. Check the API with `curl http://localhost:5080/api/v1/health`; the expected answer is `{"database":"ok","dataVersion":null,"ai":"disabled"}`. `ai: disabled` is normal: nothing in these workflows needs AI.

### 3.1 Mode A — with mise (recommended)

From the repository root:

```bash
mise install                  # install Node 22, .NET 9, Python 3.11 and vp 1.2.0
mise bootstrap                # dotnet restore + vp install
mise start                    # docker compose up -d --build : PostgreSQL/PostGIS + API
mise dev:web                  # frontend dev server at http://localhost:5173
```

Useful commands: `mise tasks`, `mise stop`, `mise dev:api`, `mise check`, `mise test`, `mise build`.

**If `mise start` does not work properly** (Docker image build failure, API container that will not start, or port 5080 already taken), you can still use mise for the toolchain and start the backend with `mise dev:api`. That task runs `dotnet run` from `backend/` using the settings from the root `.env`, **but it does not start the database**. Start only the database service first:

```bash
docker compose down        # stop a partially started api container; the pgdata volume is kept
docker compose up -d db    # PostgreSQL/PostGIS only, at localhost:5432
mise dev:api               # terminal 1: backend at http://localhost:5080
mise dev:web               # terminal 2: frontend at http://localhost:5173
```

If Docker is unavailable altogether, install PostgreSQL 16 with PostGIS locally, create the `explorer` database and user with the password from `.env`, then run `mise dev:api` and `mise dev:web` without any Compose command. In that case `docker compose down` is not needed.

`mise start` is therefore optional: what matters is that port 5432 has a PostgreSQL database matching `DATABASE_CONNECTION_STRING` in `.env`, and that one API process listens on port 5080. The `.env.example` file ships with a demo password (`explorer`) because the PostgreSQL image refuses to initialise a fresh volume with an empty one; if you change it, change it in both `POSTGRES_PASSWORD` and `DATABASE_CONNECTION_STRING`.

### 3.2 Mode B — no mise: Node and .NET directly

Install Node.js 22, pnpm 12.11.2 and the .NET 9 SDK. Check with `node -v`, `pnpm -v` and `dotnet --version`. The database still has to run: either start only the database container (`docker compose up -d db`) or install PostgreSQL 16 with PostGIS locally and create a database and user both named `explorer`.

**Terminal 1 — database (skip if you use a local PostgreSQL):**

```bash
docker compose up -d db
```

**Terminal 2 — API.** Without mise, nothing loads the root `.env` for `dotnet run`, so export the settings in the shell first.

```bash
# Linux / macOS
export DATABASE_CONNECTION_STRING='Host=localhost;Port=5432;Database=explorer;Username=explorer;Password='
export FRONTEND_BASE_URL=http://localhost:5173
export EMAIL_OUTBOX_PATH=../data/outbox
cd backend
dotnet restore
dotnet run --project src/Api/Api.csproj
```

```powershell
# Windows PowerShell
$env:DATABASE_CONNECTION_STRING = 'Host=localhost;Port=5432;Database=explorer;Username=explorer;Password='
$env:FRONTEND_BASE_URL = 'http://localhost:5173'
$env:EMAIL_OUTBOX_PATH = '../data/outbox'
cd backend
dotnet restore
dotnet run --project src/Api/Api.csproj
```

`EMAIL_OUTBOX_PATH=../data/outbox` is relative to the `backend` folder: confirmation emails then land in the repository's `data/outbox/`, as in the other modes. The API applies the EF Core migration automatically on startup, so the account tables are created on first run.

**Terminal 3 — frontend:**

```bash
cd frontend
pnpm install --frozen-lockfile       # uses the committed pnpm-lock.yaml
pnpm dev                             # runs "vp dev" → http://localhost:5173
```

On Windows PowerShell the two lines are identical. If `pnpm` is missing, install it first with `npm install -g pnpm@12.11.2` (or `corepack enable && corepack prepare pnpm@12.11.2 --activate`).

The Vite dev server reads the root `.env` itself and proxies `/api` to `http://127.0.0.1:5080`; leave `VITE_API_URL` empty.

### 3.3 Mode C — no Node and no .NET: Docker only

Compose builds the backend image (which already contains the .NET SDK/runtime, `osm2pgsql` and GDAL) and starts PostgreSQL/PostGIS. The frontend is not a Compose service, so it runs in a temporary Node container in a second terminal. The first run takes a few minutes (image build + dependency download).

**Terminal 1 — database and API:**

```bash
docker compose up -d --build db api
docker compose logs -f api          # wait for "Now listening on: http://[::]:8080", then Ctrl+C
```

**Terminal 2 — frontend (Linux / macOS):**

```bash
docker run --rm -it \
  --name neighbourhood-web \
  -v "$PWD/frontend:/app" \
  -v /app/node_modules \
  -w /app \
  -p 5173:5173 \
  -e VITE_API_URL=http://localhost:5080 \
  node:22-alpine \
  sh -c "npm install -g pnpm@12.11.2 && pnpm install --frozen-lockfile && pnpm dev --host 0.0.0.0"
```

**Terminal 2 — frontend (Windows PowerShell):**

```powershell
docker run --rm -it `
  --name neighbourhood-web `
  -v "${PWD}/frontend:/app" `
  -v /app/node_modules `
  -w /app -p 5173:5173 `
  -e VITE_API_URL=http://localhost:5080 `
  node:22-alpine `
  sh -c "npm install -g pnpm@12.11.2 && pnpm install --frozen-lockfile && pnpm dev --host 0.0.0.0"
```

`-v /app/node_modules` keeps the installed packages inside the container, so no root-owned files are written into your `frontend` folder. `VITE_API_URL=http://localhost:5080` is important: the browser then calls the API directly on the host (CORS allows `http://localhost:5173`). If it were left empty, the browser would send `/api` requests to the Node container, where no API is listening.

Stop and clean up:

```bash
# Ctrl+C in terminal 2 (removes the frontend container because of --rm)
docker compose down                # in terminal 1: stops db + api
```

The ETL can also be run entirely through Docker in this mode; see section 6.

> The interface ships in French; use the language switcher in the header to display English (`en`), Turkish (`tr`) or Arabic (`ar`). The French labels used in workflows 1 and 2 are translated inline below.

---

## 4. Workflow 1 — Register, log in and manage the account

**Requirement covered:** a user can create an account, confirm it, log in, view their profile details, manage favorites and log out.

### Step 1 — Create the account

1. Open <http://localhost:5173>.
2. Click **Se connecter** ("Sign in") in the header, then **Créer un compte** ("Create an account"), or go directly to <http://localhost:5173/signup>.
3. Fill in:
   - **Adresse électronique**: a valid, unused address (for example `teacher@example.test`);
   - **Nom affiché**: 1 to 80 characters;
   - **Mot de passe**: at least 12 characters (no other composition rule, but common passwords are rejected).
4. Click **Créer mon compte**. The page confirms that a verification email was queued.

The API answers `202 {"status":"confirmation_sent"}` whether or not the address already existed: registration does not disclose which addresses are registered.

### Step 2 — Confirm the email address

This development build does not send real email. The confirmation message is written as a `.txt` file into `data/outbox/` (mounted from the host into the API container), for example:

```
To: teacher@example.test
Subject: Confirm your email address

http://localhost:5173/confirm-email?userId=…&token=…
```

Find the newest file and copy the link:

```bash
# Linux / macOS
latest=$(ls -t data/outbox/*.txt | head -1)
grep -o 'http://localhost:5173/confirm-email[^[:space:]]*' "$latest"
```

```powershell
# Windows PowerShell
(Get-ChildItem data\outbox\*.txt | Sort-Object LastWriteTime -Descending | Select-Object -First 1 |
  Get-Content | Select-String 'http://localhost:5173/confirm-email').Line
```

Open that URL in the browser. The page shows **Adresse confirmée** ("Address confirmed"). The link is valid for 24 hours. Then click **Aller à la connexion**.

### Step 3 — Log in

1. On <http://localhost:5173/login>, enter the same email and password.
2. Click **Se connecter**. You are redirected to the home page.

The session is a `HttpOnly` cookie named `ne.session` (14-day sliding expiration) plus an anti-forgery token `ne.csrf`. Five failed attempts lock the account for 15 minutes, and authentication routes are limited to 10 requests per minute per IP — wait a minute if you hit `429 rate_limited`.

### Step 4 — View profile details and manage the account

Open the account menu in the header (it shows the display name you chose). It contains:

- the **email address** and display name returned by `GET /api/v1/me`;
- **Favoris** ("Favorites") → `/favorites`: neighborhoods saved locally before login are merged here after sign-in; each row can be removed or opened on the map with **Voir sur la carte**;
- **Administration** → `/admin/users`, only when the address is listed in `ADMIN_EMAILS` in `.env` (an administrator can list accounts and permanently remove another account, but not their own);
- **Se déconnecter** ("Sign out") → clears the session.

The profile API responses are stable and typed:

```
GET /api/v1/me → { userId, email, displayName, locale, isDemo, isAdmin }
```

**Scope of this slice:** profile details are displayed and readable through the API, and the session/favorites are manageable from the interface. In-place editing of `displayName`/`locale`, password change and self-service account deletion are not part of the delivered interface.

### What proves the workflow works

- `/signup` → confirmation file in `data/outbox/` → `/confirm-email` → `/login` → account menu shows the new address.
- `curl -b cookies.txt -c cookies.txt http://127.0.0.1:5080/api/v1/me` returns the signed-in profile.
- The session cookie disappears after logout and `/api/v1/me` then returns `401 not_authenticated`.

---

## 5. Workflow 2 — Find a neighborhood on the map

**Requirement covered:** locate a neighborhood by its place name (and its district/city), then display its profile information. The interface is at <http://localhost:5173/map>.

### Step 1 — Open the neighborhood list

1. Go to <http://localhost:5173/map>.
2. The map is centered on Istanbul and shows the mahalle polygons colored by the selected metric (`population_density` by default).
3. The **Quartiers** ("Neighborhoods") panel is open on the left. If it was closed, click the **Quartiers** button at the top left.
4. Under **Aller à un quartier** ("Go to a neighborhood"), the **Choisir un quartier** list contains every loaded neighborhood, sorted alphabetically, in the form `Neighborhood · District` (for example `Moda · Kadıköy`). The native list supports type-ahead: start typing a neighborhood name or a district name to jump to the matching entries. This is how you search by location name; the city is Istanbul (province code `TR-34`).

### Step 2 — Open a neighborhood profile

1. Select a name in the list, or click its polygon directly on the map.
2. The map flies to/ highlights that neighborhood, and the panel shows its profile card:
   - neighborhood name;
   - district;
   - the selected metric's value with its unit, formatted for the active locale (or _Pas de donnée_ when no observation exists);
   - the quality label (measurement, estimate, district value, stale or low coverage);
   - the metric name.
3. Under **Source et méthode**, the card lists the data source, attribution, license, methodology, observation date, retrieval date and source URL. This is the provenance of every value shown.
4. **Recentrer sur Istanbul** clears the selection and fits the map back to all loaded neighborhoods.
5. **La lentille** opens the criteria panel; removing a criterion there changes the layer used to color the map without leaving the page.

The selection is stored in the URL (`?neighborhood=<id>`), so a refresh or a shared link reopens the same neighborhood.

### What proves the workflow works

- `GET /api/v1/cities` lists Istanbul, `/api/v1/cities/34/neighborhoods` returns a `FeatureCollection`, and `/api/v1/cities/34/metric-values?metrics=population_density` returns values keyed by neighborhood id.
- Choosing `Moda · Kadıköy` in the list draws the highlight and displays its name, district and value in the profile card.
- The API also exposes the underlying detail endpoints `GET /api/v1/neighborhoods/{id}` and `GET /api/v1/neighborhoods/{id}/metrics` with the official hierarchy and full metric history.

**Scope of this slice:** search is by neighborhood or district name through the sorted picker; the city is fixed to Istanbul. Postal-code search and a separate, dedicated neighborhood profile _page_ are not implemented — the profile card in the map panel is the current profile view.

---

## 6. Checks and troubleshooting

```bash
# Mode A (mise), from the repository root
mise test                  # frontend + backend test suites
mise check                 # frontend format/lint + backend format
mise build                 # frontend + backend builds

# Mode B (no mise): from frontend/ use pnpm, from backend/ use dotnet
cd frontend && pnpm exec vp test && pnpm exec vp check && pnpm exec vp build
cd ../backend && dotnet test && dotnet build

# Mode C (Docker only): no test runner needed to use the app; backend tests use
# Testcontainers and therefore need Docker, frontend tests can run in the node container:
docker run --rm -v "$PWD/frontend:/app" -v /app/node_modules -w /app node:22-alpine \
  sh -c "npm install -g pnpm@12.11.2 && pnpm install --frozen-lockfile && pnpm exec vp test"
```

Common problems:

| Symptom                                                                                                 | Cause / fix                                                                                                                                                                                                                          |
| ------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `GET /api/v1/cities` returns `{"cities":[]}`, map returns `404 city_not_found`                          | No ETL data: follow section 6.                                                                                                                                                                                                       |
| A browser console error about `content.js` and `unsafe-eval`                                            | A Chrome extension injecting a script that uses `eval`; unrelated to the application. Disable the extension or ignore the message.                                                                                                   |
| `csrf_failed` on a write                                                                                | Fetch `/api/v1/auth/csrf` again after login/logout; the anti-forgery token is bound to the current session.                                                                                                                          |
| `429 rate_limited`                                                                                      | 10 authentication requests per minute per IP; wait a minute.                                                                                                                                                                         |
| `email_not_confirmed`                                                                                   | Open the latest confirmation link from `data/outbox/`.                                                                                                                                                                               |
| Port 5432 already in use                                                                                | Stop the local PostgreSQL service; the Compose database needs that port.                                                                                                                                                             |
| `permission denied` on `/var/run/docker.sock`                                                           | Add your user to the `docker` group and log in again, or run the commands through `sudo docker` if your system requires it.                                                                                                          |
| `pnpm: command not found`                                                                               | Install it: `npm install -g pnpm@12.11.2` (or `corepack enable && corepack prepare pnpm@12.11.2 --activate`).                                                                                                                        |
| The API starts but `/api/v1/health` says the database is unavailable, or accounts endpoints are missing | In mode B the root `.env` is not loaded by `dotnet run`: export `DATABASE_CONNECTION_STRING` (and the other variables shown in 3.2) in that shell before starting the API.                                                           |
| The frontend shows API 404s in mode C                                                                   | `VITE_API_URL=http://localhost:5080` must be set on the `docker run` command; otherwise `/api` calls go to the frontend container.                                                                                                   |
| `mise start` fails or the API container will not start                                                  | Use the `mise dev:api` fallback in 3.1: `docker compose down`, `docker compose up -d db`, then `mise dev:api` and `mise dev:web`.                                                                                                    |
| Fresh database will not initialise                                                                      | `.env.example` now contains the demo password `explorer`; the PostgreSQL image rejects an empty `POSTGRES_PASSWORD` on the first start. Keep `POSTGRES_PASSWORD` and the `Password=` part of `DATABASE_CONNECTION_STRING` identical. |

---

## 7. Tests, contract and data credits

- The OpenAPI contract in `backend/openapi/openapi.v1.json` is the source of truth; `frontend/src/generated/api.d.ts` is generated from it with `vp exec openapi-typescript ../backend/openapi/openapi.v1.json -o src/generated/api.d.ts`.
- Frontend tests render components in SSR and validate API responses at the boundary with zod. Backend tests use a real PostgreSQL/PostGIS database and real Identity; deterministic fakes replace only external services (email, AI, isochrones).
- Map data and basemap: © [OpenStreetMap contributors](https://www.openstreetmap.org/copyright) (ODbL), OpenFreeMap and OpenMapTiles (attribution shown in the map control). WorldPop 2025 is CC BY 4.0. Full source list, licences and methodology are in `data/sources/*.yaml` and `dependencies.md`.

This project is a non-commercial student project with no monetisation, no SLA and no production maintenance commitment.
