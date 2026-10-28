namespace ServiceDesk.Infrastructure.Persistence.Seed;

// The "Seed" configuration section; set only in appsettings.Development.json (AGENTS.md §7).
internal sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public string? DefaultPassword { get; set; }
}
