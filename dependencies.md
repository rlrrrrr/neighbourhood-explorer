# Dépendances logicielles

Liste des dépendances utilisées par le projet, avec leur licence. Toute addition
passe par une lecture de la licence et une entrée ici **avant** usage
(`docs/etl-work-order.md`). Une dépendance absente de ce fichier ne doit pas être
ajoutée sans accord de l'humain.

## Backend (NuGet)

| Paquet | Version | Licence | Usage | Décision |
|---|---|---|---|---|
| Npgsql | 9.0.5 | PostgreSQL License (BSD-like) | Accès PostgreSQL/PostGIS (API et ETL) | déjà utilisé par l'API |
| Microsoft.AspNetCore.Mvc.Testing | 9.0.21 | MIT | Tests d'intégration de l'API | déjà utilisé |
| Microsoft.NET.Test.Sdk | 17.12.0 | MIT | Exécution des tests | déjà utilisé |
| xunit | 2.9.2 | Apache-2.0 | Tests | déjà utilisé |
| xunit.runner.visualstudio | 2.8.2 | Apache-2.0 | Adaptateur de tests | déjà utilisé |
| Testcontainers.PostgreSql | 4.16.0 | MIT | Tests d'intégration sur un PostGIS réel (`backend/AGENTS.md` l'impose) | ajouté pour l'ETL, réutilisé par les tests d'API |

Pas de bibliothèque de mocking, conformément à `AGENTS.md`.

Le format des manifestes (`data/sources/*.yaml`) est lu par un lecteur strict
maison (`ManifestDocument`) qui n'accepte que le sous-ensemble utilisé par nos
fichiers et échoue avec le numéro de ligne sinon. YamlDotNet (MIT) serait la
solution standard, mais son ajout demande l'accord de l'humain.

## Outils système de l'image ETL

| Outil | Version (Debian bookworm) | Licence | Usage | Décision |
|---|---|---|---|---|
| osm2pgsql | 1.8.0+ds-1 | GPL-2.0-or-later | Import de l'extrait OSM (`docs/etl-work-order.md`, étape 1) | nommé par l'ordre de travail ; installé dans l'image, non distribué |
| gdal-bin | 3.6.2+dfsg-1+b2 | MIT/X11 | `gdalwarp` (projection/rééchantillonnage du raster WorldPop), `gdalinfo` (métadonnées et statistiques) | ajouté le 5 novembre 2026 pour l'étape 2 ; installé dans l'image, non distribué |
| postgis | 3.3.2+dfsg-1+b1 | GPL-2.0-or-later | `raster2pgsql` (import du raster en tuiles) et extension `postgis_raster` côté serveur | ajouté le 5 novembre 2026 pour l'étape 2 ; installé dans l'image, non distribué |
| postgresql-client | 15+248+deb12u1 (psql 15.19) | PostgreSQL License (BSD-like) | `psql` pour charger la sortie de `raster2pgsql` | ajouté le 5 novembre 2026 pour l'étape 2 ; installé dans l'image, non distribué |

L'image n'est pas publiée : osm2pgsql, GDAL, `raster2pgsql` et `psql` sont
exécutés localement, pas redistribués. GDAL est sous licence MIT/X11 ; PostGIS
sous GPL-2.0-or-later ; le client PostgreSQL sous PostgreSQL License. Le serveur
PostGIS de la base (`postgis/postgis:16-3.4` ou image équivalente) reste la
version 3.4.3 ; le client `raster2pgsql` 3.3.2 produit un dump compatible.

## Frontend (npm)

Voir `frontend/package.json` ; aucune dépendance ajoutée par ce travail.
