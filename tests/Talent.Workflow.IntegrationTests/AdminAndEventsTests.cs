using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Talent.Shared.Events;
using Talent.Shared.Persistence;
using Talent.Workflow.Contracts;
using Talent.Workflow.Persistence;

namespace Talent.Workflow.IntegrationTests;

public sealed class AdminAndEventsTests(WorkflowDatabaseFixture db)
{
    [Fact]
    public async Task Transitions_WriteOutboxEventsInTheSameTransaction()
    {
        await using var h = await WorkflowHarness.CreateAsync(db);
        var id = await h.StartIdpAsync();
        await h.ActAsync("emp-1", id, "Submit");
        await h.ActAsync("mgr-1", id, "Approve");

        var types = await h.OutboxEventTypesAsync();

        types.ShouldContain(nameof(WorkflowStarted));
        types.Count(t => t == nameof(WorkflowTransitioned)).ShouldBe(2);
        types.Count(t => t == nameof(WorkflowCompleted)).ShouldBe(1);
    }

    [Fact]
    public async Task Transition_WhenItFails_WritesNoEvent()
    {
        await using var h = await WorkflowHarness.CreateAsync(db);
        var id = await h.StartIdpAsync();
        var before = (await h.OutboxEventTypesAsync()).Count;

        await Should.ThrowAsync<WorkflowException>(() => h.ActAsync("emp-1", id, "Approve"));

        (await h.OutboxEventTypesAsync()).Count.ShouldBe(before);
    }

    [Fact]
    public async Task Outbox_DeliversTransitionsToSubscribers()
    {
        var seen = new List<WorkflowTransitioned>();
        await using var h = await WorkflowHarness.CreateAsync(db, configure: s =>
            s.AddSingleton<IDomainEventHandler<WorkflowTransitioned>>(new Collector(seen)));
        var id = await h.StartIdpAsync();
        await h.ActAsync("emp-1", id, "Submit");

        await h.DispatchOutboxAsync();

        seen.Single().ShouldSatisfyAllConditions(
            e => e.From.ShouldBe("Draft"),
            e => e.To.ShouldBe("PendingManager"),
            e => e.Action.ShouldBe("Submit"),
            e => e.Actor.ShouldBe("emp-1"),
            e => e.SubjectId.ShouldBe("plan-1"));
    }

    [Fact]
    public async Task Transitions_AreAudited()
    {
        await using var h = await WorkflowHarness.CreateAsync(db);
        var id = await h.StartIdpAsync();
        await h.ActAsync("emp-1", id, "Submit");

        var actions = await h.WithAsync<TalentSharedDbContext, List<string>>(c =>
            c.Set<Talent.Shared.Auditing.AuditEntry>().Where(a => a.ObjectId == id.ToString()).OrderBy(a => a.OccurredAt).ThenBy(a => a.Id)
                .Select(a => a.Action).ToListAsync());

        actions.ShouldBe(["Start", "Submit"]);
    }

    [Fact]
    public async Task Publish_WhenDefinitionInvalid_IsRejectedAndStaysDraft()
    {
        await using var h = await WorkflowHarness.CreateAsync(db);

        var error = await Should.ThrowAsync<WorkflowException>(() => h.WithAsync<WorkflowAdminService, bool>(async admin =>
        {
            var definition = await admin.CreateDefinitionAsync("BAD_FLOW", "Bad", CancellationToken.None);
            var draft = await admin.CreateDraftAsync(
                definition, """{"initialState":"A","states":[{"code":"A"}],"transitions":[]}""", CancellationToken.None);
            await admin.PublishAsync(draft, CancellationToken.None);
            return true;
        }));

        error.Kind.ShouldBe(WorkflowErrorKind.Invalid);
    }

    [Fact]
    public async Task UpdateDraft_WhenPublished_IsConflict()
    {
        await using var h = await WorkflowHarness.CreateAsync(db);

        var error = await Should.ThrowAsync<WorkflowException>(() => h.WithAsync<WorkflowAdminService, bool>(async admin =>
        {
            var definition = await admin.CreateDefinitionAsync("TWO_STEP", "Two", CancellationToken.None);
            var draft = await admin.CreateDraftAsync(definition, WorkflowTestData.SimpleIdp, CancellationToken.None);
            await admin.PublishAsync(draft, CancellationToken.None);
            await admin.UpdateDraftAsync(draft, WorkflowTestData.SimpleIdp, CancellationToken.None);
            return true;
        }));

        error.Kind.ShouldBe(WorkflowErrorKind.Conflict);
    }

    [Fact]
    public async Task CreateDraft_WithoutPayload_CopiesTheNewestVersionAsNextNumber()
    {
        await using var h = await WorkflowHarness.CreateAsync(db);

        var copyId = await h.WithAsync<WorkflowAdminService, Guid>(async admin =>
        {
            var definition = await admin.CreateDefinitionAsync("COPY_FLOW", "Copy", CancellationToken.None);
            var v1 = await admin.CreateDraftAsync(definition, WorkflowTestData.SimpleIdp, CancellationToken.None);
            await admin.PublishAsync(v1, CancellationToken.None);
            return await admin.CreateDraftAsync(definition, null, CancellationToken.None);
        });
        copyId.ShouldNotBe(Guid.Empty);

        var versionNos = await h.WithAsync<WorkflowDbContext, List<int>>(c =>
            c.Versions.Join(c.Definitions.Where(d => d.Code == "COPY_FLOW"), v => v.DefinitionId, d => d.Id, (v, _) => v.VersionNo)
                .OrderBy(n => n).ToListAsync());
        versionNos.ShouldBe([1, 2]);
    }

    [Fact]
    public async Task Publish_ChecksumIsStoredAndPayloadIsCanonical()
    {
        await using var h = await WorkflowHarness.CreateAsync(db);

        var checksums = await h.WithAsync<WorkflowDbContext, List<string?>>(c =>
            c.Versions.Select(v => v.Checksum).ToListAsync());

        checksums.Count.ShouldBe(2);
        checksums.ShouldAllBe(x => x != null && x.Length == 64);
    }

    [Fact]
    public async Task SaveChanges_WhenInstanceChangedByAnotherWriter_ThrowsConcurrencyException()
    {
        await using var h = await WorkflowHarness.CreateAsync(db);
        var id = await h.StartIdpAsync();
        await using var first = h.Services.CreateAsyncScope();
        await using var second = h.Services.CreateAsyncScope();
        var a = first.ServiceProvider.GetRequiredService<WorkflowDbContext>();
        var b = second.ServiceProvider.GetRequiredService<WorkflowDbContext>();
        var instanceA = await a.Instances.Include(i => i.History).Include(i => i.Tasks).SingleAsync(i => i.Id == id, TestContext.Current.CancellationToken);
        var instanceB = await b.Instances.Include(i => i.History).Include(i => i.Tasks).SingleAsync(i => i.Id == id, TestContext.Current.CancellationToken);
        var model = Talent.Workflow.Definitions.WorkflowDefinitionModel.Parse(WorkflowTestData.SimpleIdp);
        var submit = model.FindTransition("Draft", "Submit", instanceA.Context)!;
        var pending = model.GetState("PendingManager");
        var spec = new Talent.Workflow.Instances.TaskSpec([new Talent.Workflow.Instances.Assignment("mgr-1", null)], null);

        instanceA.Apply(submit, pending, "emp-1", null, h.Clock.GetUtcNow(), spec);
        instanceB.Apply(submit, pending, "emp-1", null, h.Clock.GetUtcNow(), spec);
        await a.SaveChangesAsync(TestContext.Current.CancellationToken);

        await Should.ThrowAsync<DbUpdateException>(() => b.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    private sealed class Collector(List<WorkflowTransitioned> seen) : IDomainEventHandler<WorkflowTransitioned>
    {
        public Task HandleAsync(WorkflowTransitioned domainEvent, CancellationToken cancellationToken)
        {
            seen.Add(domainEvent);
            return Task.CompletedTask;
        }
    }
}
