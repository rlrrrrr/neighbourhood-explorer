# AGENTS.md : frontend

Extends `../AGENTS.md` (shared rules, tests, style, autonomy). This file covers everything specific to the frontend. Validated product and design direction: `../docs/product-direction.md`.

## Responsibilities

The frontend **displays and manipulates view state**. It computes no score, no ranking, no explanation, no district aggregate: all of that comes from the backend (`../backend/AGENTS.md`). It does not depend on AI to display the map.

Three views at the same level, linked by an always-visible navigation bar (at the bottom on mobile, at the top on desktop) and by a **« Voir sur la carte »** ("View on the map") button:

| View | Role |
|---|---|
| **Chat** | Write what you are looking for; understood criteria (editable chips); 5 to 10 results as cards. Alternative to manual filters |
| **Map** | Colored neighborhoods, manual filters, neighborhood card, isochrones |
| **Data** | Ranking table: one column per criterion, sortable, raw values with unit and date |

## Stack

| Area | Choice |
|---|---|
| Tooling | Vite+ (`vp`) for everything: dev, checks, tests, build |
| UI | React + TypeScript in strict mode |
| Server data | TanStack Query |
| Map | mapcn (React components on top of MapLibre GL), OpenFreeMap basemap |
| Charts | TanStack Charts (**pre-alpha, pinned version, isolated usage** in `features/charts`) |
| Styles | Tailwind + shadcn/ui; variants with tailwind-variants |
| Contract | types generated with openapi-typescript from `../backend/openapi/openapi.v1.json`; validation at the boundary with zod |
| Tests | Vitest, Testing Library, fast-check |

Routing: undecided (adding a dependency requires the human's approval). Shared state lives in the URL.

## Structure: one folder per feature

Explicit file names in **kebab-case**, **no barrel files** (no `index.ts` that re-exports): import from the file that defines.

```
src/
├── app/                      # shell: navigation bar, providers, theme
├── features/
│   ├── criteria/             # shared criteria state, URL encoding
│   ├── chat-view/            # Chat view
│   ├── map-view/             # Map view (layers, map controls, cards)
│   ├── data-view/            # Data view (ranking table)
│   ├── neighborhood-detail/
│   ├── compare/
│   ├── selection/            # saved neighborhoods (localStorage)
│   ├── isochrones/
│   ├── charts/               # only entry point to TanStack Charts
│   └── explore-feed/         # later phase
├── generated/                # OpenAPI types (never edited by hand)
└── design-tokens/            # color tokens by role
```

No `utils`, `helpers`, `common`, `misc`. Shared code exists only when several features actually use it, and it goes in the closest feature.

## Backend contract (summary)

Source of truth: `../backend/openapi/openapi.v1.json`. Never guess a field: regenerate the types. If you need a contract change, note it in `../docs/contract-requests.md`.

| Endpoint | Frontend usage |
|---|---|
| `GET /api/v1/cities` | list of cities |
| `GET /api/v1/cities/{cityId}/neighborhoods` | simplified geometries, cacheable |
| `GET /api/v1/metrics` | definitions, source, date |
| `GET /api/v1/cities/{cityId}/metric-values?metrics=` | compact values for the map and the table |
| `GET /api/v1/neighborhoods/{id}` and `/metrics` | detail card |
| `POST /api/v1/compare` | 2 to 4 neighborhoods |
| `POST /api/v1/match` | **explained** ranking: score, coverage, contributions |
| `POST /api/v1/preferences/parse` | optional: free text → `202` + `jobId` (**asynchronous**) |
| `POST /api/v1/chat/turn` | optional (AI phase): message → `202` + `jobId`; the result carries `message` (template) + `actions[]` |
| `GET /api/v1/ai/jobs/{jobId}` | job state (`queued`, `running`, `done`, `failed`, `cancelled`), step, queue position, result |
| `DELETE /api/v1/ai/jobs/{jobId}` | cancel |
| `GET /api/v1/neighborhoods/{id}/isochrone`, `POST /api/v1/isochrones` | isochrones (quota) |
| `GET /api/v1/health` | health, including `ai: disabled \| warming \| ready \| unavailable` |

Errors: `application/problem+json` with a stable `code`. States to handle explicitly: `ai_disabled`, `ai_unavailable`, `ai_busy` (503), `ai_timeout` (failure state of a job), `job_not_found` (404), `isochrone_quota_exceeded` (503), `not_a_neighborhood_request` (422), `invalid_text` (422).

## Criteria: a single state for the three views

- A `Criteria` object (list of `{ metricKey, weight, target }`, reference neighborhood and any adjustments) is the single source of truth. The map sliders, the chat chips and the weighted table columns read and write **the same state**.
- It is encoded in the URL: view, center, zoom, layer, selection, criteria. A link reloads exactly the same view. Encoding and decoding are property-tested (lossless round trip).
- A criterion filled in by the AI carries `source: "ai"`: the interface shows it as « mis par l'IA » ("set by the AI") and makes it editable; applying it is automatic and can be undone (see Chat view).
- Changes are immediate, **with no « Appliquer » ("Apply") button**; debounce on sliders; no request on every `mousemove`.

## Chat view

- One input field; the raw text is sent as is (never rephrased on the client). Free-text languages: French and English; Turkish in beta (warning `locale_experimental`). Warning `unsupported_criterion` (`topic: "price"`): show a short, discreet message « Le prix n'est pas encore pris en compte » ("Price is not taken into account yet") under the chips, without blocking or hiding the applied criteria; the text comes from the frontend's language resources, never from a free-form sentence from the server.
- **The AI is asynchronous and live** (decided on 31 October; see `../docs/clef.md`, "Asynchronous mode"): after the `202`, open an `EventSource` on `/ai/jobs/{jobId}/events`; if the connection fails, fall back to `GET /ai/jobs/{jobId}` every 2 s (4 s after 30 s). Generate a `clientRequestId` per submission (no duplicate on a double click). Keep `jobId` in `sessionStorage` (with `try/catch`) to resume after a reload; close the stream when the component disappears.
- **Automatic application, no prior confirmation**: understood criteria (`source: "ai"`) are applied as soon as they arrive (`partial` events) and the map and the ranking update; the chips appear progressively. A message « Critères mis à jour par l'IA » ("Criteria updated by the AI") offers **« Annuler »** ("Undo") (return to the previous state). A criterion the person has modified since sending is **never** overwritten. While waiting: live state (« je lis ta demande… » ("I'm reading your request…")), queue position, a **« Annuler »** ("Cancel") button for the search; the map and the sliders stay usable.
- Response (job result): `message` (already composed from a template on the backend, shown as is) and `actions[]`. The frontend **never writes an explanation sentence itself**.
- The `actions[]` are validated with zod and then executed: `setCriteria`, `showMatches`, `fitBounds`, `highlight`, `openCard`, `ask`, `setLayer`. An unknown action is ignored and logged in development, never executed arbitrarily.
- `ask`: clarification question with quick choices (for example choosing a specific neighborhood in a district, or resolving `reference_ambiguous`).
- AI unavailable (`ai_disabled`, `ai_unavailable`, `ai_busy`, `ai_timeout`): hide or disable the chat input and offer the manual criteria panel; search by neighborhood name keeps working. Off-topic message (`not_a_neighborhood_request`): a neutral message inviting the person to describe the neighborhood they are looking for.
- District-level reference: show the "district X average" chip (`reference.profile = "district_weighted_mean"`) with the option to choose a specific mahalle.

## Map view

- mapcn on top of MapLibre GL. Needs that mapcn does not cover (polygon layers, `feature-state`, point clustering, tilt) go through the MapLibre instance: **check in the documentation of the installed version** that it is accessible before coding.
- **Basemap**: OpenFreeMap, *Positron* style by default (a calm background under the colored neighborhoods), *Liberty* as an option, *Dark* for the dark theme; URL in `VITE_BASEMAP_STYLE_URL` (never hard-coded). The attribution (MapLibre, OpenMapTiles, OpenStreetMap) stays visible.
- Three zoom levels: districts, mahalles, places. Neighborhood color is driven by `feature-state`: switching layer does not recreate layers.
- Score ramp with a **single hue** (increasing lightness), missing value in gray with a distinct pattern; color is never the only carrier of information (label, value, numbered rank).
- A value inherited from the district (`quality_flag = inherited`) is labeled « valeur du district » ("district value").
- A **list view** synchronized with the map is always available (accessibility, small screens).
- Budgets: layer switch < 100 ms with no network request; first display < 2 s.
- Mobile: bottom sheet with three snap points, touch targets ≥ 44 px, nothing depends on hover.
- **Exploration ground** (`../docs/accounts.md` §10, `../docs/product-direction.md` §4.8): three levels (tooltip, side or bottom-sheet **preview**, **card** on a dedicated page). The map can be read without leaving it; writing a review or correcting a place is done on the card. Selection in the URL (`?sel=place:123`); only one right side panel at a time.
- **Preview**: carries **« J'aime »** ("Like") and **« Enregistrer »** ("Save"), « Ouvrir la fiche » ("Open the card") and « Y aller » ("Go there"). Reviews and corrections on the card.
- **Add a pin, local first**: a single floating button → placement mode → click reads longitude and latitude → non-blocking floating window (name, category, **one** photo, note); no long press (reserved for the isochrone). **Creation is never blocked**: the pin is written to IndexedDB (native API, no dependency without the human's approval), drawn immediately with the « en cours d'étude » ("under review") marker, then sent in the background with a `clientId` (idempotent send, resumes after a connection loss). The marker follows the pin's state (`accounts.md` §5); a server rejection deletes nothing. The server validates the area and the quota of 5; the duplicate warning (30 m) is shown before sending and does not block.
- **Density**: call `GET /places/map?bbox&zoom` at the end of each pan (debounce, cancel the previous request, no request on every `mousemove`); draw only the `leader` of each zone with its `count`. A click on a zone opens the preview in zone mode (`GET /places/zone/{zoneKey}`, paginated list); each row leads to `/lieux/:id` even if the place is not drawn. The author's pins are always drawn. Zooming in and out must stay smooth: no layer recreation, update a GeoJSON source. Check in the documentation of the installed mapcn version how to read a click and access the MapLibre instance before coding.

## Data view

- Table: one row per neighborhood, one column per active criterion, sort by column, raw values with unit, observation date and accessible source.
- The composite column is called « Score selon tes critères » ("Score based on your criteria") and **exists only if weights are set**; there is never a default global score (rule in `../AGENTS.md`).
- Each row has a **« Voir sur la carte »** ("View on the map") button.
- Controlled display precision: `8,2 cafés/km²`, not `8,237491`.

## Charts

TanStack Charts is pre-alpha: pinned version, all usage in `features/charts`, API checked in the documentation of **the installed version** before each use. Charts explain what the map shows (contributions, comparison); they do not replace the map.

## Design: tokens by role

Components contain **no hard-coded color**: they use tokens named by **role** (`--color-text`, `--color-surface`, `--color-action`, `--color-accent`…). Two themes: "Bosphore" (light, default) and "Nuit" (dark, optional), detailed with their contrasts in `../docs/product-direction.md` §3. Hex values come from Sanzo Wada's `colors.json` file; do not invent any. Minimum contrast: 4.5 for body text, 3 for large elements. The accent orange is never used as text on a white background.

On the light theme, the first step of the ramp almost blends into the Positron background: thin border on the neighborhoods and the « pas de donnée » ("no data") gray (to be checked on the real map).

## Styles and variants (tailwind-variants)

- Components with variants use `tv`; `slots` for multi-part components; prop types with `VariantProps`.
- Class conflict resolution: according to the documentation consulted, the default build handles it without installing `tailwind-merge`. **To be confirmed for the pinned version** before relying on this behavior; do not add `tailwind-merge` without the human's approval.
- No manual concatenation of class strings to express variants.

## Interface and texts

- Interface language: open decision (`../docs/product.md`). No hard-coded text in components: everything goes through a translation catalog (tool to be chosen with the human).
- Each displayed value carries its unit, its date and its source when the user asks for it.
- Risk indicators (earthquake, flood) are never presented as guarantees.
- OpenStreetMap, basemap and provider attribution always visible.
- Accessibility: keyboard navigation of neighborhoods, labels, visible focus, color never alone.

## Accounts, reviews, pins (later phase)

Decisions in `../docs/accounts.md` (points to confirm in §13). Rules already settled:
- The map, the chat and the table work without an account; « Enregistrer » ("Save") writes locally and then syncs after login.
- Never `dangerouslySetInnerHTML`: user content is displayed escaped.
- Creating never blocks: a pin or an edit is saved locally first; the states (waiting for login, not sent, under review, published, rejected, hidden) are displayed explicitly.
- The client decides neither validation nor popularity; it displays what the API returns.
- The errors `401`, `403`, `409`, `422`, `429` are handled explicitly.

## TypeScript style

- Strict mode; no `any`; no chained type assertions; no non-null assertion (`!`).
- Discriminated unions for states (loading, error, data) and for map commands.
- Decode external data **only once at the boundary** (zod); internal functions receive named types.
- A single object as parameter when a function has several arguments; descriptive names without abbreviations.
- Export only what has external use; no barrel files; remove unused code.
- No comments by default (see `../AGENTS.md`).
- Do not spread a transport object into an output object: enumerate the fields.

## Tests

- Vitest + Testing Library; **no mocking library**. MSW: open decision (`../docs/product.md`); in the meantime, test against real implementations or a real replacement implementation of the API.
- Property tests (fast-check): round trip of the criteria encoding in the URL; validation of `actions[]` (never execute an invalid action); table sorting (stable, total).
- Debug with `vp test run <full/path.test.ts>`; run the full suite only once before the final summary.

## Commands

```bash
vp install
vp dev
vp check                      # format + lint (+ types if `typeCheck` is enabled in `lint.options`)
vp check --fix
vp test run <full/path.test.ts>
vp test
vp build
```

## Definition of "done" (frontend)

1. `vp check` and `vp test` pass, only once before the final summary.
2. Types regenerated if the contract changed; no file in `generated/` edited by hand.
3. No hard-coded color, no hard-coded text, no barrel file.
4. The view works **without AI** (chat hidden, sliders and search by name operational).
5. Performance budgets met; attribution visible.
