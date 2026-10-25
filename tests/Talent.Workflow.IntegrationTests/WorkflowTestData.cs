namespace Talent.Workflow.IntegrationTests;

internal static class WorkflowTestData
{
    /// <summary>An IDP flow with no HR step.</summary>
    public const string SimpleIdp = """
        {"initialState":"Draft","states":[
          {"code":"Draft"},
          {"code":"PendingManager","approval":{"approver":{"kind":"Manager"}}},
          {"code":"Approved","isFinal":true}],
         "transitions":[
          {"from":"Draft","action":"Submit","to":"PendingManager"},
          {"from":"PendingManager","action":"Approve","to":"Approved"}]}
        """;
}
