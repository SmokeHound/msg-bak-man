PRAGMA foreign_keys=ON;

CREATE TABLE IF NOT EXISTS conversation (
  conversation_id INTEGER PRIMARY KEY,
  conversation_key TEXT NOT NULL UNIQUE,
  display_name TEXT NULL,
  created_utc INTEGER NOT NULL
);

ALTER TABLE message ADD COLUMN conversation_id INTEGER NULL;

CREATE INDEX IF NOT EXISTS ix_message_conversation
  ON message(conversation_id, date_ms);

-- Best-effort FK (SQLite doesn't support adding FK via ALTER TABLE easily).

CREATE TABLE IF NOT EXISTS recipient (
  recipient_id INTEGER PRIMARY KEY,
  address_norm TEXT NOT NULL UNIQUE,
  address_raw_last TEXT NULL
);

CREATE TABLE IF NOT EXISTS conversation_recipient (
  conversation_id INTEGER NOT NULL,
  recipient_id INTEGER NOT NULL,
  PRIMARY KEY(conversation_id, recipient_id),
  FOREIGN KEY(conversation_id) REFERENCES conversation(conversation_id) ON DELETE CASCADE,
  FOREIGN KEY(recipient_id) REFERENCES recipient(recipient_id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_conversation_recipient_recipient
  ON conversation_recipient(recipient_id, conversation_id);
