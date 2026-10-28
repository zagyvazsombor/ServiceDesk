namespace ServiceDesk.Application.Common.Security;

// AGENTS.md §2: there are exactly two roles. Admin users come only from the seed (AGENTS.md §8).
public static class Roles
{
    public const string Reporter = "Reporter";

    public const string Admin = "Admin";
}
