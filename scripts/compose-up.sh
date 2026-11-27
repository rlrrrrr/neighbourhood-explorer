#!/usr/bin/env bash
# Lance les services sur la base sélectionnée par scripts/database-target.sh.
# En mode distant, seul le service api démarre (--no-deps) : db n'est pas utilisé.
set -euo pipefail

root_dir=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
# shellcheck source=database-target.sh
. "$root_dir/scripts/database-target.sh"

cd "$root_dir"

if [[ "$DATABASE_TARGET_MODE" == remote ]]; then
    export REMOTE_DATABASE_CONNECTION_STRING="$DATABASE_CONNECTION_STRING_ACTIVE"
    local_services=("$@")
    if [[ ${#local_services[@]} -eq 0 ]]; then
        local_services=(api)
    fi
    docker compose -f docker-compose.yml -f docker-compose.remote.yml up -d --no-deps "${local_services[@]}"
else
    docker compose up -d "$@"
fi
