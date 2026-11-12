#!/usr/bin/env bash
# Lance db + api sur la base sélectionnée par scripts/database-target.sh.
set -euo pipefail

root_dir=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
# shellcheck source=database-target.sh
. "$root_dir/scripts/database-target.sh"

cd "$root_dir"

if [[ "$DATABASE_TARGET_MODE" == remote ]]; then
    export REMOTE_DATABASE_CONNECTION_STRING="$DATABASE_CONNECTION_STRING_ACTIVE"
    docker compose -f docker-compose.yml -f docker-compose.remote.yml up -d "$@"
else
    docker compose up -d "$@"
fi
