using Talent.Workflow.Contracts;
using Talent.Workflow.Definitions;
using Talent.Workflow.Instances;

namespace Talent.Workflow.Tests.Instances;

public sealed class WorkflowInstanceTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 8, 0, 0, TimeSpan.Zero);

    private static readonly WorkflowDefinitionModel Model = WorkflowDefinitionModel.Parse("""
        {"initialState":"Draft","states":[
          {"code":"Draft"},
          {"code":"Pending","approval":{"approver":{"kind":"Manager"}}},
          {"code":"Done","isFinal":true}],
         "transitions":[
          {"from":"Draft","action":"Submit","to":"Pending"},
          {"from":"Pending","action":"Approve","to":"Done"},
          {"from":"Pending","action":"Reject","to":"Draft","requiresComment":true}]}
        """);

    private static WorkflowInstance Start() =>
        WorkflowInstance.Start(Guid.NewGuid(), "TEST_FLOW", "Idp", "plan-1", "emp-1", "emp-1", new Dictionary<string, string>(), Model.GetState("Draft"), null, Now);

    private static TaskSpec Task(params string[] users) =>
        new([.. users.Select(u => new Assignment(u, null))], Now.AddHours(24));

    private static WorkflowInstance Pending()
    {
        var instance = Start();
        instance.Apply(Model.FindTransition("Draft", "Submit", instance.Context)!, Model.GetState("Pending"), "emp-1", null, Now, Task("mgr-1"));
        return instance;
    }

    [Fact]
    public void Start_RecordsInitialStateAndRaisesStarted()
    {
        var instance = Start();

        instance.CurrentState.ShouldBe("Draft");
        instance.Status.ShouldBe(WorkflowStatus.Active);
        instance.History.Count.ShouldBe(1);
        instance.DomainEvents.OfType<WorkflowStarted>().Count().ShouldBe(1);
    }

    [Fact]
    public void Apply_MovesStateWritesHistoryAndOpensTask()
    {
        var instance = Pending();

        instance.CurrentState.ShouldBe("Pending");
        instance.History.Select(h => h.Action).ShouldBe(["Start", "Submit"]);
        instance.OpenTask!.Assignees.Select(a => a.UserId).ShouldBe(["mgr-1"]);
        instance.OpenTask.DueAt.ShouldBe(Now.AddHours(24));
        instance.DomainEvents.OfType<WorkflowTransitioned>().Single().To.ShouldBe("Pending");
    }

    [Fact]
    public void EnsureCanAct_WhenNoTaskOpen_OnlyTheStarterMayAct()
    {
        var instance = Start();

        instance.EnsureCanAct("emp-1");
        Should.Throw<ActorNotAllowedException>(() => instance.EnsureCanAct("mgr-1"));
    }

    [Fact]
    public void EnsureCanAct_WhenTaskOpen_OnlyAssigneesMayAct()
    {
        var instance = Pending();

        instance.EnsureCanAct("mgr-1");
        Should.Throw<ActorNotAllowedException>(() => instance.EnsureCanAct("emp-1"));
    }

    [Fact]
    public void Apply_WhenFinalState_CompletesInstanceAndClosesTask()
    {
        var instance = Pending();

        instance.Apply(Model.FindTransition("Pending", "Approve", instance.Context)!, Model.GetState("Done"), "mgr-1", "ok", Now.AddHours(2), null);

        instance.Status.ShouldBe(WorkflowStatus.Completed);
        instance.CompletedAt.ShouldBe(Now.AddHours(2));
        instance.OpenTask.ShouldBeNull();
        instance.DomainEvents.OfType<WorkflowCompleted>().Single().FinalState.ShouldBe("Done");
    }

    [Fact]
    public void Apply_WhenCommentRequiredAndMissing_Throws()
    {
        var instance = Pending();
        var reject = Model.FindTransition("Pending", "Reject", instance.Context)!;

        Should.Throw<CommentRequiredException>(() => instance.Apply(reject, Model.GetState("Draft"), "mgr-1", " ", Now, null));
    }

    [Fact]
    public void Apply_WhenAlreadyCompleted_Throws()
    {
        var instance = Pending();
        instance.Apply(Model.FindTransition("Pending", "Approve", instance.Context)!, Model.GetState("Done"), "mgr-1", null, Now, null);

        Should.Throw<WorkflowNotActiveException>(() =>
            instance.Apply(Model.FindTransition("Pending", "Approve", instance.Context)!, Model.GetState("Done"), "mgr-1", null, Now, null));
    }

    [Fact]
    public void Apply_WhenReturned_KeepsFullHistory()
    {
        var instance = Pending();
        instance.Apply(Model.FindTransition("Pending", "Reject", instance.Context)!, Model.GetState("Draft"), "mgr-1", "needs work", Now, null);
        instance.Apply(Model.FindTransition("Draft", "Submit", instance.Context)!, Model.GetState("Pending"), "emp-1", null, Now, Task("mgr-1"));

        instance.History.Select(h => h.Action).ShouldBe(["Start", "Submit", "Reject", "Submit"]);
        instance.History.Select(h => h.Sequence).ShouldBe([1, 2, 3, 4]);
        instance.Tasks.Count.ShouldBe(2);
        instance.OpenTask!.Id.ShouldBe(instance.Tasks.Last().Id);
    }

    [Fact]
    public void DelegateTask_AddsDelegateWhoThenMayAct()
    {
        var instance = Pending();

        instance.DelegateTask("mgr-1", "mgr-2", Now);

        instance.EnsureCanAct("mgr-2");
        instance.OpenTask!.Assignees.Single(a => a.UserId == "mgr-2").DelegatedFrom.ShouldBe("mgr-1");
        instance.History.Last().Action.ShouldBe("Delegate");
    }

    [Fact]
    public void DelegateTask_WhenCallerIsNotAssignee_Throws()
    {
        var instance = Pending();

        Should.Throw<ActorNotAllowedException>(() => instance.DelegateTask("someone", "mgr-2", Now));
    }

    [Fact]
    public void Escalate_AddsAssigneesRaisesLevelAndMovesDueDate()
    {
        var instance = Pending();

        instance.Escalate([new Assignment("hr-1", null)], Now.AddHours(48), Now.AddHours(25));

        instance.OpenTask!.EscalationLevel.ShouldBe(1);
        instance.OpenTask.DueAt.ShouldBe(Now.AddHours(48));
        instance.OpenTask.Assignees.Select(a => a.UserId).ShouldBe(["mgr-1", "hr-1"], ignoreOrder: true);
        instance.DomainEvents.OfType<ApprovalTaskEscalated>().Single().Level.ShouldBe(1);
        instance.History.Last().Action.ShouldBe("Escalate");
    }

    [Fact]
    public void Escalate_WhenAssigneeAlreadyPresent_DoesNotDuplicate()
    {
        var instance = Pending();

        instance.Escalate([new Assignment("mgr-1", null)], Now.AddHours(48), Now.AddHours(25));

        instance.OpenTask!.Assignees.Count.ShouldBe(1);
    }

    [Fact]
    public void MarkSlaBreached_StopsTaskBeingDueAgain()
    {
        var instance = Pending();

        instance.MarkSlaBreached(Now.AddHours(30));

        instance.OpenTask!.SlaBreachedAt.ShouldBe(Now.AddHours(30));
        instance.DomainEvents.OfType<ApprovalTaskSlaBreached>().Count().ShouldBe(1);
    }
}
