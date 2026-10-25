---
name: tdd-and-testing
description: Test-driven development rules and the testing stack - xUnit v3, Shouldly, NSubstitute, Bogus, WebApplicationFactory, Testcontainers, Respawn, NetArchTest, Stryker.NET, Vitest, React Testing Library, MSW and Playwright. Use before writing any production code, when writing tests, setting up CI, or checking a feature is done.
---

# TDD and testing

Development is **test-driven**. No production code without a failing test first.

## The cycle

1. **Red:** write 1 small failing test for the next behaviour.
2. **Green:** write the simplest code that passes.
3. **Refactor:** clean up while all tests stay green.
4. Repeat. Commit when green.

## Stack (all free)

| Layer | Tool |
|---|---|
| Test framework | **xUnit v3** |
| Assertions | **Shouldly** (FluentAssertions now needs a paid licence) |
| Fakes | **NSubstitute**, for plug points that reach outside: adapters, channels, the clock |
| Test data | **Bogus**, plus builders (`EmployeeBuilder`, `ConfigVersionBuilder`) |
| API tests | **WebApplicationFactory<Program>** |
| Database | **Testcontainers** PostgreSQL 16 + **Respawn** to reset data |
| Architecture | Boundary test + **NetArchTest** (see `dotnet-modular-backend`) |
| Mutation testing | **Stryker.NET** on Config, scoring, security and workflow |
| Frontend | **Vitest + React Testing Library + MSW** |
| End-to-end | **Playwright** |
| CI security | **OWASP ZAP** + dependency scanning |

## Test pyramid

- **Many unit tests:** rules, scoring, grid placement, state transitions.
- **Some integration tests:** API + real PostgreSQL. Scope filters, migrations, outbox, concurrency (409).
- **Few end-to-end tests:** 1 per key flow (submit, approve, return, calibrate, publish, IDP approval).

## Rules

- **Name tests by behaviour:** `Method_WhenCondition_ExpectedResult`.
- **Arrange, Act, Assert.** 1 behaviour per test.
- **Don't fake the database.** Use Testcontainers. Don't use EF Core InMemory.
- **Fake only what is outside our control:** source systems, SMTP, the clock (`FakeTimeProvider`).
- **Test return paths and edge cases**, not only the happy path.
- **Test the config limits:** 2 × 2 and 5 × 5 grids, 2 and 10 levels. Also test the defaults the client uses.
- **Test each plug implementation** against the same shared test set, so mock and real adapters behave alike.
- Every endpoint gets the security tests from `talent-security`.
- Tests are independent and run in any order.

## Example

```csharp
[Fact]
public void UpdatePayload_WhenVersionLocked_Throws()
{
    var version = new ConfigVersionBuilder().Published().Build();
    version.Lock();

    Should.Throw<ConfigVersionLockedException>(() => version.UpdatePayload("{}"));
}
```

## CI gates

- All tests pass. The boundary test passes. `dotnet format` passes.
- No high-severity findings from ZAP or the dependency scan.
- Stryker.NET mutation score is tracked for core modules. Don't chase line coverage numbers.
- Before go-live: a load test against the §5 targets (2 s screens, nine-box under 3 s, 10% of users at once).
