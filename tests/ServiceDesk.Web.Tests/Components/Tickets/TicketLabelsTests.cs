using MudBlazor;
using ServiceDesk.Domain.CategorySuggestions;
using ServiceDesk.Domain.Tickets;
using ServiceDesk.Web.Components.Tickets;
using Shouldly;

namespace ServiceDesk.Web.Tests.Components.Tickets;

public sealed class TicketLabelsTests
{
    [Fact]
    public void Texts_EveryEnumValue_HasDistinctHungarianText()
    {
        // Act
        var statuses = Enum.GetValues<TicketStatus>().Select(TicketLabels.StatusText).ToList();
        var priorities = Enum.GetValues<Priority>().Select(TicketLabels.PriorityText).ToList();
        var impacts = Enum.GetValues<Impact>().Select(TicketLabels.ImpactText).ToList();
        var urgencies = Enum.GetValues<Urgency>().Select(TicketLabels.UrgencyText).ToList();
        var actions = Enum.GetValues<TicketHistoryAction>().Select(TicketLabels.ActionText).ToList();

        // Assert
        statuses.ShouldBe(["Új", "Kiosztva", "Folyamatban", "Megoldva", "Lezárva"]);
        priorities.ShouldBe(["Kritikus", "Magas", "Közepes", "Alacsony", "Tervezhető"]);
        impacts.ShouldBe(["Magas", "Közepes", "Alacsony"]);
        urgencies.ShouldBe(["Magas", "Közepes", "Alacsony"]);
        actions.ShouldBeUnique();
        actions.Count.ShouldBe(Enum.GetValues<TicketHistoryAction>().Length);
    }

    [Theory]
    [InlineData(Priority.Critical, Color.Error)]
    [InlineData(Priority.High, Color.Warning)]
    [InlineData(Priority.Medium, Color.Info)]
    [InlineData(Priority.Low, Color.Default)]
    [InlineData(Priority.Planning, Color.Default)]
    public void PriorityColor_Priority_ReturnsFixedColor(Priority priority, Color expected)
    {
        // Act
        var color = TicketLabels.PriorityColor(priority);

        // Assert
        color.ShouldBe(expected);
    }

    [Theory]
    [InlineData(TicketStatus.New, Color.Info)]
    [InlineData(TicketStatus.Assigned, Color.Primary)]
    [InlineData(TicketStatus.InProgress, Color.Warning)]
    [InlineData(TicketStatus.Resolved, Color.Success)]
    [InlineData(TicketStatus.Closed, Color.Default)]
    public void StatusColor_Status_ReturnsFixedColor(TicketStatus status, Color expected)
    {
        // Act
        var color = TicketLabels.StatusColor(status);

        // Assert
        color.ShouldBe(expected);
    }

    [Theory]
    [InlineData("2026-07-15T10:30:00+00:00", "2026. 07. 15. 12:30")] // summer time, UTC+2
    [InlineData("2026-01-15T10:30:00+00:00", "2026. 01. 15. 11:30")] // winter time, UTC+1
    [InlineData("2026-03-29T00:59:00+00:00", "2026. 03. 29. 1:59")] // last minute of winter time
    [InlineData("2026-03-29T01:00:00+00:00", "2026. 03. 29. 3:00")] // first minute of summer time
    public void FormatDateTime_UtcTime_ShowsBudapestTimeInHungarianFormat(string utc, string expected)
    {
        // Arrange
        var value = DateTimeOffset.Parse(utc, System.Globalization.CultureInfo.InvariantCulture);

        // Act
        var text = TicketLabels.FormatDateTime(value);

        // Assert
        text.ShouldBe(expected);
    }

    [Theory]
    [InlineData(TicketHistoryAction.Created, "New", "Új")]
    [InlineData(TicketHistoryAction.StatusChanged, "InProgress", "Folyamatban")]
    [InlineData(TicketHistoryAction.ImpactChanged, "High", "Magas")]
    [InlineData(TicketHistoryAction.UrgencyChanged, "Low", "Alacsony")]
    [InlineData(TicketHistoryAction.Assigned, "Kiss Anna", "Kiss Anna")]
    [InlineData(TicketHistoryAction.Recategorized, "Hardver", "Hardver")]
    [InlineData(TicketHistoryAction.StatusChanged, "Unknown", "Unknown")]
    [InlineData(TicketHistoryAction.StatusChanged, "7", "7")]
    public void HistoryValueText_StoredValue_ReturnsHungarianTextOrValue(TicketHistoryAction action, string value, string expected)
    {
        // Act
        var text = TicketLabels.HistoryValueText(action, value);

        // Assert
        text.ShouldBe(expected);
    }

    [Theory]
    [InlineData(TicketHistoryAction.StatusChanged, "Assigned", "InProgress", "Kiosztva → Folyamatban")]
    [InlineData(TicketHistoryAction.Created, null, "New", "Új")]
    [InlineData(TicketHistoryAction.Unassigned, "Kiss Anna", null, "Kiss Anna → –")]
    [InlineData(TicketHistoryAction.Assigned, null, null, null)]
    public void HistoryChangeText_OldAndNewValue_ReturnsChange(
        TicketHistoryAction action, string? oldValue, string? newValue, string? expected)
    {
        // Act
        var text = TicketLabels.HistoryChangeText(action, oldValue, newValue);

        // Assert
        text.ShouldBe(expected);
    }

    [Theory]
    [Trait("BR", "BR-09")]
    [InlineData(CategorySuggestionDecision.Pending, "Döntésre vár", Color.Default)]
    [InlineData(CategorySuggestionDecision.Accepted, "Elfogadva", Color.Success)]
    [InlineData(CategorySuggestionDecision.Overridden, "Felülbírálva", Color.Warning)]
    public void SuggestionDecision_Decision_ReturnsHungarianTextAndColor(
        CategorySuggestionDecision decision, string expectedText, Color expectedColor)
    {
        // Act
        var text = TicketLabels.SuggestionDecisionText(decision);
        var color = TicketLabels.SuggestionDecisionColor(decision);

        // Assert
        text.ShouldBe(expectedText);
        color.ShouldBe(expectedColor);
    }

    [Theory]
    [InlineData(0.0, "0%")]
    [InlineData(0.824, "82%")]
    [InlineData(0.826, "83%")]
    [InlineData(1.0, "100%")]
    public void ConfidenceText_Confidence_ReturnsWholePercentage(double confidence, string expected)
    {
        // Act
        var text = TicketLabels.ConfidenceText(confidence);

        // Assert
        text.ShouldBe(expected);
    }

    [Theory]
    [Trait("BR", "BR-12")]
    [Trait("BR", "BR-13")]
    [InlineData("Hardver", true, "Hardver")]
    [InlineData("Nagy Béla", false, "Nagy Béla (inaktív)")]
    public void NameWithActiveState_Name_MarksOnlyInactive(string name, bool isActive, string expected)
    {
        // Act
        var text = TicketLabels.NameWithActiveState(name, isActive);

        // Assert
        text.ShouldBe(expected);
    }
}
