using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Talent.Workflow.Contracts;
using Talent.Workflow.Persistence;

namespace Talent.Workflow.IntegrationTests;

public sealed class DelegationAndSlaTests(WorkflowDatabaseFixture db)
{
    private static async Task<Guid> SubmittedIdpAsync(WorkflowHarness h)
    {
        var id = await h.StartIdpAsync();
        await h.ActAsync("emp-1", id, "Submit");
        return id;
    }

    [Fact]
    public async Task DelegateTask_AddsTheDelegateWhoCanThenApprove()
    {
        await using var h = await WorkflowHarness.CreateAsync(db);
        var id = await SubmittedIdpAsync(h);
        var taskId = (await h.GetAsync(id)).OpenTask!.Id;

        await h.As("mgr-1").RunAsync(s => s.DelegateTaskAsync(taskId, "mgr-2", CancellationToken.None));
        await h.ActAsync("mgr-2", id, "Approve");

        var done = await h.GetAsync(id);
        done.CurrentState.ShouldBe("Approved");
        done.History.Select(x => x.Action).ShouldContain("Delegate");
    }

    [Fact]
    public async Task DelegateTask_WhenCallerIsNotAssigned_IsForbidden()
    {
        await using var h = await WorkflowHarness.CreateAsync(db);
        var id = await SubmittedIdpAsync(h);
        var taskId = (await h.GetAsync(id)).OpenTask!.Id;

        var error = await Should.ThrowAsync<WorkflowException>(() =>
            h.As("emp-1").RunAsync(s => s.DelegateTaskAsync(taskId, "emp-1", CancellationToken.None)));

        error.Kind.ShouldBe(WorkflowErrorKind.Forbidden);
    }

    [Fact]
    public async Task StandingDelegation_WhenActive_AddsDelegateToNewTasks()
    {
        await using var h = await WorkflowHarness.CreateAsync(db);
        await h.As("mgr-1").RunAsync(s => s.CreateDelegationAsync(
            "mgr-2", h.Clock.GetUtcNow().AddDays(-1), h.Clock.GetUtcNow().AddDays(7), CancellationToken.None));

        var id = await SubmittedIdpAsync(h);

        (await h.GetAsync(id)).OpenTask!.Assignees.ShouldBe(["mgr-1", "mgr-2"], ignoreOrder: true);
        (await h.As("mgr-2").RunAsync(s => s.GetInboxAsync(CancellationToken.None))).Count.ShouldBe(1);
    }

    [Fact]
    public async Task StandingDelegation_WhenExpiredOrRevoked_IsIgnored()
    {
        await using var h = await WorkflowHarness.CreateAsync(db);
        var expired = await h.As("mgr-1").RunAsync(s => s.CreateDelegationAsync(
            "mgr-2", h.Clock.GetUtcNow().AddDays(-9), h.Clock.GetUtcNow().AddDays(-2), CancellationToken.None));
        var revoked = await h.As("mgr-1").RunAsync(s => s.CreateDelegationAsync(
            "mgr-3", h.Clock.GetUtcNow().AddDays(-1), h.Clock.GetUtcNow().AddDays(7), CancellationToken.None));
        await h.As("mgr-1").RunAsync(s => s.RevokeDelegationAsync(revoked, CancellationToken.None));
        expired.ShouldNotBe(Guid.Empty);

        var id = await SubmittedIdpAsync(h);

        (await h.GetAsync(id)).OpenTask!.Assignees.ShouldBe(["mgr-1"]);
    }

    [Fact]
    public async Task RevokeDelegation_WhenNotTheOwner_IsForbidden()
    {
        await using var h = await WorkflowHarness.CreateAsync(db);
        var delegation = await h.As("mgr-1").RunAsync(s => s.CreateDelegationAsync(
            "mgr-2", h.Clock.GetUtcNow(), h.Clock.GetUtcNow().AddDays(1), CancellationToken.None));

        var error = await Should.ThrowAsync<WorkflowException>(() =>
            h.As("mgr-2").RunAsync(s => s.RevokeDelegationAsync(delegation, CancellationToken.None)));

        error.Kind.ShouldBe(WorkflowErrorKind.Forbidden);
    }

    [Fact]
    public async Task SlaTick_BeforeDeadline_DoesNothing()
    {
        await using var h = await WorkflowHarness.CreateAsync(db);
        var id = await SubmittedIdpAsync(h);
        h.Clock.Advance(TimeSpan.FromHours(71));

        (await h.RunSlaTickAsync()).ShouldBe(0);

        (await h.GetAsync(id)).OpenTask!.EscalationLevel.ShouldBe(0);
    }

    [Fact]
    public async Task SlaTick_AfterDeadline_EscalatesToHrAndHrCanApprove()
    {
        await using var h = await WorkflowHarness.CreateAsync(db);
        var id = await SubmittedIdpAsync(h);
        h.Clock.Advance(TimeSpan.FromHours(73));

        (await h.RunSlaTickAsync()).ShouldBe(1);

        var escalated = await h.GetAsync(id);
        escalated.OpenTask!.EscalationLevel.ShouldBe(1);
        escalated.OpenTask.Assignees.ShouldBe(["hr-1", "mgr-1"], ignoreOrder: true);
        escalated.OpenTask.DueAt.ShouldBe(h.Clock.GetUtcNow().AddHours(72));
        escalated.History[^1].Action.ShouldBe("Escalate");

        await h.ActAsync("hr-1", id, "Approve");
        (await h.GetAsync(id)).CurrentState.ShouldBe("Approved");
    }

    [Fact]
    public async Task SlaTick_WhenEscalationsUsedUpAndNoExpiryAction_MarksBreachedOnce()
    {
        await using var h = await WorkflowHarness.CreateAsync(db);
        await SubmittedIdpAsync(h);
        h.Clock.Advance(TimeSpan.FromHours(73));
        await h.RunSlaTickAsync();
        h.Clock.Advance(TimeSpan.FromHours(73));

        (await h.RunSlaTickAsync()).ShouldBe(1);
        (await h.RunSlaTickAsync()).ShouldBe(0);

        await h.DispatchOutboxAsync();
        (await h.OutboxEventTypesAsync()).Count(t => t == nameof(ApprovalTaskSlaBreached)).ShouldBe(1);
    }

    [Fact]
    public async Task SlaTick_WhenExpiryActionDefined_AppliesItAsSystemAfterEscalation()
    {
        await using var h = await WorkflowHarness.CreateAsync(db);
        var id = await h.As("facilitator").RunAsync(s => s.StartAsync(
            new StartWorkflowRequest(WorkflowCodes.CalibrationSignoff, "Session", "s-1", null, null), CancellationToken.None));
        await h.ActAsync("facilitator", id, "Submit");
        h.Clock.Advance(TimeSpan.FromHours(121));
        await h.RunSlaTickAsync();
        h.Clock.Advance(TimeSpan.FromHours(121));

        (await h.RunSlaTickAsync()).ShouldBe(1);

        var reopened = await h.GetAsync(id);
        reopened.CurrentState.ShouldBe("Open");
        reopened.History[^1].Actor.ShouldBe("system");
        reopened.History[^1].Comment.ShouldBe("SLA expired");
    }

    [Fact]
    public async Task SlaTick_WhenNobodyCanBeEscalatedTo_TreatsEscalationsAsUsedUpForEveryTask()
    {
        await using var h = await WorkflowHarness.CreateAsync(db);
        h.Directory.Managers["emp-2"] = "mgr-2";
        var first = await SubmittedIdpAsync(h);
        var second = await h.StartIdpAsync("emp-2", "plan-2");
        await h.ActAsync("emp-2", second, "Submit");
        h.Directory.Roles.Remove("HR_ADMIN");
        h.Clock.Advance(TimeSpan.FromHours(73));

        // No HR to escalate to: escalations count as used up, so both tasks are marked breached.
        (await h.RunSlaTickAsync()).ShouldBe(2);
        (await h.GetAsync(first)).OpenTask!.EscalationLevel.ShouldBe(0);

        // Both tasks left the due set, so the next tick has nothing to retry.
        (await h.RunSlaTickAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task HostedTickService_RunsTheSlaCheckOnTheTimer()
    {
        await using var h = await WorkflowHarness.CreateAsync(db, tickEnabled: true);
        var id = await SubmittedIdpAsync(h);
        h.Clock.Advance(TimeSpan.FromHours(73));
        var service = h.TickService();
        await service.StartAsync(CancellationToken.None);
        try
        {
            for (var i = 0; i < 100; i++)
            {
                h.Clock.Advance(TimeSpan.FromSeconds(60));
                await Task.Delay(50, TestContext.Current.CancellationToken);
                if ((await h.GetAsync(id)).OpenTask!.EscalationLevel > 0)
                {
                    break;
                }
            }

            (await h.GetAsync(id)).OpenTask!.EscalationLevel.ShouldBeGreaterThan(0);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Tick_IsOffWhenDisabledInConfiguration()
    {
        await using var h = await WorkflowHarness.CreateAsync(db, tickEnabled: false);

        h.Services.GetServices<IHostedService>().ShouldNotContain(s => s.GetType().Name == "SlaTickService");
        (await h.WithAsync<WorkflowDbContext, int>(c => c.Tasks.CountAsync())).ShouldBe(0);
    }
}
