# Starter pack for track A (accounts and favorites)

English translation of the French originals (French stays the source of truth; dated 4 November 2026).

Start here, in this order:

1. `docs/agent-brief-accounts.md`: hand this file verbatim to your coding agent. It is the work order (nine features, stacked PRs).
2. `AGENTS.md`, `backend/AGENTS.md`, `frontend/AGENTS.md`: non-negotiable rules and code style.
3. `docs/accounts.md`: product and API decisions (the brief names the sections to read).
4. `docs/dependencies.md`: approved and forbidden dependencies. Ask the human before adding any other.
5. `docs/fixtures/accounts/`: test and demo data (users, favorites, neighborhoods, auth edge cases, email texts).

Copy `AGENTS.md`, `backend/AGENTS.md`, `frontend/AGENTS.md` and `docs/` into the repository root (the repository has no `docs/` folder yet).

Some files link to documents that are not in this pack (`docs/product.md`, `docs/clef.md`, `docs/matching.md`...). They belong to other tracks; ignore them for track A.

Never commit secrets. The French UI strings (« Enregistrer », « Favoris ») are source strings of the app (fbtee); English and Turkish are required in each PR.
