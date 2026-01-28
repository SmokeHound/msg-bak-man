PRAGMA foreign_keys=ON;

CREATE TABLE IF NOT EXISTS schema_migrations (
  version INTEGER PRIMARY KEY,
  applied_utc INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS sources (
  source_id INTEGER PRIMARY KEY,
  path TEXT NOT NULL,
  imported_utc INTEGER NOT NULL,
  file_size INTEGER NULL,
  file_sha256 TEXT NULL
);

CREATE TABLE IF NOT EXISTS message (
  message_id INTEGER PRIMARY KEY,
  transport TEXT NOT NULL,
  box INTEGER NOT NULL,
  date_ms INTEGER NOT NULL,
  date_sent_ms INTEGER NULL,
  read INTEGER NULL,
  seen INTEGER NULL,
  source_id INTEGER NOT NULL,
  fingerprint_version INTEGER NOT NULL,
  fingerprint TEXT NOT NULL,
  FOREIGN KEY(source_id) REFERENCES sources(source_id) ON DELETE RESTRICT
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_message_fingerprint
  ON message(transport, fingerprint_version, fingerprint);

CREATE INDEX IF NOT EXISTS ix_message_date
  ON message(date_ms);

CREATE TABLE IF NOT EXISTS sms (
  message_id INTEGER PRIMARY KEY,
  address_raw TEXT NULL,
  address_norm TEXT NULL,
  protocol INTEGER NULL,
  subject TEXT NULL,
  body TEXT NOT NULL,
  service_center TEXT NULL,
  read INTEGER NULL,
  status INTEGER NULL,
  locked INTEGER NULL,
  FOREIGN KEY(message_id) REFERENCES message(message_id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_sms_address_norm
  ON sms(address_norm);

CREATE TABLE IF NOT EXISTS mms (
  message_id INTEGER PRIMARY KEY,
  m_id TEXT NULL,
  ct_t TEXT NULL,
  sub TEXT NULL,
  text_only INTEGER NULL,
  locked INTEGER NULL,
  FOREIGN KEY(message_id) REFERENCES message(message_id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS mms_addr (
  mms_addr_id INTEGER PRIMARY KEY,
  message_id INTEGER NOT NULL,
  type INTEGER NULL,
  address_raw TEXT NULL,
  address_norm TEXT NULL,
  charset INTEGER NULL,
  FOREIGN KEY(message_id) REFERENCES message(message_id) ON DELETE CASCADE,
  UNIQUE(message_id, type, address_norm)
);

CREATE INDEX IF NOT EXISTS ix_mms_addr_address_norm
  ON mms_addr(address_norm);

CREATE TABLE IF NOT EXISTS media_blob (
  sha256 TEXT PRIMARY KEY,
  size_bytes INTEGER NOT NULL,
  mime_type TEXT NULL,
  extension TEXT NULL,
  rel_path TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS mms_part (
  mms_part_id INTEGER PRIMARY KEY,
  message_id INTEGER NOT NULL,
  seq INTEGER NULL,
  content_type TEXT NULL,
  name TEXT NULL,
  chset TEXT NULL,
  cd TEXT NULL,
  fn TEXT NULL,
  cid TEXT NULL,
  cl TEXT NULL,
  text TEXT NULL,
  data_sha256 TEXT NULL,
  data_size INTEGER NULL,
  part_fingerprint TEXT NOT NULL,
  FOREIGN KEY(message_id) REFERENCES message(message_id) ON DELETE CASCADE,
  FOREIGN KEY(data_sha256) REFERENCES media_blob(sha256) ON DELETE RESTRICT,
  UNIQUE(message_id, part_fingerprint)
);

CREATE INDEX IF NOT EXISTS ix_mms_part_data_sha256
  ON mms_part(data_sha256);