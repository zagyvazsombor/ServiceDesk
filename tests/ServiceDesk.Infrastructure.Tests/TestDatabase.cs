namespace ServiceDesk.Infrastructure.Tests;

// AGENTS.md §7: the tests use their own LocalDB database, never the development one.
internal static class TestDatabase
{
    public const string ConnectionString =
        @"Server=(localdb)\MSSQLLocalDB;Database=ServiceDesk_Tests;Trusted_Connection=True;MultipleActiveResultSets=true";
}
