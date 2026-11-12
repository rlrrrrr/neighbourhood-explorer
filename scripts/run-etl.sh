#!/usr/bin/env bash
# Ingestion ETL sur la base sélectionnée par scripts/database-target.sh.
# Monte les dossiers de data/raw pointés par des liens symboliques hors du dépôt.
set -euo pipefail

root_dir=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)

if [[ $# -lt 1 ]]; then
    echo "usage: $0 <source> [arguments de l'ETL]" >&2
    exit 2
fi

# shellcheck source=database-target.sh
. "$root_dir/scripts/database-target.sh"

cd "$root_dir"

compose_files=(-f docker-compose.yml)
if [[ "$DATABASE_TARGET_MODE" == remote ]]; then
    export REMOTE_DATABASE_CONNECTION_STRING="$DATABASE_CONNECTION_STRING_ACTIVE"
    compose_files+=(-f docker-compose.remote.yml)
fi

mounts=()
if [[ -L "$root_dir/data/raw" ]]; then
    mounts+=(-v "$(readlink -f "$root_dir/data/raw"):/data/raw")
else
    for entry in "$root_dir"/data/raw/*; do
        if [[ -L "$entry" ]]; then
            mounts+=(-v "$(readlink -f "$entry"):/data/raw/$(basename "$entry")")
        fi
    done
fi

docker compose "${compose_files[@]}" --profile etl run --rm "${mounts[@]}" etl "$@"
