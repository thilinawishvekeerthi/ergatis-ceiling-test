using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Talent.Config.Contracts;
using Talent.Workflow.Contracts;
using Talent.Workflow.Persistence;

namespace Talent.Workflow.IntegrationTests;

public sealed class WorkflowFlowTests(WorkflowDatabaseFixture db)
{
    [Fact]
    public async Task Defaults_ArePublishedForIdpAndCalibration()
    {
        await using var h = await WorkflowHarness.CreateAsync(db);

        var idp = await h.StartIdpAsync();
        var calibration = await h.As("facilitator").RunAsync(s => s.StartAsync(
            new StartWorkflowRequest(WorkflowCodes.CalibrationSignoff, "Session", "s-1", null, null), CancellationToken.None));

        (await h.GetAsync(idp)).CurrentState.ShouldBe("Draft");
        (await h.GetAsync(calibration)).CurrentState.ShouldBe("Open");
    }

    [Fact]
    public async Task IdpApproval_WhenManagerApproves_CompletesWithFullHistory()
    {
        await using var h = await WorkflowHarness.CreateAsync(db);
        var id = await h.StartIdpAsync();

        await h.ActAsync("emp-1", id, "Submit");
        var pending = await h.GetAsync(id);
        pending.CurrentState.ShouldBe("PendingManager");
        pending.OpenTask!.Assignees.ShouldBe(["mgr-1"]);
        pending.AvailableActions.ShouldBe(["Approve", "Reject"], ignoreOrder: true);

        await h.ActAsync("mgr-1", id, "Approve", "looks good");

        var done = await h.GetAsync(id);
        done.Status.ShouldBe(WorkflowStatus.Completed);
        done.CurrentState.ShouldBe("Approved");
        done.OpenTask.ShouldBeNull();
        done.History.Select(x => x.Action).ShouldBe(["Start", "Submit", "Approve"]);
        done.History[^1].Actor.ShouldBe("mgr-1");
        done.History[^1].Comment.ShouldBe("looks good");
    }

    [Fact]
    public async Task IdpApproval_WhenCostIsHigh_NeedsHrAfterManager()
    {
        await using var h = await WorkflowHarness.CreateAsync(db);
        var id = await h.StartIdpAsync(cost: "2500");
        await h.ActAsync("emp-1", id, "Submit");

        await h.ActAsync("mgr-1", id, "Approve");
        (await h.GetAsync(id)).CurrentState.ShouldBe("PendingHr");

        await h.ActAsync("hr-1", id, "Approve");
        (await h.GetAsync(id)).CurrentState.ShouldBe("Approved");
    }

    [Fact]
    public async Task Reject_WithoutComment_IsRefusedAndStateIsKept()
    {
        await using var h = await WorkflowHarness.CreateAsync(db);
        var id = await h.StartIdpAsync();
        await h.ActAsync("emp-1", id, "Submit");

        var error = await Should.ThrowAsync<WorkflowException>(() => h.ActAsync("mgr-1", id, "Reject"));

        error.Kind.ShouldBe(WorkflowErrorKind.Invalid);
        (await h.GetAsync(id)).CurrentState.ShouldBe("PendingManager");
    }

    [Fact]
    public async Task Reject_WithComment_ReturnsToEmployeeWhoCanResubmit()
    {
        await using var h = await WorkflowHarness.CreateAsync(db);
        var id = await h.StartIdpAsync();
        await h.ActAsync("emp-1", id, "Submit");

        await h.ActAsync("mgr-1", id, "Reject", "add a course");
        await h.ActAsync("emp-1", id, "Submit");

        var instance = await h.GetAsync(id);
        instance.CurrentState.ShouldBe("PendingManager");
        instance.History.Select(x => x.Action).ShouldBe(["Start", "Submit", "Reject", "Submit"]);
    }

    [Fact]
    public async Task Transition_WhenCallerIsNotAssigned_IsForbidden()
    {
        await using var h = await WorkflowHarness.CreateAsync(db);
        var id = await h.StartIdpAsync();
        await h.ActAsync("emp-1", id, "Submit");

        var error = await Should.ThrowAsync<WorkflowException>(() => h.ActAsync("emp-1", id, "Approve"));

        error.Kind.ShouldBe(WorkflowErrorKind.Forbidden);
    }

    [Fact]
    public async Task Transition_WhenActionNotAllowedInState_IsInvalid()
    {
        await using var h = await WorkflowHarness.CreateAsync(db);
        var id = await h.StartIdpAsync();

        var error = await Should.ThrowAsync<WorkflowException>(() => h.ActAsync("emp-1", id, "Approve"));

        error.Kind.ShouldBe(WorkflowErrorKind.Invalid);
    }

    [Fact]
    public async Task Transition_WhenWorkflowIsCompleted_IsConflict()
    {
        await using var h = await WorkflowHarness.CreateAsync(db);
        var id = await h.StartIdpAsync();
        await h.ActAsync("emp-1", id, "Cancel");

        var error = await Should.ThrowAsync<WorkflowException>(() => h.ActAsync("emp-1", id, "Submit"));

        error.Kind.ShouldBe(WorkflowErrorKind.Conflict);
    }

    [Fact]
    public async Task Submit_WhenEmployeeHasNoManager_FailsAndNothingChanges()
    {
        await using var h = await WorkflowHarness.CreateAsync(db);
        h.Directory.Managers.Clear();
        var id = await h.StartIdpAsync();

        var error = await Should.ThrowAsync<WorkflowException>(() => h.ActAsync("emp-1", id, "Submit"));

        error.Kind.ShouldBe(WorkflowErrorKind.Invalid);
        var instance = await h.GetAsync(id);
        instance.CurrentState.ShouldBe("Draft");
        instance.History.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Start_WhenSubjectAlreadyHasRunningWorkflow_IsConflict()
    {
        await using var h = await WorkflowHarness.CreateAsync(db);
        await h.StartIdpAsync();

        var error = await Should.ThrowAsync<WorkflowException>(() => h.StartIdpAsync());

        error.Kind.ShouldBe(WorkflowErrorKind.Conflict);
    }

    [Fact]
    public async Task Start_WhenDefinitionUnknown_IsNotFound()
    {
        await using var h = await WorkflowHarness.CreateAsync(db);

        var error = await Should.ThrowAsync<WorkflowException>(() => h.As("emp-1").RunAsync(s => s.StartAsync(
            new StartWorkflowRequest("NOPE", "Idp", "p", null, null), CancellationToken.None)));

        error.Kind.ShouldBe(WorkflowErrorKind.NotFound);
    }

    [Fact]
    public async Task FindForSubject_ReturnsTheInstance()
    {
        await using var h = await WorkflowHarness.CreateAsync(db);
        var id = await h.StartIdpAsync(subject: "plan-9");

        var found = await h.RunAsync(s => s.FindForSubjectAsync("Idp", "plan-9", CancellationToken.None));

        found!.Id.ShouldBe(id);
        (await h.RunAsync(s => s.FindForSubjectAsync("Idp", "other", CancellationToken.None))).ShouldBeNull();
    }

    [Fact]
    public async Task Inbox_ListsPendingTasksOfTheCurrentUserOnly()
    {
        await using var h = await WorkflowHarness.CreateAsync(db);
        var id = await h.StartIdpAsync();
        await h.ActAsync("emp-1", id, "Submit");

        var mine = await h.As("mgr-1").RunAsync(s => s.GetInboxAsync(CancellationToken.None));
        var others = await h.As("someone").RunAsync(s => s.GetInboxAsync(CancellationToken.None));

        mine.Single().InstanceId.ShouldBe(id);
        others.ShouldBeEmpty();
    }

    [Fact]
    public async Task ConcurrentApprovals_OnlyOneWins()
    {
        await using var h = await WorkflowHarness.CreateAsync(db);
        var id = await h.StartIdpAsync();
        await h.ActAsync("emp-1", id, "Submit");
        var taskId = (await h.GetAsync(id)).OpenTask!.Id;
        await h.As("mgr-1").RunAsync(s => s.DelegateTaskAsync(taskId, "mgr-2", CancellationToken.None));

        async Task<bool> TryApprove(string user)
        {
            await Task.Yield();
            try
            {
                await h.ActAsync(user, id, "Approve");
                return true;
            }
            catch (WorkflowException)
            {
                return false;
            }
        }

        var results = await Task.WhenAll(TryApprove("mgr-1"), TryApprove("mgr-2"));

        results.Count(r => r).ShouldBe(1);
        (await h.GetAsync(id)).History.Count(x => x.Action == "Approve").ShouldBe(1);
    }

    [Fact]
    public async Task VersionPinning_RunningInstanceKeepsItsVersionWhileNewOnesUseTheNewOne()
    {
        await using var h = await WorkflowHarness.CreateAsync(db);
        h.Directory.Managers["emp-2"] = "mgr-2";
        var running = await h.StartIdpAsync(subject: "plan-1", cost: "2500");
        var v1 = (await h.GetAsync(running)).DefinitionVersionId;

        // Version 2 has no HR step.
        var definitionId = await h.WithAsync<WorkflowDbContext, Guid>(c =>
            c.Definitions.Where(d => d.Code == WorkflowCodes.IdpApproval).Select(d => d.Id).SingleAsync());
        await h.WithAsync<WorkflowAdminService, bool>(async admin =>
        {
            var draft = await admin.CreateDraftAsync(definitionId, WorkflowTestData.SimpleIdp, CancellationToken.None);
            await admin.PublishAsync(draft, CancellationToken.None);
            return true;
        });

        var fresh = await h.StartIdpAsync(employee: "emp-2", subject: "plan-2", cost: "2500");
        (await h.GetAsync(fresh)).DefinitionVersionId.ShouldNotBe(v1);
        (await h.GetAsync(running)).DefinitionVersionId.ShouldBe(v1);

        await h.ActAsync("emp-1", running, "Submit");
        await h.ActAsync("mgr-1", running, "Approve");
        (await h.GetAsync(running)).CurrentState.ShouldBe("PendingHr");

        await h.ActAsync("emp-2", fresh, "Submit");
        await h.ActAsync("mgr-2", fresh, "Approve");
        (await h.GetAsync(fresh)).CurrentState.ShouldBe("Approved");

        var firstVersionStatus = await h.WithAsync<WorkflowDbContext, ConfigVersionStatus>(c =>
            c.Versions.Where(v => v.Id == v1).Select(v => v.Status).SingleAsync());
        firstVersionStatus.ShouldBe(ConfigVersionStatus.Locked);
    }
}
