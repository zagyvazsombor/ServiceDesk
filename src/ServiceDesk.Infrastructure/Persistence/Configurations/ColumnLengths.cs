namespace ServiceDesk.Infrastructure.Persistence.Configurations;

internal static class ColumnLengths
{
    // AGENTS.md §7: enums are stored as strings.
    public const int Enum = 30;

    // Same as the Identity key column AspNetUsers.Id (nvarchar(450)).
    public const int UserId = 450;
}
