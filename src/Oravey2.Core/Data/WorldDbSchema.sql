CREATE TABLE IF NOT EXISTS world_meta (
    key   TEXT PRIMARY KEY,
    value TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS continent (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    name        TEXT    NOT NULL,
    description TEXT,
    grid_width  INTEGER NOT NULL,
    grid_height INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS region (
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
    continent_id INTEGER NOT NULL REFERENCES continent(id),
    name         TEXT    NOT NULL,
    grid_x       INTEGER NOT NULL,
    grid_y       INTEGER NOT NULL,
    biome        TEXT    NOT NULL DEFAULT 'wasteland',
    base_height  REAL    NOT NULL DEFAULT 0,
    description  TEXT,
    UNIQUE(continent_id, grid_x, grid_y)
);

CREATE TABLE IF NOT EXISTS chunk (
    id        INTEGER PRIMARY KEY AUTOINCREMENT,
    region_id INTEGER NOT NULL REFERENCES region(id),
    grid_x    INTEGER NOT NULL,
    grid_y    INTEGER NOT NULL,
    mode      INTEGER NOT NULL DEFAULT 0,
    layer     INTEGER NOT NULL DEFAULT 2,
    tile_data BLOB    NOT NULL,
    UNIQUE(region_id, grid_x, grid_y)
);

CREATE TABLE IF NOT EXISTS chunk_layer (
    id        INTEGER PRIMARY KEY AUTOINCREMENT,
    chunk_id  INTEGER NOT NULL REFERENCES chunk(id),
    layer     INTEGER NOT NULL,
    tile_data BLOB    NOT NULL,
    UNIQUE(chunk_id, layer)
);

CREATE TABLE IF NOT EXISTS entity_spawn (
    id             INTEGER PRIMARY KEY AUTOINCREMENT,
    chunk_id       INTEGER NOT NULL REFERENCES chunk(id),
    prefab_id      TEXT    NOT NULL,
    local_x        REAL    NOT NULL,
    local_z        REAL    NOT NULL,
    rotation_y     REAL    NOT NULL DEFAULT 0,
    faction        TEXT,
    level          INTEGER,
    dialogue_id    TEXT,
    loot_table     TEXT,
    persistent     INTEGER NOT NULL DEFAULT 0,
    condition_flag TEXT
);

CREATE TABLE IF NOT EXISTS linear_feature (
    id         INTEGER PRIMARY KEY AUTOINCREMENT,
    region_id  INTEGER NOT NULL REFERENCES region(id),
    type       INTEGER NOT NULL,
    style      TEXT    NOT NULL,
    width      REAL    NOT NULL,
    nodes_json TEXT    NOT NULL
);

CREATE TABLE IF NOT EXISTS poi (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    region_id   INTEGER NOT NULL REFERENCES region(id),
    name        TEXT    NOT NULL,
    type        TEXT    NOT NULL,
    grid_x      INTEGER NOT NULL,
    grid_y      INTEGER NOT NULL,
    description TEXT,
    icon        TEXT
);

CREATE TABLE IF NOT EXISTS interior (
    id        INTEGER PRIMARY KEY AUTOINCREMENT,
    poi_id    INTEGER REFERENCES poi(id),
    name      TEXT    NOT NULL,
    width     INTEGER NOT NULL,
    height    INTEGER NOT NULL,
    tile_data BLOB    NOT NULL
);

CREATE TABLE IF NOT EXISTS terrain_modifier (
    id        INTEGER PRIMARY KEY AUTOINCREMENT,
    chunk_id  INTEGER NOT NULL REFERENCES chunk(id),
    type      TEXT    NOT NULL,
    data_json TEXT    NOT NULL
);

CREATE TABLE IF NOT EXISTS sync_log (
    id        INTEGER PRIMARY KEY AUTOINCREMENT,
    timestamp TEXT    NOT NULL DEFAULT (datetime('now')),
    action    TEXT    NOT NULL,
    target    TEXT    NOT NULL,
    details   TEXT
);

CREATE TABLE IF NOT EXISTS location_description (
    location_id    INTEGER PRIMARY KEY,
    location_type  TEXT    NOT NULL,
    tagline        TEXT    NOT NULL,
    summary        TEXT,
    dossier        TEXT,
    summary_utc    TEXT,
    dossier_utc    TEXT,
    llm_model      TEXT
);

CREATE INDEX IF NOT EXISTS idx_region_continent ON region(continent_id);
CREATE INDEX IF NOT EXISTS idx_chunk_region     ON chunk(region_id);
CREATE INDEX IF NOT EXISTS idx_entity_chunk     ON entity_spawn(chunk_id);
CREATE INDEX IF NOT EXISTS idx_lf_region        ON linear_feature(region_id);
CREATE INDEX IF NOT EXISTS idx_poi_region       ON poi(region_id);
CREATE INDEX IF NOT EXISTS idx_tm_chunk         ON terrain_modifier(chunk_id);
CREATE INDEX IF NOT EXISTS idx_locdesc_type    ON location_description(location_type);
