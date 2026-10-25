using Talent.Shared.Tests;

// One shared real database. Parallelism is turned off in xunit.runner.json because tests reset the data.
[assembly: AssemblyFixture(typeof(DatabaseFixture))]
