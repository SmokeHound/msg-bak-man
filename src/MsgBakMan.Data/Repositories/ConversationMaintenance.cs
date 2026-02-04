using Dapper;
using Microsoft.Data.Sqlite;

namespace MsgBakMan.Data.Repositories;

public sealed class ConversationMaintenance
{
    private readonly SqliteConnection _conn;

    public ConversationMaintenance(SqliteConnection conn)
    {
        _conn = conn;
    }

    public void BackfillConversations()
    {
        using var tx = _conn.BeginTransaction();

        // SMS conversations based on normalized address.
        _conn.Execute(@"
INSERT OR IGNORE INTO conversation(conversation_key, display_name, created_utc)
SELECT 'sms:' || COALESCE(s.address_norm, s.address_raw, ''),
       COALESCE(s.address_raw, s.address_norm),
       strftime('%s','now')
FROM message m
JOIN sms s ON s.message_id = m.message_id
WHERE m.transport='sms' AND m.conversation_id IS NULL;
", transaction: tx);

        _conn.Execute(@"
UPDATE message
SET conversation_id = (
  SELECT c.conversation_id
  FROM sms s
  JOIN conversation c
    ON c.conversation_key = 'sms:' || COALESCE(s.address_norm, s.address_raw, '')
  WHERE s.message_id = message.message_id
)
WHERE transport='sms' AND conversation_id IS NULL;
", transaction: tx);

        // MMS conversations based on distinct participant set.
        _conn.Execute(@"
WITH addrset AS (
  SELECT a.message_id,
         (SELECT group_concat(address_norm, '|')
            FROM (
              SELECT DISTINCT address_norm
              FROM mms_addr a2
              WHERE a2.message_id = a.message_id AND a2.address_norm IS NOT NULL
              ORDER BY address_norm
            )
         ) AS members
  FROM mms_addr a
  GROUP BY a.message_id
)
INSERT OR IGNORE INTO conversation(conversation_key, display_name, created_utc)
SELECT 'mms:' || COALESCE(mm.address_norm, mm.address_raw, '') || '|' || COALESCE(addrset.members, ''),
       COALESCE(mm.address_raw, mm.address_norm),
       strftime('%s','now')
FROM message m
JOIN mms mm ON mm.message_id = m.message_id
LEFT JOIN addrset ON addrset.message_id = m.message_id
WHERE m.transport='mms' AND m.conversation_id IS NULL;
", transaction: tx);

        _conn.Execute(@"
WITH addrset AS (
  SELECT a.message_id,
         (SELECT group_concat(address_norm, '|')
            FROM (
              SELECT DISTINCT address_norm
              FROM mms_addr a2
              WHERE a2.message_id = a.message_id AND a2.address_norm IS NOT NULL
              ORDER BY address_norm
            )
         ) AS members
  FROM mms_addr a
  GROUP BY a.message_id
)
UPDATE message
SET conversation_id = (
  SELECT c.conversation_id
  FROM mms mm
  LEFT JOIN addrset ON addrset.message_id = mm.message_id
  JOIN conversation c
    ON c.conversation_key = 'mms:' || COALESCE(mm.address_norm, mm.address_raw, '') || '|' || COALESCE(addrset.members, '')
  WHERE mm.message_id = message.message_id
)
WHERE transport='mms' AND conversation_id IS NULL;
", transaction: tx);

        // Recipients: derive from normalized addresses.
        _conn.Execute(@"
INSERT OR IGNORE INTO recipient(address_norm, address_raw_last)
SELECT DISTINCT address_norm, address_raw
FROM sms
WHERE address_norm IS NOT NULL;
", transaction: tx);

        _conn.Execute(@"
INSERT OR IGNORE INTO recipient(address_norm, address_raw_last)
SELECT DISTINCT address_norm, address_raw
FROM mms_addr
WHERE address_norm IS NOT NULL;
", transaction: tx);

        // Link conversation recipients for SMS.
        _conn.Execute(@"
INSERT OR IGNORE INTO conversation_recipient(conversation_id, recipient_id)
SELECT m.conversation_id, r.recipient_id
FROM message m
JOIN sms s ON s.message_id = m.message_id
JOIN recipient r ON r.address_norm = s.address_norm
WHERE m.conversation_id IS NOT NULL AND s.address_norm IS NOT NULL;
", transaction: tx);

        // Link conversation recipients for MMS.
        _conn.Execute(@"
INSERT OR IGNORE INTO conversation_recipient(conversation_id, recipient_id)
SELECT m.conversation_id, r.recipient_id
FROM message m
JOIN mms_addr a ON a.message_id = m.message_id
JOIN recipient r ON r.address_norm = a.address_norm
WHERE m.conversation_id IS NOT NULL AND a.address_norm IS NOT NULL;
", transaction: tx);

  // Remove empty conversations that can be created when messages are reassigned (e.g. manual merges).
  _conn.Execute(@"
DELETE FROM conversation
WHERE conversation_id NOT IN (
  SELECT DISTINCT conversation_id
  FROM message
  WHERE conversation_id IS NOT NULL
);
", transaction: tx);

  // Clean up any recipient links to conversations that no longer exist.
  _conn.Execute(@"
DELETE FROM conversation_recipient
WHERE conversation_id NOT IN (
  SELECT conversation_id
  FROM conversation
);
", transaction: tx);

        tx.Commit();
    }
}
