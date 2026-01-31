using Dapper;
using Microsoft.Data.Sqlite;

namespace MsgBakMan.Data.Repositories;

public sealed class PhoneNormalizationMaintenance
{
    private readonly SqliteConnection _conn;

    public PhoneNormalizationMaintenance(SqliteConnection conn)
    {
        _conn = conn;
    }

    public RepairLegacyPlus10Result RepairLegacyPlus10Numbers()
    {
        using var tx = _conn.BeginTransaction();

        // Fix address_norm stored as "+1" + "0..." (caused by old NANP heuristic applied to local numbers that begin with 0).
        // Example: 0409478694 -> "+10409478694" (wrong) -> should be "+0409478694".
        var smsUpdated = _conn.Execute(@"
UPDATE sms
SET address_norm = '+0' || substr(address_norm, 4)
WHERE address_norm LIKE '+10%';
", transaction: tx);

        var mmsUpdated = _conn.Execute(@"
UPDATE mms
SET address_norm = '+0' || substr(address_norm, 4)
WHERE address_norm LIKE '+10%';
", transaction: tx);

        var mmsAddrUpdated = _conn.Execute(@"
UPDATE mms_addr
SET address_norm = '+0' || substr(address_norm, 4)
WHERE address_norm LIKE '+10%';
", transaction: tx);

        // Merge recipient rows that would collide after the update.
        var recipientMergePairs = _conn.Query<(long OldId, long NewId)>(@"
SELECT old.recipient_id AS OldId,
       new.recipient_id AS NewId
FROM recipient old
JOIN recipient new
  ON new.address_norm = '+0' || substr(old.address_norm, 4)
WHERE old.address_norm LIKE '+10%';
", transaction: tx).ToList();

        var recipientsMerged = 0;
        foreach (var (oldId, newId) in recipientMergePairs)
        {
            if (oldId == newId)
            {
                continue;
            }

            _conn.Execute(@"
UPDATE OR IGNORE conversation_recipient
SET recipient_id = @newId
WHERE recipient_id = @oldId;
", new { oldId, newId }, tx);

            _conn.Execute(@"
DELETE FROM recipient
WHERE recipient_id = @oldId;
", new { oldId }, tx);

            recipientsMerged++;
        }

        var recipientsUpdated = _conn.Execute(@"
UPDATE recipient
SET address_norm = '+0' || substr(address_norm, 4)
WHERE address_norm LIKE '+10%'
  AND NOT EXISTS (
    SELECT 1
    FROM recipient r2
    WHERE r2.address_norm = '+0' || substr(recipient.address_norm, 4)
  );
", transaction: tx);

        // Rebuild conversation assignment so keys/display refresh based on corrected numbers.
        var messagesCleared = _conn.Execute(@"
UPDATE message
SET conversation_id = NULL
WHERE conversation_id IS NOT NULL;
", transaction: tx);

        var convRecipientsDeleted = _conn.Execute("DELETE FROM conversation_recipient;", transaction: tx);
        var conversationsDeleted = _conn.Execute("DELETE FROM conversation;", transaction: tx);

        tx.Commit();

        return new RepairLegacyPlus10Result(
            SmsUpdated: smsUpdated,
            MmsUpdated: mmsUpdated,
            MmsAddrUpdated: mmsAddrUpdated,
            RecipientsUpdated: recipientsUpdated,
            RecipientsMerged: recipientsMerged,
            MessagesConversationCleared: messagesCleared,
            ConversationRecipientsDeleted: convRecipientsDeleted,
            ConversationsDeleted: conversationsDeleted
        );
    }

    public sealed record RepairLegacyPlus10Result(
        int SmsUpdated,
        int MmsUpdated,
        int MmsAddrUpdated,
        int RecipientsUpdated,
        int RecipientsMerged,
        int MessagesConversationCleared,
        int ConversationRecipientsDeleted,
        int ConversationsDeleted
    );
}
