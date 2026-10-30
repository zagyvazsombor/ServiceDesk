using System.Reflection;
using ServiceDesk.Application.Tickets.CreateTicket;
using ServiceDesk.Application.Tickets.GetActiveCategories;
using ServiceDesk.Application.Tickets.GetMyTickets;
using ServiceDesk.Application.Tickets.GetTicketDetails;
using ServiceDesk.Application.Tickets.SuggestCategory;
using ServiceDesk.Application.Users.GetCurrentUserProfile;
using Shouldly;

namespace ServiceDesk.Application.Tests.Common.Security;

// AGENTS.md §8, over-posting: what a Reporter sends holds only the fields the Reporter may set. The reporter, the user,
// the status, the assignee and the priority always come from the server (ICurrentUser, the domain), never from the client.
public sealed class ReporterInputShapeTests
{
    private static readonly string[] ServerControlledFields =
        ["ReporterId", "UserId", "RequestedByUserId", "Status", "AssigneeAgentId", "Priority", "Decision"];

    public static TheoryData<Type> ReporterInputs =>
    [
        typeof(CreateTicketCommand),
        typeof(SuggestCategoryCommand),
        typeof(GetMyTicketsQuery),
        typeof(GetTicketDetailsQuery),
        typeof(GetActiveCategoriesQuery),
        typeof(GetCurrentUserProfileQuery),
    ];

    [Fact]
    [Trait("BR", "BR-01")]
    [Trait("BR", "BR-11")]
    public void CreateTicketCommand_Properties_AreOnlyTheFieldsOfTheReporter()
    {
        // Act
        var properties = PublicInstanceProperties(typeof(CreateTicketCommand));

        // Assert: BR-01 the reporter is the signed-in user; BR-11 the priority is calculated, never set.
        properties.ShouldBe(
            [
                nameof(CreateTicketCommand.Title),
                nameof(CreateTicketCommand.Description),
                nameof(CreateTicketCommand.Impact),
                nameof(CreateTicketCommand.Urgency),
                nameof(CreateTicketCommand.CategoryId),
                nameof(CreateTicketCommand.SuggestionId),
            ],
            ignoreOrder: true);
    }

    [Theory]
    [MemberData(nameof(ReporterInputs))]
    [Trait("BR", "BR-02")]
    public void ReporterInput_Properties_ContainNoServerControlledField(Type inputType)
    {
        // Act
        var properties = PublicInstanceProperties(inputType);

        // Assert
        properties.ShouldNotContain(property => ServerControlledFields.Contains(property));
    }

    // The positional properties of the record; EqualityContract is protected, so it is not listed.
    private static string[] PublicInstanceProperties(Type type) =>
        [.. type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(property => property.Name)];
}
