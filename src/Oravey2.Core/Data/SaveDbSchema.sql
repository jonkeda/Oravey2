CREATE TABLE IF NOT EXISTS save_meta (
    key   TEXT PRIMARY KEY,
    value TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS party (
    id        INTEGER PRIMARY KEY AUTOINCREMENT,
    data_json TEXT    NOT NULL
);

CREATE TABLE IF NOT EXISTS chunk_state (
    id                INTEGER PRIMARY KEY AUTOINCREMENT,
    region_id         INTEGER NOT NULL,
    grid_x            INTEGER NOT NULL,
    grid_y            INTEGER NOT NULL,
    tile_overrides    BLOB,
    modified_entities TEXT,
    UNIQUE(region_id, grid_x, grid_y)
);

CREATE TABLE IF NOT EXISTS fog_of_war (
    id        INTEGER PRIMARY KEY AUTOINCREMENT,
    region_id INTEGER NOT NULL,
    grid_x    INTEGER NOT NULL,
    grid_y    INTEGER NOT NULL,
    revealed  INTEGER NOT NULL DEFAULT 0,
    UNIQUE(region_id, grid_x, grid_y)
);

CREATE TABLE IF NOT EXISTS discovered_poi (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    poi_id        INTEGER NOT NULL UNIQUE,
    discovered_at TEXT    NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE IF NOT EXISTS fast_travel_unlock (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    poi_id      INTEGER NOT NULL UNIQUE,
    unlocked_at TEXT    NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE IF NOT EXISTS map_marker (
    id         INTEGER PRIMARY KEY AUTOINCREMENT,
    region_id  INTEGER NOT NULL,
    grid_x     INTEGER NOT NULL,
    grid_y     INTEGER NOT NULL,
    label      TEXT    NOT NULL,
    icon       TEXT,
    created_at TEXT    NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE IF NOT EXISTS quest_state (
    id         INTEGER PRIMARY KEY AUTOINCREMENT,
    quest_id   TEXT    NOT NULL UNIQUE,
    stage      INTEGER NOT NULL DEFAULT 0,
    data_json  TEXT,
    updated_at TEXT    NOT NULL DEFAULT (datetime('now'))
);

CREATE INDEX IF NOT EXISTS idx_cs_region   ON chunk_state(region_id, grid_x, grid_y);
CREATE INDEX IF NOT EXISTS idx_fow_region  ON fog_of_war(region_id, grid_x, grid_y);
CREATE INDEX IF NOT EXISTS idx_mm_region   ON map_marker(region_id);
