using Talent.Workflow.Definitions;

namespace Talent.Workflow.Tests.Definitions;

public sealed class GuardTests
{
    private static Dictionary<string, string> Ctx(string key, string value) => new() { [key] = value };

    [Theory]
    [InlineData("Eq", "5", "5", true)]
    [InlineData("Eq", "5.0", "5", true)]
    [InlineData("Neq", "5", "6", true)]
    [InlineData("Gt", "10", "9", true)]
    [InlineData("Gt", "9", "10", false)]
    [InlineData("Gte", "9", "9", true)]
    [InlineData("Lt", "9", "10", true)]
    [InlineData("Lte", "10", "9", false)]
    [InlineData("In", "M2", "M1,M2,M3", true)]
    [InlineData("In", "M9", "M1,M2,M3", false)]
    [InlineData("Eq", "abc", "abc", true)]
    public void Evaluate_ComparesNumbersAsNumbersAndTextAsText(string op, string actual, string expected, bool result) =>
        new GuardDefinition("f", Enum.Parse<GuardOperator>(op), expected).Evaluate(Ctx("f", actual)).ShouldBe(result);

    [Fact]
    public void Evaluate_WhenFieldMissing_IsFalse() =>
        new GuardDefinition("f", GuardOperator.Eq, "1").Evaluate(new Dictionary<string, string>()).ShouldBeFalse();

    [Fact]
    public void Evaluate_WhenOrderingTextValues_IsFalse() =>
        new GuardDefinition("f", GuardOperator.Gt, "abc").Evaluate(Ctx("f", "abd")).ShouldBeFalse();

    [Fact]
    public void FindTransition_UsesFirstTransitionWhoseGuardsAllPass()
    {
        var model = WorkflowDefinitionModel.Parse("""
            {"initialState":"P","states":[{"code":"P","approval":{"approver":{"kind":"Manager"}}},{"code":"Hr"},{"code":"Done","isFinal":true}],
             "transitions":[
              {"from":"P","action":"Approve","to":"Hr","guards":[{"field":"cost","op":"Gte","value":"1000"}]},
              {"from":"P","action":"Approve","to":"Done"},
              {"from":"Hr","action":"Approve","to":"Done"}]}
            """);

        model.FindTransition("P", "Approve", Ctx("cost", "1500"))!.To.ShouldBe("Hr");
        model.FindTransition("P", "Approve", Ctx("cost", "200"))!.To.ShouldBe("Done");
        model.FindTransition("P", "Approve", new Dictionary<string, string>())!.To.ShouldBe("Done");
    }
}
