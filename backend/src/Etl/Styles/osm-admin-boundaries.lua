local administrative_boundaries = osm2pgsql.define_relation_table('osm_admin_boundaries', {
    { column = 'admin_level', type = 'int' },
    { column = 'tags', type = 'jsonb' },
    { column = 'geom', type = 'geometry', projection = 4326 },
})

local kept_levels = {
    [4] = true,
    [6] = true,
    [8] = true,
}

function osm2pgsql.process_relation(object)
    if object.tags.boundary ~= 'administrative' then
        return
    end

    local level = tonumber(object.tags.admin_level)
    if level == nil or not kept_levels[level] then
        return
    end

    local geometry = object:as_multipolygon()
    if geometry == nil then
        return
    end

    administrative_boundaries:insert({
        admin_level = level,
        tags = object.tags,
        geom = geometry,
    })
end
