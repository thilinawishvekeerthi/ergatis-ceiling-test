using Talent.Workflow.Definitions;

namespace Talent.Workflow.Tests.Definitions;

public sealed class WorkflowDefinitionModelTests
{
    private const string Valid = """
        {"initialState":"Draft","states":[
          {"code":"Draft"},
          {"code":"Pending","approval":{"approver":{"kind":"Manager"},"sla":{"hours":24,"escalateTo":{"kind":"Role","value":"HR"},"maxEscalations":2,"expiryAction":"Reject"}}},
          {"code":"Done","isFinal":true},
          {"code":"Back"}],
         "transitions":[
          {"from":"Draft","action":"Submit","to":"Pending"},
          {"from":"Pending","action":"Approve","to":"Done"},
          {"from":"Pending","action":"Reject","to":"Back","requiresComment":true},
          {"from":"Back","action":"Resubmit","to":"Pending"}]}
        """;

    private static string With(string find, string replace) => Valid.Replace(find, replace, StringComparison.Ordinal);

    [Fact]
    public void Parse_WhenValid_ReadsStatesTransitionsAndSla()
    {
        var model = WorkflowDefinitionModel.Parse(Valid);

        model.InitialState.ShouldBe("Draft");
        model.States.Count.ShouldBe(4);
        var sla = model.GetState("Pending").Approval!.Sla!;
        sla.Hours.ShouldBe(24);
        sla.MaxEscalations.ShouldBe(2);
        sla.EscalateTo.Kind.ShouldBe(ActorKind.Role);
        sla.ExpiryAction.ShouldBe("Reject");
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("""{"initialState":"A","states":[{"code":"A"}],"transitions":[],"extra":1}""")]
    public void Parse_WhenMalformed_Throws(string json) =>
        Should.Throw<InvalidWorkflowDefinitionException>(() => WorkflowDefinitionModel.Parse(json));

    [Fact]
    public void Parse_WhenInitialStateUnknown_Throws() =>
        Should.Throw<InvalidWorkflowDefinitionException>(() => WorkflowDefinitionModel.Parse(With("\"initialState\":\"Draft\"", "\"initialState\":\"Nope\"")));

    [Fact]
    public void Parse_WhenTransitionTargetUnknown_Throws() =>
        Should.Throw<InvalidWorkflowDefinitionException>(() => WorkflowDefinitionModel.Parse(With("\"to\":\"Done\"", "\"to\":\"Nowhere\"")));

    [Fact]
    public void Parse_WhenDuplicateStateCode_Throws() =>
        Should.Throw<InvalidWorkflowDefinitionException>(() => WorkflowDefinitionModel.Parse(With("{\"code\":\"Back\"}", "{\"code\":\"Draft\"}")));

    [Fact]
    public void Parse_WhenStateUnreachable_Throws() =>
        Should.Throw<InvalidWorkflowDefinitionException>(() => WorkflowDefinitionModel.Parse(With("{\"code\":\"Back\"}", "{\"code\":\"Back\"},{\"code\":\"Orphan\"}")));

    [Fact]
    public void Parse_WhenNoFinalState_Throws() =>
        Should.Throw<InvalidWorkflowDefinitionException>(() => WorkflowDefinitionModel.Parse(With("\"isFinal\":true", "\"isFinal\":false")));

    [Fact]
    public void Parse_WhenFinalStateHasOutgoingTransition_Throws() =>
        Should.Throw<InvalidWorkflowDefinitionException>(() => WorkflowDefinitionModel.Parse(With(
            "{\"from\":\"Back\"", "{\"from\":\"Done\",\"action\":\"Reopen\",\"to\":\"Back\"},{\"from\":\"Back\"")));

    [Fact]
    public void Parse_WhenSlaHoursNotPositive_Throws() =>
        Should.Throw<InvalidWorkflowDefinitionException>(() => WorkflowDefinitionModel.Parse(With("\"hours\":24", "\"hours\":0")));

    [Fact]
    public void Parse_WhenExpiryActionHasNoTransition_Throws() =>
        Should.Throw<InvalidWorkflowDefinitionException>(() => WorkflowDefinitionModel.Parse(With("\"expiryAction\":\"Reject\"", "\"expiryAction\":\"Explode\"")));

    [Fact]
    public void Parse_WhenUserActorHasNoValue_Throws() =>
        Should.Throw<InvalidWorkflowDefinitionException>(() => WorkflowDefinitionModel.Parse(With("\"kind\":\"Role\",\"value\":\"HR\"", "\"kind\":\"User\"")));

    [Fact]
    public void Parse_WhenGuardOperatorUnknown_Throws() =>
        Should.Throw<InvalidWorkflowDefinitionException>(() => WorkflowDefinitionModel.Parse(With(
            "\"action\":\"Approve\",\"to\":\"Done\"", "\"action\":\"Approve\",\"to\":\"Done\",\"guards\":[{\"field\":\"x\",\"op\":\"Like\",\"value\":\"1\"}]")));

    [Fact]
    public void Parse_WhenApprovalStateHasNoExit_Throws() =>
        Should.Throw<InvalidWorkflowDefinitionException>(() => WorkflowDefinitionModel.Parse(
            """{"initialState":"A","states":[{"code":"A","approval":{"approver":{"kind":"Manager"}}},{"code":"B","isFinal":true}],"transitions":[]}"""));

    [Fact]
    public void FindTransition_WhenActionKnown_ReturnsIt()
    {
        var model = WorkflowDefinitionModel.Parse(Valid);

        model.FindTransition("Pending", "Approve", new Dictionary<string, string>())!.To.ShouldBe("Done");
    }

    [Fact]
    public void FindTransition_WhenActionNotAllowedInState_ReturnsNull() =>
        WorkflowDefinitionModel.Parse(Valid).FindTransition("Draft", "Approve", new Dictionary<string, string>()).ShouldBeNull();

    [Fact]
    public void ToCanonicalJson_RoundTripsThroughParse()
    {
        var model = WorkflowDefinitionModel.Parse(Valid);

        WorkflowDefinitionModel.Parse(model.ToCanonicalJson()).ToCanonicalJson().ShouldBe(model.ToCanonicalJson());
    }
}
