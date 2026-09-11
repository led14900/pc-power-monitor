namespace PcPowerMonitor.Core.Storage;

/// <summary>
/// Raw DDL for the SQLite database. No logic lives here — only SQL strings keyed by
/// schema version. <see cref="DatabaseMigrator"/> owns the sequencing.
/// </summary>
public static class DatabaseSchema
{
    /// <summary>Schema version 1 — the full initial layout. Runs inside one transaction.</summary>
    public const string V1 = @"
CREATE TABLE IF NOT EXISTS SensorSamples (
  id            INTEGER PRIMARY KEY AUTOINCREMENT,
  ts_utc_ms     INTEGER NOT NULL,
  wall_w        REAL    NOT NULL,
  cpu_w         REAL,
  gpu_w         REAL,
  dc_w          REAL,
  kwh_delta     REAL    NOT NULL,
  dt_seconds    REAL    NOT NULL,
  is_gap        INTEGER NOT NULL DEFAULT 0,
  quality       INTEGER NOT NULL,
  cpu_temp      REAL,
  gpu_temp      REAL,
  cpu_load      REAL,
  gpu_load      REAL,
  ram_load      REAL
);
CREATE INDEX IF NOT EXISTS idx_samples_ts ON SensorSamples(ts_utc_ms);

CREATE TABLE IF NOT EXISTS HourlyRollup (
  hour_local     TEXT PRIMARY KEY,
  kwh            REAL NOT NULL,
  avg_w          REAL,
  max_w          REAL,
  uptime_seconds REAL NOT NULL,
  sample_count   INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS DailyRollup (
  date_local     TEXT PRIMARY KEY,
  kwh            REAL NOT NULL,
  avg_w          REAL,
  max_w          REAL,
  uptime_seconds REAL NOT NULL,
  cost_vnd       REAL NOT NULL DEFAULT 0,
  unit_price_vnd REAL NOT NULL DEFAULT 0,
  vat_rate       REAL NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS MonthlyRollup (
  month_local    TEXT PRIMARY KEY,
  kwh            REAL NOT NULL,
  cost_vnd       REAL NOT NULL DEFAULT 0,
  uptime_seconds REAL NOT NULL,
  max_w          REAL
);

CREATE TABLE IF NOT EXISTS Meta (
  key   TEXT PRIMARY KEY,
  value TEXT NOT NULL
);
";
}
