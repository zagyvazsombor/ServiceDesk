using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ServiceDesk.Infrastructure.Identity;
using Shouldly;

namespace ServiceDesk.Infrastructure.Tests.Identity;

// AGENTS.md §8: the Identity settings of AddInfrastructure, checked through the real UserManager and SignInManager.
public sealed class IdentityConfigurationTests(DatabaseFixture database) : IntegrationTest(database)
{
    // Test values only.
    private const string Password = "Teszt-Jelszo-2026";
    private const string WrongPassword = "Rossz-Jelszo-2026";

    [Fact]
    public void AddInfrastructure_IdentityOptions_FollowSecurityRules()
    {
        // Act
        var options = Database.Services.GetRequiredService<IOptions<IdentityOptions>>().Value;

        // Assert
        options.SignIn.RequireConfirmedAccount.ShouldBeFalse();
        options.Password.RequiredLength.ShouldBe(10);
        options.Lockout.MaxFailedAccessAttempts.ShouldBe(5);
        options.Lockout.DefaultLockoutTimeSpan.ShouldBe(TimeSpan.FromMinutes(15));
        options.Lockout.AllowedForNewUsers.ShouldBeTrue();
    }

    [Fact]
    public async Task CreateAsync_PasswordWith9Characters_FailsWithPasswordTooShort()
    {
        // Arrange
        await using var scope = Database.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        // Act
        var result = await userManager.CreateAsync(NewUser(), "Abcdef-12");

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Errors.Select(error => error.Code).ShouldContain(nameof(IdentityErrorDescriber.PasswordTooShort));
    }

    [Fact]
    public async Task CreateAsync_PasswordWith10Characters_Succeeds()
    {
        // Arrange
        await using var scope = Database.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        // Act
        var result = await userManager.CreateAsync(NewUser(), "Abcdef-123");

        // Assert
        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task CheckPasswordSignInAsync_UnconfirmedEmail_Succeeds()
    {
        // Arrange
        await using var scope = Database.CreateScope();
        var user = await CreateUserAsync(scope);
        var signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<ApplicationUser>>();

        // Act
        var result = await signInManager.CheckPasswordSignInAsync(user, Password, lockoutOnFailure: true);

        // Assert
        user.EmailConfirmed.ShouldBeFalse();
        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task CheckPasswordSignInAsync_FiveWrongPasswords_LocksOutFor15Minutes()
    {
        // Arrange
        await using var scope = Database.CreateScope();
        var user = await CreateUserAsync(scope);
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<ApplicationUser>>();
        var startedAt = TimeProvider.System.GetUtcNow();

        // Act
        var wrongPasswordResults = new List<SignInResult>();
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            wrongPasswordResults.Add(await signInManager.CheckPasswordSignInAsync(user, WrongPassword, lockoutOnFailure: true));
        }

        var correctPasswordResult = await signInManager.CheckPasswordSignInAsync(user, Password, lockoutOnFailure: true);
        var finishedAt = TimeProvider.System.GetUtcNow();

        // Assert
        wrongPasswordResults.Take(4).ShouldAllBe(result => !result.Succeeded && !result.IsLockedOut);
        wrongPasswordResults[4].IsLockedOut.ShouldBeTrue();
        correctPasswordResult.IsLockedOut.ShouldBeTrue();
        var lockoutEnd = (await userManager.GetLockoutEndDateAsync(user)).ShouldNotBeNull();
        lockoutEnd.ShouldBeInRange(startedAt.AddMinutes(15), finishedAt.AddMinutes(15));
    }

    private static ApplicationUser NewUser()
    {
        var email = $"user-{Guid.NewGuid():N}@servicedesk.test";
        return new ApplicationUser { UserName = email, Email = email };
    }

    private static async Task<ApplicationUser> CreateUserAsync(AsyncServiceScope scope)
    {
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = NewUser();
        (await userManager.CreateAsync(user, Password)).Succeeded.ShouldBeTrue();
        return user;
    }
}
