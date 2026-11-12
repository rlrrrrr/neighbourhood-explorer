local kept_buildings = {
    apartments = true,
    house = true,
    residential = true,
    commercial = true,
    retail = true,
    office = true,
    industrial = true,
    school = true,
    university = true,
    hospital = true,
    dormitory = true,
    hotel = true,
}

local kept_leisure = {
    park = true,
    garden = true,
    playground = true,
    pitch = true,
    recreation_ground = true,
}

local walkable_highways = {
    residential = true,
    living_street = true,
    pedestrian = true,
    footway = true,
    service = true,
    unclassified = true,
    tertiary = true,
    tertiary_link = true,
    secondary = true,
    secondary_link = true,
    primary = true,
    primary_link = true,
    path = true,
    steps = true,
    cycleway = true,
    track = true,
    road = true,
}

local pois = osm2pgsql.define_table({
    name = 'osm_urban_poi',
    ids = { type = 'any', id_column = 'osm_id', type_column = 'osm_type' },
    columns = {
        { column = 'name', type = 'text' },
        { column = 'tags', type = 'jsonb' },
        { column = 'geom', type = 'geometry', projection = 4326, not_null = true },
    },
})

local areas = osm2pgsql.define_table({
    name = 'osm_urban_area',
    ids = { type = 'any', id_column = 'osm_id', type_column = 'osm_type' },
    columns = {
        { column = 'name', type = 'text' },
        { column = 'tags', type = 'jsonb' },
        { column = 'geom', type = 'geometry', projection = 4326, not_null = true },
    },
})

local highways = osm2pgsql.define_way_table('osm_urban_highway', {
    { column = 'highway', type = 'text', not_null = true },
    { column = 'geom', type = 'linestring', projection = 4326, not_null = true },
})

local function is_poi(tags)
    return tags.amenity ~= nil
        or tags.shop ~= nil
        or tags.office ~= nil
        or tags.tourism ~= nil
        or tags.healthcare ~= nil
end

local function is_area(tags)
    return tags.landuse ~= nil
        or tags.natural ~= nil
        or kept_leisure[tags.leisure]
        or kept_buildings[tags.building]
end

local function insert_poi(object, geometry)
    if not is_poi(object.tags) or geometry == nil then
        return
    end

    pois:insert({
        name = object.tags.name,
        tags = object.tags,
        geom = geometry,
    })
end

local function insert_area(object, geometry)
    if not is_area(object.tags) or geometry == nil then
        return
    end

    areas:insert({
        name = object.tags.name,
        tags = object.tags,
        geom = geometry,
    })
end

function osm2pgsql.process_node(object)
    insert_poi(object, object:as_point())
end

function osm2pgsql.process_way(object)
    if walkable_highways[object.tags.highway] then
        local geometry = object:as_linestring()
        if geometry ~= nil then
            highways:insert({
                highway = object.tags.highway,
                geom = geometry,
            })
        end
    end

    insert_poi(object, object:as_polygon())
    insert_area(object, object:as_polygon())
end

function osm2pgsql.process_relation(object)
    if object.tags.type ~= 'multipolygon' and object.tags.type ~= 'boundary' then
        return
    end

    insert_area(object, object:as_multipolygon())
    insert_poi(object, object:as_multipolygon())
end
