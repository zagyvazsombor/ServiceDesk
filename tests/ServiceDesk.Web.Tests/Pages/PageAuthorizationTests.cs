using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Web.Components.Pages.Admin;
using ServiceDesk.Web.Components.Pages.Tickets;
using Shouldly;

namespace ServiceDesk.Web.Tests.Pages;

// AGENTS.md §8, level 1: the route and the required role of every page; the AuthorizeRouteView enforces them.
public sealed class PageAuthorizationTests
{
    public static TheoryData<Type, string, string?> ProtectedPages => new()
    {
        { typeof(NewTicket), "/tickets/new", Roles.Reporter },
        { typeof(MyTickets), "/tickets/my", Roles.Reporter },
        // Any signed-in user; the resource check (BR-02) belongs to the use case.
        { typeof(TicketDetails), "/tickets/{Id:guid}", null },
        { typeof(TicketQueue), "/admin/queue", Roles.Admin },
        { typeof(Categories), "/admin/categories", Roles.Admin },
        { typeof(Agents), "/admin/agents", Roles.Admin },
    };

    [Theory]
    [MemberData(nameof(ProtectedPages))]
    public void AuthorizeAttribute_ProtectedPage_RequiresSignInAndExpectedRole(Type pageType, string route, string? roles)
    {
        // Act
        var routeAttribute = pageType.GetCustomAttribute<RouteAttribute>();
        var authorizeAttribute = pageType.GetCustomAttribute<AuthorizeAttribute>();

        // Assert
        routeAttribute.ShouldNotBeNull().Template.ShouldBe(route);
        authorizeAttribute.ShouldNotBeNull().Roles.ShouldBe(roles);
    }
}
