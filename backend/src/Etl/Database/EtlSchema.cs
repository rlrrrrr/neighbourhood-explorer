using Npgsql;

namespace NeighbourhoodExplorer.Etl.Database;

public static class EtlSchema
{
    private const string Ddl = """
        CREATE EXTENSION IF NOT EXISTS postgis;
        CREATE EXTENSION IF NOT EXISTS postgis_raster;

        CREATE TABLE IF NOT EXISTS etl_run (
            id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            source_id text NOT NULL,
            started_at timestamptz NOT NULL,
            finished_at timestamptz,
            status text NOT NULL CHECK (status IN ('running', 'succeeded', 'failed')),
            tool_version text NOT NULL,
            input_sha256 text,
            row_counts jsonb,
            error_message text
        );

        CREATE TABLE IF NOT EXISTS admin_area (
            id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            parent_id bigint REFERENCES admin_area(id),
            level text NOT NULL CHECK (level IN ('province', 'district', 'neighborhood')),
            country_code char(2) NOT NULL,
            name text NOT NULL,
            name_normalized text NOT NULL,
            official_name text,
            official_code text,
            geometry geometry(MultiPolygon, 4326) NOT NULL,
            display_geometry geometry(MultiPolygon, 4326),
            centroid geometry(Point, 4326) NOT NULL,
            valid_from date,
            valid_to date,
            source_id text NOT NULL,
            source_feature_id text NOT NULL,
            CONSTRAINT admin_area_level_parent CHECK (
                (level = 'province' AND parent_id IS NULL)
                OR (level = 'district' AND parent_id IS NOT NULL)
                OR (level = 'neighborhood' AND parent_id IS NOT NULL)),
            CONSTRAINT admin_area_geometry_valid CHECK (ST_IsValid(geometry)),
            CONSTRAINT admin_area_source_feature_unique UNIQUE (source_id, source_feature_id)
        );

        ALTER TABLE admin_area ADD COLUMN IF NOT EXISTS display_geometry geometry(MultiPolygon, 4326);

        CREATE INDEX IF NOT EXISTS admin_area_geometry_gist ON admin_area USING GIST (geometry);
        CREATE INDEX IF NOT EXISTS admin_area_parent_id ON admin_area (parent_id);
        CREATE INDEX IF NOT EXISTS admin_area_level ON admin_area (level);
        CREATE INDEX IF NOT EXISTS admin_area_name_normalized ON admin_area (name_normalized);

        CREATE TABLE IF NOT EXISTS staging_ibb_buildings_2017 (
            id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            source_id text NOT NULL,
            mahalle_uavt bigint NOT NULL,
            district_name text NOT NULL,
            neighborhood_name text NOT NULL,
            district_normalized text NOT NULL,
            neighborhood_normalized text NOT NULL,
            buildings_before_1980 integer NOT NULL,
            buildings_1980_2000 integer NOT NULL,
            buildings_after_2000 integer NOT NULL,
            buildings_1_4_floors integer NOT NULL,
            buildings_5_9_floors integer NOT NULL,
            buildings_9_19_floors integer NOT NULL,
            admin_area_id bigint REFERENCES admin_area(id) ON DELETE CASCADE,
            match_method text,
            loaded_at timestamptz NOT NULL,
            CONSTRAINT staging_ibb_buildings_2017_source_uavt UNIQUE (source_id, mahalle_uavt),
            CONSTRAINT staging_ibb_buildings_2017_match_method
                CHECK (match_method IN ('name', 'alias', 'spatial'))
        );

        ALTER TABLE staging_ibb_buildings_2017
            DROP CONSTRAINT IF EXISTS staging_ibb_buildings_2017_match_method_check;
        ALTER TABLE staging_ibb_buildings_2017
            DROP CONSTRAINT IF EXISTS staging_ibb_buildings_2017_match_method;
        ALTER TABLE staging_ibb_buildings_2017
            ADD CONSTRAINT staging_ibb_buildings_2017_match_method
            CHECK (match_method IN ('name', 'alias', 'spatial'));

        CREATE INDEX IF NOT EXISTS staging_ibb_buildings_2017_admin_area
            ON staging_ibb_buildings_2017 (admin_area_id);

        CREATE TABLE IF NOT EXISTS tuik_adnks_totals (
            source_id text NOT NULL,
            reference_year integer NOT NULL,
            level text NOT NULL CHECK (level IN ('province', 'district')),
            code text NOT NULL,
            name text NOT NULL,
            name_normalized text NOT NULL,
            population integer NOT NULL CHECK (population > 0),
            retrieved_at timestamptz NOT NULL,
            PRIMARY KEY (source_id, reference_year, code)
        );

        CREATE TABLE IF NOT EXISTS worldpop_neighborhood_sum (
            source_id text NOT NULL,
            grid_year integer NOT NULL,
            admin_area_id bigint NOT NULL REFERENCES admin_area(id) ON DELETE CASCADE,
            population_raw double precision NOT NULL CHECK (population_raw >= 0),
            area_km2 double precision NOT NULL CHECK (area_km2 > 0),
            population_estimated double precision,
            computed_at timestamptz NOT NULL,
            PRIMARY KEY (source_id, grid_year, admin_area_id)
        );

        CREATE TABLE IF NOT EXISTS worldpop_district_coefficient (
            source_id text NOT NULL,
            grid_year integer NOT NULL,
            reference_year integer NOT NULL,
            district_id bigint NOT NULL REFERENCES admin_area(id) ON DELETE CASCADE,
            worldpop_sum double precision NOT NULL CHECK (worldpop_sum > 0),
            adnks_total integer NOT NULL CHECK (adnks_total > 0),
            coefficient double precision NOT NULL CHECK (coefficient > 0),
            flagged boolean NOT NULL,
            computed_at timestamptz NOT NULL,
            PRIMARY KEY (source_id, grid_year, reference_year, district_id)
        );

        CREATE TABLE IF NOT EXISTS data_source (
            id text PRIMARY KEY,
            name text NOT NULL,
            organization text NOT NULL,
            url text NOT NULL,
            license text NOT NULL,
            attribution_text text NOT NULL,
            methodology text NOT NULL,
            retrieved_at date NOT NULL,
            source_version text NOT NULL
        );

        CREATE TABLE IF NOT EXISTS metric_definition (
            id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            key text NOT NULL UNIQUE,
            category text NOT NULL,
            unit text NOT NULL,
            native_level text NOT NULL CHECK (native_level IN ('province', 'district', 'neighborhood')),
            polarity text NOT NULL CHECK (polarity IN ('higher_is_better', 'lower_is_better', 'neutral')),
            normalization_method text NOT NULL,
            normalization_version text NOT NULL,
            display_precision integer NOT NULL CHECK (display_precision >= 0),
            source_id text NOT NULL REFERENCES data_source(id)
        );

        CREATE TABLE IF NOT EXISTS metric_translation (
            metric_id bigint NOT NULL REFERENCES metric_definition(id),
            locale text NOT NULL CHECK (locale IN ('fr', 'en', 'tr')),
            name text NOT NULL,
            description text NOT NULL,
            subject text NOT NULL,
            PRIMARY KEY (metric_id, locale)
        );

        CREATE TABLE IF NOT EXISTS metric_value (
            admin_area_id bigint NOT NULL REFERENCES admin_area(id) ON DELETE CASCADE,
            metric_id bigint NOT NULL REFERENCES metric_definition(id),
            observed_at date NOT NULL,
            reference_period text NOT NULL,
            raw_value double precision NOT NULL,
            normalized_value double precision NOT NULL CHECK (normalized_value BETWEEN 0 AND 1),
            quality_flag text NOT NULL CHECK (quality_flag IN ('ok', 'estimated', 'stale', 'low_coverage', 'inherited')),
            computed_at timestamptz NOT NULL,
            PRIMARY KEY (admin_area_id, metric_id, observed_at)
        );

        CREATE INDEX IF NOT EXISTS metric_value_metric_observed
            ON metric_value (metric_id, observed_at);

        CREATE INDEX IF NOT EXISTS metric_value_metric_area_observed
            ON metric_value (metric_id, admin_area_id, observed_at DESC);

        ALTER TABLE staging_ibb_buildings_2017
            DROP CONSTRAINT IF EXISTS staging_ibb_buildings_2017_admin_area_id_fkey;
        ALTER TABLE staging_ibb_buildings_2017
            ADD CONSTRAINT staging_ibb_buildings_2017_admin_area_id_fkey
            FOREIGN KEY (admin_area_id) REFERENCES admin_area(id) ON DELETE CASCADE;

        ALTER TABLE worldpop_neighborhood_sum
            DROP CONSTRAINT IF EXISTS worldpop_neighborhood_sum_admin_area_id_fkey;
        ALTER TABLE worldpop_neighborhood_sum
            ADD CONSTRAINT worldpop_neighborhood_sum_admin_area_id_fkey
            FOREIGN KEY (admin_area_id) REFERENCES admin_area(id) ON DELETE CASCADE;

        ALTER TABLE worldpop_district_coefficient
            DROP CONSTRAINT IF EXISTS worldpop_district_coefficient_district_id_fkey;
        ALTER TABLE worldpop_district_coefficient
            ADD CONSTRAINT worldpop_district_coefficient_district_id_fkey
            FOREIGN KEY (district_id) REFERENCES admin_area(id) ON DELETE CASCADE;

        ALTER TABLE metric_value
            DROP CONSTRAINT IF EXISTS metric_value_admin_area_id_fkey;
        ALTER TABLE metric_value
            ADD CONSTRAINT metric_value_admin_area_id_fkey
            FOREIGN KEY (admin_area_id) REFERENCES admin_area(id) ON DELETE CASCADE;

        CREATE INDEX IF NOT EXISTS worldpop_neighborhood_sum_admin_area
            ON worldpop_neighborhood_sum (admin_area_id);
        """;

    public static async Task EnsureAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(Ddl);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
