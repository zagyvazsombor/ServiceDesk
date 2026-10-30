namespace ServiceDesk.Infrastructure.Tests;

// AGENTS.md §7: the tests use their own LocalDB database, never the development one. Connect Timeout: the first start
// of a stopped LocalDB instance can take longer than the default 15 seconds.
internal static class TestDatabase
{
    public const string ConnectionString =
        @"Server=(localdb)\MSSQLLocalDB;Database=ServiceDesk_Tests;Trusted_Connection=True;MultipleActiveResultSets=true;Connect Timeout=60";
}
