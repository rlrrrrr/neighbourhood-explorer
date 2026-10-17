# AGENTS.md : frontend

Complète `../AGENTS.md` (règles communes, tests, style, autonomie). Ici : tout ce qui est propre au frontend. Direction produit et design validée : `../docs/product-direction.md`.

## Responsabilités

Le frontend **affiche et manipule l'état de la vue**. Il ne calcule ni score, ni classement, ni explication, ni agrégat de district : tout cela vient du backend (`../backend/AGENTS.md`). Il ne dépend pas de l'IA pour afficher la carte.

Trois vues de même niveau, reliées par une barre de navigation toujours visible (en bas sur mobile, en haut sur bureau) et par un bouton **« Voir sur la carte »** :

| Vue         | Rôle                                                                                                                         |
| ----------- | ---------------------------------------------------------------------------------------------------------------------------- |
| **Chat**    | Écrire ce qu'on cherche ; critères compris (puces modifiables) ; 5 à 10 résultats en cartes. Alternative aux filtres manuels |
| **Carte**   | Quartiers colorés, filtres manuels, fiche de quartier, isochrones                                                            |
| **Données** | Tableau de classement : une colonne par critère, triable, valeurs brutes avec unité et date                                  |

## Pile

| Domaine              | Choix                                                                                                                    |
| -------------------- | ------------------------------------------------------------------------------------------------------------------------ |
| Outillage            | Vite+ (`vp`) pour tout : dev, vérifications, tests, build                                                                |
| UI                   | React + TypeScript en mode strict                                                                                        |
| Internationalisation | fbtee 5 : traductions inline (`<fbt>`, `fbt()`, `fbs()`), compilées par le plugin Vite ; locales fr (source), en, tr, ar |
| Données serveur      | TanStack Query                                                                                                           |
| Carte                | mapcn (composants React au-dessus de MapLibre GL), fond OpenFreeMap                                                      |
| Graphiques           | TanStack Charts (**pré-alpha, version épinglée, usage isolé** dans `features/charts`)                                    |
| Styles               | Tailwind + shadcn/ui ; variantes avec tailwind-variants                                                                  |
| Contrat              | types générés avec openapi-typescript depuis `../backend/openapi/openapi.v1.json` ; validation à la frontière avec zod   |
| Tests                | Vitest, Testing Library, fast-check                                                                                      |

Routage : non décidé (ajouter une dépendance demande l'accord de l'humain). L'état partagé vit dans l'URL.

## Structure : un dossier par fonctionnalité

Noms de fichiers explicites en **kebab-case**, **aucun barrel file** (pas de `index.ts` qui ré-exporte) : on importe depuis le fichier qui définit.

```
src/
├── app/                      # coque : barre de navigation, fournisseurs, thème, locales (locale-runtime, locale-context, language-switcher), pied de page (site-footer, dia-gradient)
├── features/
│   ├── criteria/             # état des critères partagé, encodage dans l'URL
│   ├── chat-view/            # vue Chat
│   ├── map-view/             # vue Carte (couches, commandes de carte, fiches)
│   ├── data-view/            # vue Données (tableau de classement)
│   ├── neighborhood-detail/
│   ├── compare/
│   ├── selection/            # quartiers enregistrés (localStorage)
│   ├── isochrones/
│   ├── charts/               # seule porte d'entrée vers TanStack Charts
│   └── explore-feed/         # phase ultérieure
├── generated/                # types OpenAPI (jamais édités à la main)
├── translations/             # catalogues fbtee compilés (générés par `mise i18n`, commités, jamais édités à la main)
└── design-tokens/            # jetons de couleur par rôle
```

Pas de `utils`, `helpers`, `common`, `misc`. Du code partagé n'existe que quand plusieurs fonctionnalités l'utilisent réellement, et il se place dans la fonctionnalité la plus proche.

## Contrat backend (résumé)

Source de vérité : `../backend/openapi/openapi.v1.json`. Ne jamais deviner un champ : regénérer les types. Besoin d'un changement de contrat : le noter dans `../docs/contract-requests.md`.

| Endpoint                                                              | Usage côté frontend                                                          |
| --------------------------------------------------------------------- | ---------------------------------------------------------------------------- |
| `GET /api/v1/cities`                                                  | liste des villes                                                             |
| `GET /api/v1/cities/{cityId}/neighborhoods`                           | géométries simplifiées, cacheables                                           |
| `GET /api/v1/metrics`                                                 | définitions, source, date                                                    |
| `GET /api/v1/cities/{cityId}/metric-values?metrics=`                  | valeurs compactes pour la carte et le tableau                                |
| `GET /api/v1/neighborhoods/{id}` et `/metrics`                        | fiche détail                                                                 |
| `POST /api/v1/compare`                                                | 2 à 4 quartiers                                                              |
| `POST /api/v1/match`                                                  | classement **expliqué** : score, couverture, contributions                   |
| `POST /api/v1/preferences/parse`                                      | optionnel : texte libre → préférences proposées                              |
| `POST /api/v1/chat/turn`                                              | optionnel (phase IA) : message → `message` composé par gabarit + `actions[]` |
| `GET /api/v1/neighborhoods/{id}/isochrone`, `POST /api/v1/isochrones` | isochrones (quota)                                                           |
| `GET /api/v1/health`                                                  | santé, dont `ai: disabled \| warming \| ready \| unavailable`                |

Erreurs : `application/problem+json` avec un `code` stable. États à gérer explicitement : `ai_disabled`, `ai_unavailable`, `ai_busy` (503), `ai_timeout` (504), `isochrone_quota_exceeded` (503), `not_a_neighborhood_request` (422), `invalid_text` (422).

## Critères : un seul état pour les trois vues

- Un objet `Criteria` (liste de `{ metricKey, weight, target }`, quartier de référence et ajustements éventuels) est l'unique source de vérité. Les curseurs de la carte, les puces du chat et les colonnes pondérées du tableau lisent et écrivent **le même état**.
- Il est encodé dans l'URL : vue, centre, zoom, couche, sélection, critères. Un lien recharge exactement la même vue. L'encodage et le décodage sont testés par propriété (aller-retour sans perte).
- Un critère rempli par l'IA porte `source: "ai"` : l'interface l'affiche « proposé par l'IA » et le rend modifiable. L'IA propose, l'utilisateur valide.
- Changements immédiats, **sans bouton « Appliquer »** ; debounce sur les curseurs ; pas de requête à chaque `mousemove`.

## Vue Chat

- Un champ de saisie ; le texte brut part tel quel (jamais reformulé côté client). Langues du texte libre : français et anglais ; turc en bêta (avertissement `locale_experimental`).
- Réponse : `message` (déjà composé par gabarit côté backend, affiché tel quel) et `actions[]`. Le frontend **n'écrit jamais de phrase d'explication lui-même**.
- Les `actions[]` sont validées avec zod puis exécutées : `setCriteria`, `showMatches`, `fitBounds`, `highlight`, `openCard`, `ask`, `setLayer`. Une action inconnue est ignorée et journalisée en développement, jamais exécutée au hasard.
- `ask` : question de clarification avec choix rapides (par exemple choisir un quartier précis d'un district, ou résoudre `reference_ambiguous`).
- IA indisponible (`ai_disabled`, `ai_unavailable`, `ai_busy`, `ai_timeout`) : on masque ou désactive la saisie du chat et on propose le panneau de critères manuel ; la recherche par nom de quartier continue de marcher. Message hors sujet (`not_a_neighborhood_request`) : un message neutre invitant à décrire le quartier recherché.
- Référence de niveau district : afficher la puce « moyenne du district X » (`reference.profile = "district_weighted_mean"`) avec l'option de choisir un mahalle précis.

## Vue Carte

- mapcn au-dessus de MapLibre GL. Les besoins que mapcn ne couvre pas (couches de polygones, `feature-state`, regroupement de points, inclinaison) passent par l'instance MapLibre : **vérifier dans la documentation de la version installée** qu'on y a accès avant de coder.
- **Fond de carte** : OpenFreeMap, style _Positron_ par défaut (fond calme sous les quartiers colorés), _Liberty_ en option, _Dark_ pour le thème sombre ; URL dans `VITE_BASEMAP_STYLE_URL` (jamais en dur). L'attribution (MapLibre, OpenMapTiles, OpenStreetMap) reste visible.
- Trois niveaux de zoom : districts, mahalles, lieux. La couleur des quartiers est pilotée par `feature-state` : changer de couche ne recrée pas les couches.
- Rampe de scores à **une seule teinte** (clarté croissante), valeur manquante en gris avec motif distinct ; la couleur n'est jamais le seul porteur d'information (libellé, valeur, rang numéroté).
- Une valeur héritée du district (`quality_flag = inherited`) est étiquetée « valeur du district ».
- Une **vue liste** synchronisée avec la carte est toujours disponible (accessibilité, petits écrans).
- Budgets : changement de couche < 100 ms sans requête réseau ; premier affichage < 2 s.
- Mobile : feuille basse à trois crans, zones tactiles ≥ 44 px, rien ne dépend du survol.

## Vue Données

- Tableau : une ligne par quartier, une colonne par critère actif, tri par colonne, valeurs brutes avec unité, date d'observation et source accessible.
- La colonne composite s'appelle « Score selon tes critères » et **n'existe que si des poids sont réglés** ; il n'y a jamais de score global par défaut (règle de `../AGENTS.md`).
- Chaque ligne a un bouton **« Voir sur la carte »**.
- Précision d'affichage maîtrisée : `8,2 cafés/km²`, pas `8,237491`.

## Graphiques

TanStack Charts est pré-alpha : version épinglée, tout l'usage dans `features/charts`, API vérifiée dans la documentation de **la version installée** avant chaque usage. Les graphiques expliquent ce que montre la carte (contributions, comparaison) ; ils ne remplacent pas la carte.

## Design : jetons par rôle

Les composants ne contiennent **aucune couleur en dur** : ils utilisent des jetons nommés par **rôle** (`--color-text`, `--color-surface`, `--color-action`, `--color-accent`…). **Onze thèmes** dérivés de combinaisons précises de `colors.json` (Sanzo Wada) sur fonds blanc/noir — clairs : Moda (défaut), Güneş, Lale, Gül, Sabah, Kandil, Çarşı, Yeditepe ; sombres : Fener, Sis, Gece — chacun un jeu complet de jetons, catalogués avec leurs contrastes dans `../docs/product-direction.md` §3. Le sélecteur (`app/theme-switcher.tsx`) affiche nom + pastilles d'aperçu via `--preview-bg` / `--preview-action` / `--preview-highlight` (blocs `[data-theme-preview]` dans `themes.css`, valeurs hors thème actif). Ids de thème : `ALL_THEME_NAMES` dans `app/theme.ts` — toute nouvelle paire `[data-theme]` / `[data-theme-preview]` doit y être ajoutée (avec son nom dans `THEME_DISPLAY_NAMES` du même fichier). Les valeurs hex viennent du fichier `colors.json` de Sanzo Wada ; ne pas en inventer. Contraste minimal : 4,5 pour le texte courant, 3 pour les grands éléments. Une couleur d'accent claire (ex. Sulpher Yellow, Seashell Pink) ne sert **jamais** de texte : uniquement en aplat avec libellé sombre.

**Profondeur** : les cartes et panneaux portent les jetons d'ombre en couches `--elevated` / `--elevated-hover` (utilités `shadow-elevated`, `shadow-elevated-hover`) plutôt que des bordures ; les commandes (champs, chips, légende) gardent `--border` à 3:1. Le grain de film (tuile SVG 200 px dans `index.css`, `--grain-image`) est **quasi invisible** (`--grain-opacity: 0,04`) : un réglage unique, jamais une texture marquée — sans `mix-blend-mode` (annulé sur blanc/noir purs) et jamais sur un conteneur défilant (le `::after` défilerait avec le contenu).

**Images** : passer par `components/ui/feathered-image.tsx` (`FeatheredImage`) plutôt qu'un `<img>` nu. Il combine un skeleton à sheen pendant le chargement, un reveal `blur-[6px]` → net en 500 ms, et une lisière organique (masque SVG turbulence + flou, `--feathered-image-mask` dans `index.css`) qui fait fondre le bord de l'image dans le fond — CSS pur, jamais de WebGL ; transitions neutralisées sous `prefers-reduced-motion`.

Sur les thèmes clairs, revérifier à chaque palette que le premier cran de la rampe reste discernable sur le fond de Positron (les crans actuels vont de 1,23 à 1,9 sur blanc : contour sur les polygones obligatoire ; à retester sur la vraie carte).

## Styles et variantes (tailwind-variants)

- Composants à variantes avec `tv` ; `slots` pour les composants à plusieurs parties ; types de props avec `VariantProps`.
- Résolution des conflits de classes : selon la documentation consultée, la build par défaut s'en charge sans installer `tailwind-merge`. **À confirmer pour la version épinglée** avant de s'en remettre à ce comportement ; ne pas ajouter `tailwind-merge` sans l'accord de l'humain.
- Pas de concaténation manuelle de chaînes de classes pour exprimer des variantes.

## Interface et textes

- **Langues** : français (langue source — les textes source sont en français dans le code), anglais, turc, arabe. Commutateur dans l'en-tête (`app/language-switcher.tsx`) ; `lang` et `dir` (RTL pour l'arabe) gérés par `app/locale-context.tsx` ; choix mémorisé dans `localStorage` (clé `locale`) et restauré au chargement (`LocaleContext.preload()` dans `main.tsx`, avant le premier rendu).
- **Aucun texte en dur** : tout passe par fbtee — `<fbt desc="…">` dans le JSX, `fbt()` pour les textes construits en fonction, `fbs()` pour les attributs (`placeholder`, `aria-label`). `desc` est le contexte du traducteur et doit être écrit.
- **Noms propres non traduits** (données brutes, pas de catalogue) : noms de thèmes (`THEME_DISPLAY_NAMES` dans `app/theme.ts`), noms de quartiers et de districts, noms de fonds de carte (Positron, Liberty…), crédits photo, nom de marque de la source de données.
- **Workflow de traduction** : après tout ajout ou changement de texte, relancer `mise i18n` (collect + compile). Les fichiers `translations/*.json` sont édités à la main (le champ `status: "new"` des nouvelles chaînes est retiré une fois traduit). `source_strings.json`, `.enum_manifest.json` et `src/translations/*.json` sont **générés et commités** (clone fraîche buildable sans étape supplémentaire) — jamais édités à la main ; c'est un écart assumé à la recommandation fbtee (qui suggère de les ignorer) pour garder le build reproductible hors CI.
- Chaque valeur affichée porte son unité, sa date et sa source quand l'utilisateur le demande.
- Les indicateurs de risque (séisme, inondation) ne sont jamais présentés comme des garanties.
- Attribution OpenStreetMap, du fond de carte et des fournisseurs toujours visible.
- Accessibilité : navigation clavier des quartiers, libellés, focus visible, couleur jamais seule.

## Style TypeScript

- Mode strict ; pas de `any` ; pas d'assertions de type en chaîne ; pas d'assertion non nulle (`!`).
- Unions discriminées pour les états (chargement, erreur, donnée) et pour les commandes de carte.
- Décoder les données externes **une seule fois à la frontière** (zod) ; les fonctions internes reçoivent des types nommés.
- Un seul objet en paramètre quand une fonction a plusieurs arguments ; noms descriptifs sans abréviation.
- N'exporter que ce qui a un usage externe ; pas de barrel files ; supprimer le code inutilisé.
- Pas de commentaires par défaut (voir `../AGENTS.md`).
- Ne pas propager (`spread`) un objet de transport dans un objet de sortie : énumérer les champs.

## Tests

- Vitest + Testing Library ; **pas de bibliothèque de mocking**. MSW : décision ouverte (`../docs/product.md`) ; en attendant, tester contre les vraies implémentations ou une implémentation de remplacement réelle de l'API.
- Tests de propriétés (fast-check) : aller-retour de l'encodage des critères dans l'URL ; validation des `actions[]` (jamais d'exécution d'une action invalide) ; tri du tableau (stable, total).
- Locales : `src/app/locale-context.test.tsx` charge les vrais catalogues compilés (en, tr, ar) et vérifie le rendu de textes visibles ; `App.test.tsx` vérifie le rendu sans provider (langue source, déterministe). Aucun mock du runtime fbtee.
- Déboguer avec `vp test run <chemin/complet.test.ts>` ; suite complète une seule fois avant le résumé final.

## Commandes

```bash
vp install
vp dev
vp check                      # format + lint (+ types si `typeCheck` est activé dans `lint.options`)
vp check --fix
vp test run <chemin/complet.test.ts>
vp test
vp build

# Traductions (depuis la racine : mise i18n ; depuis frontend/) :
vp exec fbtee collect --src src --include-default-strings=false   # collecte les chaînes source
vp exec fbtee prepare-translations --source-strings source_strings.json --output-dir translations --locales en tr ar  # prépare les nouvelles chaînes (status: new)
# éditer translations/{en,tr,ar}.json (traduire les chaînes new, retirer leur status)
vp exec fbtee translate --source-strings source_strings.json --translations "translations/*.json" --output-dir src/translations  # compile les catalogues runtime
```

## Définition de « terminé » (frontend)

1. `vp check` et `vp test` passent, une seule fois avant le résumé final.
2. Types régénérés si le contrat a changé ; aucun fichier de `generated/` édité à la main.
3. Aucune couleur en dur, aucun texte en dur (fbtee), aucun barrel file ; chaînes traduites dans `translations/*.json` et `mise i18n` relancé.
4. La vue fonctionne **sans IA** (chat masqué, curseurs et recherche par nom opérationnels).
5. Budgets de performance respectés ; attribution visible.
