using Microsoft.EntityFrameworkCore;
using Talent.Shared.Security;

namespace Talent.Shared.Auditing;

public static class AuditExtensions
{
    /// <summary>
    /// Adds an audit row to the caller's context, so it commits or rolls back with the change it describes.
    /// Pass IDs and values only. Never pass names, emails or other personal data in the JSON of non-confidential rows.
    /// </summary>
    public static void AddAudit(
        this DbContext context,
        ICurrentUser user,
        TimeProvider clock,
        string action,
        string objectType,
        string objectId,
        bool confidential = false,
        string? beforeJson = null,
        string? afterJson = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(clock);

        context.Set<AuditEntry>().Add(new AuditEntry
        {
            OccurredAt = clock.GetUtcNow(),
            ActorId = user.UserId,
            Action = action,
            ObjectType = objectType,
            ObjectId = objectId,
            Confidential = confidential,
            BeforeJson = beforeJson,
            AfterJson = afterJson,
        });
    }
}
