# backend/AGENTS.md

Backend of the neighborhood explorer: API, data, matching, ETL. Also read `../AGENTS.md`, `../docs/data.md`, `../docs/matching.md`, `../docs/clef.md`, `../docs/isochrones.md`, `../docs/infrastructure.md`, `../docs/verification.md`.

This file describes **everything the backend must provide**. The frontend (`../frontend/AGENTS.md`) depends on it: the API contract is the boundary between the two.

## Responsibilities

- Geographic entities (administrative hierarchy), metrics, data sources.
- Normalization, scores, rankings, matching, comparison, trends.
- Isochrones (via an external provider, behind an abstraction).
- Natural-language preference analysis (optional, behind an abstraction).
- Ingestion and heavy computation **outside the user path** (ETL).

The backend is the **only** source of business logic. The frontend never implements a score or a normalization.

## Stack

C#, ASP.NET Core (REST API), EF Core where relevant (complex spatial queries: SQL/Npgsql + NetTopologySuite if simpler), PostgreSQL + PostGIS, xUnit, FsCheck (property tests), Testcontainers (integration tests with real PostGIS).

## Organization: one folder per feature

```
backend/
├── openapi/openapi.v1.json         # versioned contract, source of truth
├── src/
│   ├── Api/                        # startup, DI, middleware, ProblemDetails
│   ├── Features/
│   │   ├── Geography/              # cities, districts, neighborhoods, GeoJSON
│   │   ├── Metrics/                # definitions, values, normalization
│   │   ├── Matching/               # pure scoring + /match endpoint
│   │   ├── Comparison/
│   │   ├── Isochrones/             # IIsochroneProvider + cache + precomputation
│   │   ├── PreferenceParsing/      # questions, interpretation, IDecisionModel + implementations (docs/clef.md)
│   │   ├── Accounts/               # Identity, profile, favorites, likes (docs/accounts.md)
│   │   ├── Reviews/                # reviews and moderation (ReviewModeration, background task)
│   │   ├── Places/                 # community pins, validation queue, places/map, places/zone, photo
│   │   └── Trending/               # anonymous events, popularity, offline trends
│   ├── Etl/                        # ingestion console application (see docs/data.md)
│   └── Infrastructure/             # DbContext, migrations, HTTP clients
└── tests/
```

Rules: explicit file names (`NeighborhoodScoreCalculator.cs`, `MatchRequestValidator.cs`), one main type per file, no `Utils`/`Helpers`/`Common`. The **scoring and normalization core is pure code** (no dependency on the database, the network, the clock, or randomness) so that it can be tested with properties.

## Data model

The Turkish administrative hierarchy is handled **explicitly**: `province (il)` > `district (ilçe)` > `neighborhood (mahalle)`.

```
AdminArea
---------
id, parent_id, level (province | district | neighborhood), country_code,
name, official_name, official_code (nullable), geometry, centroid, valid_from, valid_to

MetricDefinition
----------------
id, key (unique), category, unit,
native_level (province | district | neighborhood : level at which the source publishes the value),
polarity (higher_is_better | lower_is_better | neutral),
normalization_method, normalization_version, display_precision, source_id

MetricTranslation      -- per-language texts (fr, en ; tr = beta)
-----------------
metric_id, locale, name, description,
subject (sentence complement used in CLEF's questions, see ../docs/clef.md)
PK (metric_id, locale)

MetricValue            -- source of truth (EAV format)
-----------
admin_area_id, metric_id, observed_at, raw_value, normalized_value,
quality_flag (ok | estimated | stale | low_coverage | inherited), computed_at
PK (admin_area_id, metric_id, observed_at)

DataSource
----------
id, name, organization, url, license, attribution_text, methodology,
retrieved_at, source_version

UserPreferenceProfile   -- phase 3, separate from the metrics
InteractionEvent        -- anonymous, for trending (see "Privacy")
```

Rules:
- `raw_value` and `normalized_value` are **two distinct columns** and are never mixed. `raw_value` is displayed (with its unit); `normalized_value` (0..1, percentile by default) is used for the score. The method is versioned (`normalization_version`).
- **The EAV format is the source of truth, not the read format.** To serve the map, create a materialized view `neighborhood_metric_latest` (latest observation per neighborhood and metric) and a compact `metric-values` payload per city, refreshed by the ETL.
- Database constraints (CHECK, FK, UNIQUE) for everything that can be enforced: `normalized_value BETWEEN 0 AND 1`, valid geometries (`ST_IsValid`), `level` consistent with the parent.
- `inherited`: the source only publishes at the `native_level` (e.g. district); the mahalles below receive the parent's value. The frontend displays it as "district value", never as a measurement of the mahalle. Affected sources and metrics: `../docs/data-istanbul.md`.
- Every migration is reviewed by the human (see `../AGENTS.md`).
- User preferences and social interactions are never mixed with the metrics.

## API contract (v1)

Versioned REST, JSON, **OpenAPI kept up to date in `openapi/openapi.v1.json`**. The frontend generates its types from this file.

Cross-cutting rules:
- Errors: `application/problem+json` (RFC 9457) with a stable `code`.
- Pagination for every unbounded list (`limit`, `cursor`).
- Reads: `ETag` + `Cache-Control`; the metrics and geometry endpoints are cacheable.
- "Feature unavailable" errors (AI off, isochrone quota exhausted) return `503` with an explicit `code` (`ai_disabled`, `ai_unavailable`, `ai_busy`, `isochrone_quota_exceeded`), never `500`. An asynchronous task that exceeds its deadline ends as `failed` with `code: ai_timeout` (never a `504`: the submission request responds immediately). Off-topic text → `422 not_a_neighborhood_request`.
- No endpoint depends on AI except `/preferences/parse` and `/chat/turn`.

| Endpoint | Role |
|---|---|
| `GET /api/v1/cities` | Available cities |
| `GET /api/v1/cities/{cityId}/neighborhoods` | Simplified GeoJSON (reduced precision), long `Cache-Control` |
| `GET /api/v1/metrics` | Metric definitions + source + date |
| `GET /api/v1/cities/{cityId}/metric-values?metrics=a,b` | Compact `{ metricKey: { neighborhoodId: { raw, normalized } } }` |
| `GET /api/v1/neighborhoods/{id}` | Detail (hierarchy, full geometry, centroid) |
| `GET /api/v1/neighborhoods/{id}/metrics` | All the neighborhood's metrics, with date and source |
| `POST /api/v1/compare` | 2 to 4 `neighborhoodIds` → metrics table |
| `POST /api/v1/match` | Preferences → **explained** ranking (see below) |
| `POST /api/v1/chat/turn` | **Optional (AI phase)**: `{sessionId,text,locale,mapContext}` → `{message, actions[]}`. `message` is composed from **templates** using computed results; `actions[]` = typed map commands (`setCriteria`, `showMatches`, `fitBounds`, `highlight`, `openCard`, `ask`, `setLayer`). Reuses the `/preferences/parse` service (intent, needs, gates); same `ai_*` errors |
| `POST /api/v1/preferences/parse` | **Optional**: free text (fr/en) `{cityId,text,locale}` → validated preferences, reference neighborhood, adjustments, warnings |
| `GET /api/v1/neighborhoods/{id}/isochrone?minutes=&mode=` | Precomputed isochrone + POI count |
| `POST /api/v1/isochrones` | Isochrone for an arbitrary point (cache + quota) |
| `GET /api/v1/trending?period=7d` | Trends (growth, never a popularity ranking; `trends-v1`) |
| `POST /api/v1/events` | Anonymous event (with consent) |
| `GET /api/v1/health` | Health (database, data version, `ai: disabled \| warming \| ready \| unavailable \| unauthorized`) |

`POST /api/v1/match` (shape, details in `../docs/matching.md`):

```jsonc
// request
{
  "cityId": 34,
  "preferences": [
    { "metricKey": "transport_accessibility", "weight": 0.9, "target": "high" },
    { "metricKey": "housing_affordability",   "weight": 1.0, "target": "high" },
    { "metricKey": "noise",                   "weight": 0.6, "target": "low"  }
  ],
  "limit": 20
}
// response
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

The engine returns **contributions per criterion**: the explanation is computed, never invented by a model.

## Accounts, reviews, pins (phases 4a to 4e)

Full specification in `../docs/accounts.md`; this file keeps only the rules that bind the code.
- ASP.NET Core Identity, session cookie; `FakeEmailSender`; demo users (`is_demo`) rejected in production.
- `Review.is_visible` is the **only** column the public reads. `ReviewModeration` (and `PlaceModeration`): **append-only**, one row per decision, written in the same transaction as `is_visible`.
- Pins: a `pending` pin never appears in `places/map`, `places/zone`, trends or anyone else's popularity; submission is **idempotent** (`clientId`); the quota of 5 pins is checked at submission (`409`), without ever deleting the author's local pin; an edit to a published pin is visible immediately, CLEF re-reads it in the background and the administrator can hide it.
- `places/map`: zones = Web Mercator tiles at zoom `min(zoom + 2, 18)` (key `z/x/y`), `leader` = place with maximum popularity (tie: published `community` before OSM, then identifier); `places/zone/{zoneKey}` returns all the places in the zone, sorted the same way, paginated.
- `Place.popularity`: `3 × likes + 4 × visible reviews` (saving does not count), distinct people, verified accounts, **offline** (never in a user request); never enters matching.
- ETL dependencies: approval given on 18 October 2026 for the list in `../docs/dependencies.md`; read each package's license before adding it and record it.
- Pin photo: only one, re-encoded server-side, EXIF stripped, content recognized; **no image library added without the human's approval** (license to validate).

## Matching

Details in `../docs/matching.md`. Mandatory points for the implementation:
- Pure function `ScoreNeighborhoods(preferences, metricValues) -> results`.
- Explicit weights; weights are renormalized (sum = 1) before the calculation.
- Missing values: the criterion is **excluded** for that neighborhood and the remaining weights are renormalized; `coverage` is returned; below a coverage threshold the neighborhood is flagged, not silently hidden.
- Total, deterministic ordering (score tie → ascending `neighborhoodId`).
- The engine knows nothing about AI, the database, or HTTP.

## Optional AI (CLEF-Flash)

CLEF-Flash is a **decision model**: it receives a state (here the user's raw text) and typed questions in natural language, and returns probabilities; it **does not generate text**. Full detail (format, questions, interpretation, execution) in `../docs/clef.md`.

```csharp
public interface IDecisionModel
{
    Task<DecisionAnswers> DecideAsync(DecisionRequest request, CancellationToken cancellationToken);
}
```

The interface is generic; the "preferences" logic lives in three **pure** components: `PreferenceQuestionCatalog`, `PreferenceAnswerInterpreter` (`preferences-v2`) and `NeighborhoodNameMatcher`. Implementations: `FakeDecisionModel` (default in dev and tests, deterministic rules, same response shapes), `ClefLlamaServerDecisionModel` (HTTP to `llama-server`, `/v1/systemone`).

Rules:
- **A model's output is untrusted input.** Validate it (zod on the front end, strict schemas here): keys known in `MetricDefinition`, bounds, no duplicates. Invalid → `ProblemDetails`, never a silent correction.
- The user's text is untrusted (prompt injection): it only enters the model's state, which returns only probabilities on **our** questions; the model triggers no action and writes no sentence. Chat messages come from **templates**.
- **Asynchronous** AI (see `../docs/clef.md`, "Asynchronous mode"): `POST` responds `202` with a `jobId`, `GET /api/v1/ai/jobs/{jobId}` gives the state, `DELETE` cancels; a single worker, bounded in-memory queue (`AI_QUEUE_CAPACITY`, otherwise `503 ai_busy`), chat before moderation; state and result deleted after `AI_JOB_RETENTION_SECONDS`; rate limiting; text never logged in production; warm-up request at startup; `GET /health` reports `ai: disabled | warming | ready | unavailable | unauthorized`.
- `AI_ENABLED=false`: `/preferences/parse` and `/chat/turn` return `503 ai_disabled`; everything else works, **moderation included** (reviews published without a check, reason `ai_unavailable`, see `../docs/accounts.md` §4).
- **Moderation (phase 4b)**: `BackgroundService` background task, one item at a time, `MODERATION_TIMEOUT_SECONDS = 180`, 30 s wait if the queue is empty and 60 s if CLEF is absent; chat goes first. Reviews: publish first, re-moderate afterwards. **Pins: CLEF never publishes or rejects**; it ranks the administrator's queue.
- Local CLEF-Flash is measured, not assumed fast (median 42 s, moderation request about 78 s on the test machine): see `../docs/clef.md` "Measurements". Docker `ai` profile only.

## Isochrones

No self-hosted routing server: we rely on a **hosted service** behind `IIsochroneProvider`. Full strategy (precomputation, cache, quotas, fallback) in `../docs/isochrones.md`.

## Data and ETL

Details in `../docs/data.md`. Mandatory points:
- Every source has a manifest (source, version, download date, license, steps, output); folders `raw/` → `processed/` → `normalized/`.
- Reproducible ETL: same input → same output; no manual modification of a dataset without a versioned transformation.
- No metric without a filled-in `DataSource` (license and attribution included).
- Heavy computations (densities, accessibility, land-use entropy, precomputed isochrones) live in the ETL, never in a user request.

## Privacy and security

- Accounts are optional (see `../docs/accounts.md`): reading never requires a login. Saved places (private favorites) are kept on the device until login, then merged without duplicates.
- Trending: **anonymous** events (type, neighborhood identifier, rounded timestamp, random session identifier that is neither persistent nor tied to a person). Do not store IP addresses. Enable collection only with consent; the Turkish KVKK law applies to users in Turkey, the GDPR to European users.
- Secrets only through environment variables; keep `.env.example` up to date.
- Restricted CORS; rate limiting on `/preferences/parse`, `/isochrones` and `/events`.

## Configuration

```
DATABASE_CONNECTION_STRING
AI_ENABLED            # false by default
AI_PROVIDER           # fake | clef-server (remote CLEF server, see AI_ENDPOINT)
AI_USERNAME / AI_PASSWORD   # HTTP Basic authentication of the CLEF server (optional; secrets: never in the repository or the logs)
AI_ENDPOINT           # URL of the CLEF server; if AI_ENABLED=true and it is empty, the API refuses to start
AI_ENDPOINT
AI_MODEL              # clef-flash
AI_JOB_TIMEOUT_SECONDS      # 180 by default: maximum duration of a task (two passes)
AI_REQUEST_TIMEOUT_SECONDS  # 120 by default: one request to CLEF
AI_QUEUE_CAPACITY           # 3 by default: waiting tasks
AI_JOB_RETENTION_SECONDS    # 600 by default: how long a result is kept
MODERATION_ON_AI_UNAVAILABLE   # publish (default) | hold
MODERATION_TIMEOUT_SECONDS     # 180
ISOCHRONE_PROVIDER    # fake | geoapify | openrouteservice | valhalla
ISOCHRONE_API_KEY
ISOCHRONE_DAILY_BUDGET
```

Environments: `Development` (must stay extremely simple), `Test`, `Production`.

## Observability

Structured logs, `GET /api/v1/health`, latency metrics per endpoint. Log the isochrone API quota consumption.

## Tests

See `../docs/verification.md` for the full strategy.
- **Unit + properties (FsCheck)** on the pure core: normalization, scoring, validation of `UserPreferences`.
- **Integration (Testcontainers + PostGIS)**: spatial queries, constraints, materialized views.
- **Contract**: the API complies with `openapi.v1.json` (schema validation; property tests on the API possible with Schemathesis).
- **AI**: `PreferenceQuestionCatalog`, `PreferenceAnswerInterpreter` and `NeighborhoodNameMatcher` tested with properties; the default suite uses `FakeDecisionModel`. No test in the default suite depends on a local model or an external service; validation against the real server is a separate bench (`../benchmarks/clef/`).
- **Isochrones**: always `FakeIsochroneProvider` in tests.

## Commands

```bash
docker compose up                      # db + api
docker compose --profile etl run --rm etl <source>   # ingestion
dotnet build
dotnet test
dotnet format --verify-no-changes
dotnet run --project src/Etl -- <source>   # ingestion of one source
```

## Definition of "done" (backend)

`dotnet test` and `dotnet format` pass; OpenAPI up to date; migration validated by the human; provenance documented; calculation reproducible; works with `AI_ENABLED=false` and without isochrone keys (`fake`).
