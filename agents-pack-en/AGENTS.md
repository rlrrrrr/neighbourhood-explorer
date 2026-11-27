# AGENTS.md

Web application for exploring the neighborhoods (*mahalle*) of Turkey, starting with Istanbul.

## Project context

**Student** project, non-commercial, not intended to be maintained long term. Consequences:

- **Technical rigor stays intact**: code quality, tests, security, data reproducibility, provenance, explainability. "It's a student project" is never an excuse to cut corners.
- **Only the commercial side is relaxed**: no monetization, no SLA, no scaling up, no purchase of a commercial license, no optimization of operating costs. Do not build a feature for commercial use.
- **Legal obligations remain** even without a commercial goal: OpenStreetMap and provider attribution, compliance with API terms of use and data licenses, privacy (KVKK/GDPR).
- Reduced scope: no microservices, no over-engineering, no anticipating maintenance over several years. A young dependency (e.g. pre-alpha) is acceptable if its version is pinned and its use is isolated.

**The map is the product.** We are not building a dashboard: the map is the lens that gives meaning to the data, and the metrics and charts serve to explain what you see on it. The application has three views (Chat, Map, Data) that share a single criteria state; every result projects onto the map (« Voir sur la carte » ("View on the map")). See `docs/product-direction.md`.

## Where to read what

| You are working on… | Read first |
|---|---|
| The frontend (`frontend/`) | `frontend/AGENTS.md` (contains a summary of the backend contract) |
| The backend, the ETL, the database (`backend/`) | `backend/AGENTS.md` |
| Vision, UX, phases, open decisions | `docs/product.md` |
| Validated direction: three views, map, palette, role of CLEF | `docs/product-direction.md` |
| Matching score | `docs/matching.md` |
| CLEF-Flash: format, role in matching, execution | `docs/clef.md` |
| Docker, services, CI, hosting | `docs/infrastructure.md` |
| Isochrones ("neighborhood within 10 min") | `docs/isochrones.md` |
| Data, sources, licenses, metrics | `docs/data.md` |
| Istanbul open data sources by metric | `docs/data-istanbul.md` |
| ETL work order (steps, checks, what not to do) | `docs/etl-work-order.md` |
| Brief for the track A agent (accounts, favorites, stacked PRs) and its fixtures | `docs/agent-brief-accounts.md`, `docs/fixtures/accounts/` |
| Accounts, reviews, place suggestions, favorites, audience trends | `docs/accounts.md` |
| Test strategy, invariants, formal verification | `docs/verification.md` |

If this file and a sub-file contradict each other, the sub-file closest to the code being modified wins. Report the contradiction.

## Non-negotiable rules

**Always**
- Make the map the destination of all data: every result (chat, ranking, detail card) has a « Voir sur la carte » ("View on the map") entry point; in the Map view, it stays visible and dominant.
- Make the application work **without AI and without a GPU**. AI is an optional layer, never in the critical path (map, metrics, filters, comparison, navigation).
- Document the provenance of every piece of data: source, methodology, license, date, transformation, normalization (see `docs/data.md`).
- Make calculations reproducible and run outside the user path (ETL → PostGIS → API).
- Separate raw value from normalized value, and keep the observation date.
- Keep the domain generic: never hard-code Istanbul in business logic.
- Make the score explainable: explicit weights, contributions per criterion.

**Never**
- Depend on an AI model, a paid API or an external service to display the map.
- Display a universal "best neighborhood" or an arbitrary global score.
- Display artificial precision (`8.2 cafés/km²`, not `8.237491`).
- Present risk indicators (earthquake, flood…) as safety guarantees.
- Commit a secret (API key, token, password). Use environment variables and `.env.example`.
- Build interactions between users: messaging, subscriptions, comments, user-published content. Allowed: consumption forms (*Explorer* feed, computed rankings, trends derived from the data, local selection, sharing by link) and the chat with CLEF, which is not messaging. Accounts, favorites, reviews, place suggestions and audience trends: later phase, decisions in `docs/accounts.md`; messaging, subscriptions and comments remain excluded.
- Create 50 metrics before having good UX: MVP = 11 metrics (`docs/data.md`).

**Trade-off**: when technical sophistication conflicts with user experience, choose experience and simplicity.

## Administrative hierarchy (important)

In Turkey: `province (il)` > `district (ilçe)` > `neighborhood (mahalle)`.
Kadıköy is an **ilçe**; Moda is a **mahalle**. The data model handles the levels explicitly (see `backend/AGENTS.md`). Examples in the docs that mention "Kadıköy neighborhood" are shortcuts.

## Repository structure

```
/
├── AGENTS.md
├── docker-compose.yml          # db + api; "etl" profile (frontend run with vp locally; the CLEF model is hosted on another instance)
├── .env.example
├── infra/
│   └── clef/Dockerfile         # llama-server + CLEF-Flash (optional)
├── data/                       # raw/ processed/ normalized/ (ETL), outside Git except manifests
├── docs/                       # product and technical documentation
├── benchmarks/                 # separate benchmarks (e.g. benchmarks/clef/)
├── frontend/                   # React + TypeScript + Vite+  (see frontend/AGENTS.md)
└── backend/                    # ASP.NET Core + PostGIS      (see backend/AGENTS.md)
    ├── openapi/                # versioned API contract (source of truth)
    ├── src/
    └── tests/
```

## Commands

Adjust to the real scaffold; update this block as soon as a command changes. **Use Vite+ (`vp`) for the whole frontend**; do not call `pnpm`/`npm`/`vite`/`vitest` directly for these tasks.

```bash
# Infrastructure (PostgreSQL + PostGIS + API)
docker compose up
python benchmarks/clef/smoke-test.py     # minimal test of /v1/systemone on the remote CLEF server (CLEF_URL = its address)

# Frontend (from frontend/)
vp install
vp dev                                # dev server
vp check                              # format + lint (+ types if `typeCheck` is enabled in `lint.options` of vite.config.ts)
vp check --fix                        # fixes what can be fixed
vp test run <full/path.test.ts>   # a single file, a single run (see "Tests")
vp test                               # the whole suite
vp build                              # production build

# Backend (from backend/)
dotnet build
dotnet test
dotnet format --verify-no-changes
```

## Performance budgets (testable)

- Switching metric on the map: **< 100 ms** (no network request if the data is already loaded).
- First map render: **< 2 s** on a standard connection.
- No network request on every `mousemove`; debounce on sliders.
- Read API response (metrics, neighborhoods): **< 300 ms** p95 locally.

## Definition of "done"

A task is done when:
1. the final checks pass **once, before the final summary** (not after every change): `vp test` and `vp check` (frontend); `dotnet test` and `dotnet format --verify-no-changes` (backend);
2. the tests cover the added behavior, and every fixed bug has a regression test;
3. no data or secret was added without provenance / without `.env.example`;
4. the API contract (`backend/openapi/`) is up to date if the API changed, and the frontend has regenerated its types;
5. the relevant documentation (`docs/`, `AGENTS.md`) is updated;
6. the feature works without AI and without a GPU.

## Autonomy

**You can do on your own**: create files in an existing feature, add tests, refactor within a feature, fix bugs, update the docs.

**Ask before**:
- adding a dependency (npm or NuGet);
- creating or modifying a database migration;
- changing the API contract in an incompatible way;
- adding a data source (check its license);
- wiring up an external service with a quota (isochrones, AI);
- changing the matching thresholds or formulas (`docs/matching.md`);
- deleting data;
- introducing formal verification (see `docs/verification.md`).

## Replies and feedback to the user

### Open questions: "what's next?", "anything else?"

When the user ends a task with an open prompt ("next steps ?", "anything else ?", "something else ?", "anything remaining ?", "what's next ?", or in French « la suite ? », « autre chose ? », « il reste quoi ? »), it is a genuine invitation to propose worthwhile **improvements, refactorings, tests, docs or edge cases**. Group **all of them in a single message**, never one by one.

When they ask "any questions?" or "need anything from me?": gather all outstanding unknowns and ask them in **a single grouped message**. If a question can be answered by exploring the code, do that instead of asking it. If there is nothing: "Nothing else — all good."

### Section format

When you propose work, ask for feedback or present changes:

- Use **indexed** items so the user can reply precisely: `1`, `2`, `3` within a single section; `A1`, `A2`, `B1` if there are several sections.
- Put completed or proposed changes under a clearly named **`SHOWCASE`** section.
- Add a **`FEEDBACK NEEDED`** section only when a real question or decision is needed. All questions go there, indexed, separate from `SHOWCASE`. Omit the section if there are no questions.

## Investigation and boundaries

- Prefer **domain-local code** over generic catch-alls. Create shared code only when several real callers have stable semantics or an invariant in common, and place it at the narrowest existing domain boundary.
- **Unfamiliar library**: identify the installed version and check the exact API in the official documentation for **that version** before implementing. Especially for TanStack Charts (pre-alpha), mapcn, Vite+.
- **Generated artifacts** (schemas, migrations, generated clients, snapshots): derived output. Modify the source of truth, regenerate, inspect the diff, validate the result. Never hand-edit a generated file to mask a problem in the source.
- **Database**: investigate **read-only** first: schema, indexes, constraints, bounded representative data, query plans, before touching database code or migrations.
- **Browser/runtime behavior**: investigate on the running application. Correlate reproduction steps with console errors, network requests, DOM state and computed styles, instead of relying on reading the code alone.
- **Bugs**: reproduce before fixing, state a falsifiable root-cause hypothesis, add a regression test.
- **Security review** of every change: authorization, input validation, injection, secret exposure, sensitive data leakage, dangerous process execution, dependency risk. Never display sensitive values during investigation.
- **Compact validation evidence**: run the narrowest useful command, keep full logs in a file if needed, report new failures separately from already-known failures.
- **Structured observability** at the boundary of the use case / unit of work: typed fields, correlation identifiers, bounded payloads, masking of sensitive data. No duplicate audit events; no adapter-level logging that blurs responsibility.
- **External data**: decode **only once at the boundary**; internal APIs handle named, validated types.
- Avoid chained assertions, broad `object`/`unknown` contracts, and assertions on raw response JSON.
- Do not spread (`spread`) database rows or caller-controlled transport data into output objects: enumerate the fields that cross the boundary.

## Tests (common rules)

- **Avoid mocks: test the real implementation.** No mocking library. For external boundaries that are non-deterministic or quota-limited (AI model, isochrone provider) only, use a **real stand-in implementation of the interface** (`FakeDecisionModel`, `FakeIsochroneProvider`) that returns deterministic data, never a behavior mock.
- **Debug by running a single file**, not the whole suite. Use the full path and `vp test run <file>` (a single run, no watch mode) for fast feedback; add `.only` to isolate a failing test (remove it before finishing).
- Run the full suite and the checks **only once before the final summary**.
- See `docs/verification.md` for invariants and property tests.

## Common code style

- **No comments by default.** A comment is added only if the *why* is not obvious.
- Unacceptable comments: those that repeat what the code does, commented-out code, obvious comments, comments in place of good naming. If a comment is needed to explain *what* the code does, **refactor**: the code must document itself.
- **Descriptive** variable names, no abbreviations.
- Remove unused code, avoid repetition.
- Avoid multiple arguments: prefer a **single object** as a parameter.
- Export only what is intended for external use.
- TypeScript-specific rules are in `frontend/AGENTS.md`, C# ones in `backend/AGENTS.md`.

## Conventions

- Code and identifiers: **English**. Documentation: French.
- Explicit file names: the name says what the file contains. No `utils`, `helpers`, `common`, `misc`. Casing: **kebab-case on the frontend**, C# convention (PascalCase) on the backend.
- Commits: [Conventional Commits](https://www.conventionalcommits.org/) (`feat:`, `fix:`, `docs:`, `test:`, `refactor:`).
- Simple > clever. Reproducible data > manual data. Explicit methodology > mysterious score.
