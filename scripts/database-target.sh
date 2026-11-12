# Résout la base de données active : Docker local ou PostgreSQL distant.
# S'utilise en le sourçant ; DATABASE_TARGET_MODE vaut alors « local » ou « remote »
# et DATABASE_CONNECTION_STRING_ACTIVE contient la chaîne Npgsql correspondante.
#
# DATABASE_TARGET (dans .env) : local | remote | auto.
# « auto » vérifie que la base distante répond ET propose PostGIS + PostGIS raster,
# puis retombe sur le service db de docker-compose.yml sinon.

database_target_root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)

if [[ -f "$database_target_root/.env" ]]; then
    set -a
    # shellcheck disable=SC1091
    . "$database_target_root/.env"
    set +a
fi

_database_target_local_connection() {
    printf 'Host=db;Port=5432;Database=%s;Username=%s;Password=%s' \
        "${POSTGRES_DB:-explorer}" "${POSTGRES_USER:-explorer}" "${POSTGRES_PASSWORD:-}"
}

_database_target_remote_connection() {
    printf 'Host=%s;Port=%s;Database=%s;Username=%s;Password=%s;SSL Mode=Prefer;Timeout=15' \
        "$REMOTE_DB_HOST" "${REMOTE_DB_PORT:-5432}" "$REMOTE_DB_NAME" "$REMOTE_DB_USER" "$REMOTE_DB_PASSWORD"
}

_database_target_remote_is_ready() {
    if [[ -z ${REMOTE_DB_HOST:-} || -z ${REMOTE_DB_NAME:-} || -z ${REMOTE_DB_USER:-} || -z ${REMOTE_DB_PASSWORD:-} ]]; then
        return 1
    fi

    local probe_result
    probe_result=$(PGPASSWORD="$REMOTE_DB_PASSWORD" timeout 30 docker run --rm \
        -e PGPASSWORD -e PGCONNECT_TIMEOUT=8 -e PGSSLMODE=prefer \
        "${DATABASE_PROBE_IMAGE:-postgis/postgis:16-3.4}" \
        psql -h "$REMOTE_DB_HOST" -p "${REMOTE_DB_PORT:-5432}" \
        -U "$REMOTE_DB_USER" -d "$REMOTE_DB_NAME" -Atc \
        "SELECT 1 WHERE (SELECT count(*) FROM pg_available_extensions WHERE name IN ('postgis', 'postgis_raster')) = 2" \
        2>/dev/null) || return 1

    [[ "$probe_result" == "1" ]]
}

DATABASE_TARGET_MODE=
DATABASE_CONNECTION_STRING_ACTIVE=

case "${DATABASE_TARGET:-local}" in
    local)
        DATABASE_TARGET_MODE=local
        DATABASE_CONNECTION_STRING_ACTIVE=$(_database_target_local_connection)
        ;;
    remote)
        if ! _database_target_remote_is_ready; then
            echo "Base distante indisponible ou sans PostGIS : ${REMOTE_DB_HOST:-<non configurée>}. Rien n'a été lancé." >&2
            exit 1
        fi
        DATABASE_TARGET_MODE=remote
        DATABASE_CONNECTION_STRING_ACTIVE=$(_database_target_remote_connection)
        ;;
    auto)
        if _database_target_remote_is_ready; then
            DATABASE_TARGET_MODE=remote
            DATABASE_CONNECTION_STRING_ACTIVE=$(_database_target_remote_connection)
            echo "Base de données : distante (${REMOTE_DB_HOST})." >&2
        else
            DATABASE_TARGET_MODE=local
            DATABASE_CONNECTION_STRING_ACTIVE=$(_database_target_local_connection)
            echo "Base de données : Docker (repli ; la base distante ne répond pas ou n'a pas PostGIS)." >&2
        fi
        ;;
    *)
        echo "DATABASE_TARGET invalide : « ${DATABASE_TARGET} » (attendu : local, remote, auto)." >&2
        exit 2
        ;;
esac

if [[ "${BASH_SOURCE[0]}" == "$0" ]]; then
    printf 'mode: %s\n' "$DATABASE_TARGET_MODE"
    if [[ "$DATABASE_TARGET_MODE" == remote ]]; then
        printf 'hôte: %s:%s/%s\n' "$REMOTE_DB_HOST" "${REMOTE_DB_PORT:-5432}" "$REMOTE_DB_NAME"
    else
        printf 'hôte: db:5432/%s\n' "${POSTGRES_DB:-explorer}"
    fi
fi
