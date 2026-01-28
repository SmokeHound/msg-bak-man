using Dapper;
using Microsoft.Data.Sqlite;

namespace MsgBakMan.Data.Repositories;

public sealed class ConversationRepository
{
    private readonly SqliteConnection _conn;

    public ConversationRepository(SqliteConnection conn)
    {
        _conn = conn;
    }

    public IEnumerable<ConversationRow> ListConversations(string? search)
    {
        search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        return _conn.Query<ConversationRow>(@"
SELECT c.conversation_id AS ConversationId,
       c.conversation_key AS ConversationKey,
       c.display_name AS DisplayName,
         CAST(COUNT(m.message_id) AS INTEGER) AS MessageCount,
         CAST(MAX(m.date_ms) AS INTEGER) AS LastDateMs
FROM conversation c
LEFT JOIN message m ON m.conversation_id = c.conversation_id
WHERE (@q IS NULL)
   OR (c.display_name LIKE '%' || @q || '%')
   OR (c.conversation_key LIKE '%' || @q || '%')
GROUP BY c.conversation_id, c.conversation_key, c.display_name
ORDER BY LastDateMs DESC NULLS LAST, MessageCount DESC;
", new { q = search });
    }

    public IEnumerable<MessageRow> GetMessages(long conversationId, int limit = 500)
    {
        return _conn.Query<MessageRow>(@"
SELECT m.message_id AS MessageId,
       m.conversation_id AS ConversationId,
       m.transport AS Transport,
       m.box AS Box,
       m.date_ms AS DateMs,
       s.address_raw AS AddressRaw,
       s.body AS SmsBody,
       mm.sub AS MmsSubject
FROM message m
LEFT JOIN sms s ON s.message_id = m.message_id
LEFT JOIN mms mm ON mm.message_id = m.message_id
WHERE m.conversation_id = @id
ORDER BY m.date_ms DESC, m.message_id DESC
LIMIT @lim;
", new { id = conversationId, lim = limit });
    }

    public IEnumerable<MessageRow> SearchMessages(string query, int limit = 500)
    {
        query = query.Trim();
        if (query.Length == 0)
        {
            return Array.Empty<MessageRow>();
        }

        return _conn.Query<MessageRow>(@"
SELECT m.message_id AS MessageId,
         m.conversation_id AS ConversationId,
       m.transport AS Transport,
       m.box AS Box,
       m.date_ms AS DateMs,
       s.address_raw AS AddressRaw,
       s.body AS SmsBody,
       mm.sub AS MmsSubject
FROM message m
LEFT JOIN sms s ON s.message_id = m.message_id
LEFT JOIN mms mm ON mm.message_id = m.message_id
LEFT JOIN mms_part p ON p.message_id = m.message_id
WHERE (s.body LIKE '%' || @q || '%')
   OR (mm.sub LIKE '%' || @q || '%')
   OR (p.text LIKE '%' || @q || '%')
GROUP BY m.message_id
ORDER BY m.date_ms DESC, m.message_id DESC
LIMIT @lim;
", new { q = query, lim = limit });
    }

    public void MergeConversations(long targetConversationId, IReadOnlyList<long> mergeConversationIds)
    {
        if (mergeConversationIds.Count == 0)
        {
            return;
        }

        using var tx = _conn.BeginTransaction();

        _conn.Execute(@"
UPDATE message
SET conversation_id = @target
WHERE conversation_id IN @ids;
", new { target = targetConversationId, ids = mergeConversationIds }, tx);

        _conn.Execute(@"
INSERT OR IGNORE INTO conversation_recipient(conversation_id, recipient_id)
SELECT @target, cr.recipient_id
FROM conversation_recipient cr
WHERE cr.conversation_id IN @ids;
", new { target = targetConversationId, ids = mergeConversationIds }, tx);

        _conn.Execute(@"
DELETE FROM conversation
WHERE conversation_id IN @ids;
", new { ids = mergeConversationIds }, tx);

        tx.Commit();
    }

    public sealed record ConversationRow(
        long ConversationId,
        string ConversationKey,
        string? DisplayName,
        long MessageCount,
        long? LastDateMs
    );

    public sealed class MessageRow
    {
        public long MessageId { get; init; }
        public long? ConversationId { get; init; }
        public string Transport { get; init; } = string.Empty;
        public long Box { get; init; }
        public long DateMs { get; init; }
        public string? AddressRaw { get; init; }
        public string? SmsBody { get; init; }
        public string? MmsSubject { get; init; }
    }
}
