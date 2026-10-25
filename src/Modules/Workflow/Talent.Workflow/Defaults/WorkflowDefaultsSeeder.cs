using Microsoft.EntityFrameworkCore;
using Talent.Shared.Security;
using Talent.Workflow.Contracts;
using Talent.Workflow.Persistence;

namespace Talent.Workflow.Defaults;

/// <summary>
/// Publishes the default flows so a new install works before the client sends its approval chains.
/// These are defaults, not the client's answers. The client replaces them with new versions.
/// </summary>
internal static class WorkflowDefaultsSeeder
{
    /// <summary>
    /// Employee drafts and submits. The manager approves. A plan costing 1000 or more also needs HR.
    /// A rejection returns the plan to the employee and needs a comment. A late manager is escalated to HR after 72 hours.
    /// </summary>
    public const string IdpApproval = """
        {"initialState":"Draft","states":[
          {"code":"Draft"},
          {"code":"PendingManager","approval":{"approver":{"kind":"Manager"},
             "sla":{"hours":72,"escalateTo":{"kind":"Role","value":"HR_ADMIN"},"maxEscalations":1}}},
          {"code":"PendingHr","approval":{"approver":{"kind":"Role","value":"HR_ADMIN"},
             "sla":{"hours":120,"escalateTo":{"kind":"Role","value":"HR_ADMIN"},"maxEscalations":0}}},
          {"code":"Approved","isFinal":true},
          {"code":"Cancelled","isFinal":true}],
         "transitions":[
          {"from":"Draft","action":"Submit","to":"PendingManager"},
          {"from":"Draft","action":"Cancel","to":"Cancelled"},
          {"from":"PendingManager","action":"Approve","to":"PendingHr","guards":[{"field":"estimatedCost","op":"Gte","value":"1000"}]},
          {"from":"PendingManager","action":"Approve","to":"Approved"},
          {"from":"PendingManager","action":"Reject","to":"Draft","requiresComment":true},
          {"from":"PendingHr","action":"Approve","to":"Approved"},
          {"from":"PendingHr","action":"Reject","to":"Draft","requiresComment":true}]}
        """;

    /// <summary>
    /// A facilitator submits the calibration result. The board chair signs off, then HR. A rejection reopens it.
    /// A chair who is late is escalated to HR once, then the sign-off is returned after the second deadline.
    /// </summary>
    public const string CalibrationSignoff = """
        {"initialState":"Open","states":[
          {"code":"Open"},
          {"code":"PendingChair","approval":{"approver":{"kind":"Role","value":"BOARD_CHAIR"},
             "sla":{"hours":120,"escalateTo":{"kind":"Role","value":"HR_ADMIN"},"maxEscalations":1,"expiryAction":"Reject"}}},
          {"code":"PendingHr","approval":{"approver":{"kind":"Role","value":"HR_ADMIN"}}},
          {"code":"SignedOff","isFinal":true},
          {"code":"Withdrawn","isFinal":true}],
         "transitions":[
          {"from":"Open","action":"Submit","to":"PendingChair"},
          {"from":"Open","action":"Withdraw","to":"Withdrawn"},
          {"from":"PendingChair","action":"Approve","to":"PendingHr"},
          {"from":"PendingChair","action":"Reject","to":"Open","requiresComment":true},
          {"from":"PendingHr","action":"Approve","to":"SignedOff"},
          {"from":"PendingHr","action":"Reject","to":"Open","requiresComment":true}]}
        """;

    public static async Task EnsureAsync(WorkflowDbContext db, TimeProvider clock, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(clock);

        var admin = new WorkflowAdminService(db, new SystemCurrentUser(), clock);
        await EnsureAsync(db, admin, WorkflowCodes.IdpApproval, "IDP approval", IdpApproval, cancellationToken);
        await EnsureAsync(db, admin, WorkflowCodes.CalibrationSignoff, "Calibration sign-off", CalibrationSignoff, cancellationToken);
    }

    private static async Task EnsureAsync(
        WorkflowDbContext db, WorkflowAdminService admin, string code, string name, string payload, CancellationToken cancellationToken)
    {
        if (await db.Definitions.AnyAsync(d => d.Code == code, cancellationToken))
        {
            return;
        }

        var definitionId = await admin.CreateDefinitionAsync(code, name, cancellationToken);
        var versionId = await admin.CreateDraftAsync(definitionId, payload, cancellationToken);
        await admin.PublishAsync(versionId, cancellationToken);
    }
}
