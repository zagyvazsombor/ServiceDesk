using System.Globalization;
using MudBlazor;
using ServiceDesk.Domain.CategorySuggestions;
using ServiceDesk.Domain.Tickets;

namespace ServiceDesk.Web.Components.Tickets;

// AGENTS.md §6: the Hungarian texts, chip colors and date format of the ticket values. Presentation only; the values
// themselves (e.g. the priority) come from the use cases.
public static class TicketLabels
{
    public const string NoAssignee = "Nincs kiosztva";

    private static readonly CultureInfo HungarianCulture = CultureInfo.GetCultureInfo("hu-HU");

    // The stored times are UTC; they are shown in Hungarian time, independently of the time zone of the server.
    private static readonly TimeZoneInfo BudapestTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Budapest");

    public static string StatusText(TicketStatus status) => status switch
    {
        TicketStatus.New => "Új",
        TicketStatus.Assigned => "Kiosztva",
        TicketStatus.InProgress => "Folyamatban",
        TicketStatus.Resolved => "Megoldva",
        TicketStatus.Closed => "Lezárva",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown status."),
    };

    public static Color StatusColor(TicketStatus status) => status switch
    {
        TicketStatus.New => Color.Info,
        TicketStatus.Assigned => Color.Primary,
        TicketStatus.InProgress => Color.Warning,
        TicketStatus.Resolved => Color.Success,
        TicketStatus.Closed => Color.Default,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown status."),
    };

    public static string PriorityText(Priority priority) => priority switch
    {
        Priority.Critical => "Kritikus",
        Priority.High => "Magas",
        Priority.Medium => "Közepes",
        Priority.Low => "Alacsony",
        Priority.Planning => "Tervezhető",
        _ => throw new ArgumentOutOfRangeException(nameof(priority), priority, "Unknown priority."),
    };

    public static Color PriorityColor(Priority priority) => priority switch
    {
        Priority.Critical => Color.Error,
        Priority.High => Color.Warning,
        Priority.Medium => Color.Info,
        Priority.Low or Priority.Planning => Color.Default,
        _ => throw new ArgumentOutOfRangeException(nameof(priority), priority, "Unknown priority."),
    };

    public static string ImpactText(Impact impact) => impact switch
    {
        Impact.High => "Magas",
        Impact.Medium => "Közepes",
        Impact.Low => "Alacsony",
        _ => throw new ArgumentOutOfRangeException(nameof(impact), impact, "Unknown impact."),
    };

    public static string UrgencyText(Urgency urgency) => urgency switch
    {
        Urgency.High => "Magas",
        Urgency.Medium => "Közepes",
        Urgency.Low => "Alacsony",
        _ => throw new ArgumentOutOfRangeException(nameof(urgency), urgency, "Unknown urgency."),
    };

    public static string ActionText(TicketHistoryAction action) => action switch
    {
        TicketHistoryAction.Created => "Jegy létrehozva",
        TicketHistoryAction.Assigned => "Kiosztás",
        TicketHistoryAction.Reassigned => "Átosztás",
        TicketHistoryAction.Unassigned => "Felelős eltávolítva",
        TicketHistoryAction.StatusChanged => "Státuszváltás",
        TicketHistoryAction.Recategorized => "Átkategorizálás",
        TicketHistoryAction.ImpactChanged => "Hatás módosítása",
        TicketHistoryAction.UrgencyChanged => "Sürgősség módosítása",
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown history action."),
    };

    // The status, impact and urgency entries store the enum name (see Ticket); the agent and category names are
    // already resolved by the query and stay as they are.
    public static string? HistoryValueText(TicketHistoryAction action, string? value) => action switch
    {
        TicketHistoryAction.Created or TicketHistoryAction.StatusChanged
            when TryParseDefined(value, out TicketStatus status) => StatusText(status),
        TicketHistoryAction.ImpactChanged when TryParseDefined(value, out Impact impact) => ImpactText(impact),
        TicketHistoryAction.UrgencyChanged when TryParseDefined(value, out Urgency urgency) => UrgencyText(urgency),
        _ => value,
    };

    // "old → new"; a missing old value shows only the new one, a missing new value (e.g. a removed assignee) a dash.
    public static string? HistoryChangeText(TicketHistoryAction action, string? oldValue, string? newValue)
    {
        var oldText = HistoryValueText(action, oldValue);
        var newText = HistoryValueText(action, newValue);
        return (oldText, newText) switch
        {
            (null, null) => null,
            (null, _) => newText,
            _ => $"{oldText} → {newText ?? "–"}",
        };
    }

    // BR-09: the decision of the Reporter about the AI suggestion; a ticket only ever shows Accepted or Overridden.
    public static string SuggestionDecisionText(CategorySuggestionDecision decision) => decision switch
    {
        CategorySuggestionDecision.Pending => "Döntésre vár",
        CategorySuggestionDecision.Accepted => "Elfogadva",
        CategorySuggestionDecision.Overridden => "Felülbírálva",
        _ => throw new ArgumentOutOfRangeException(nameof(decision), decision, "Unknown suggestion decision."),
    };

    public static Color SuggestionDecisionColor(CategorySuggestionDecision decision) => decision switch
    {
        CategorySuggestionDecision.Pending => Color.Default,
        CategorySuggestionDecision.Accepted => Color.Success,
        CategorySuggestionDecision.Overridden => Color.Warning,
        _ => throw new ArgumentOutOfRangeException(nameof(decision), decision, "Unknown suggestion decision."),
    };

    // The confidence of the model (0–1) as a whole percentage.
    public static string ConfidenceText(double confidence) => confidence.ToString("P0", HungarianCulture);

    // An inactive category or agent can still be on tickets (BR-12, BR-13), so it stays selectable with a marker.
    public static string NameWithActiveState(string name, bool isActive) => isActive ? name : $"{name} (inaktív)";

    public static string FormatDateTime(DateTimeOffset value) =>
        TimeZoneInfo.ConvertTime(value, BudapestTimeZone).ToString("g", HungarianCulture);

    private static bool TryParseDefined<TEnum>(string? value, out TEnum result)
        where TEnum : struct, Enum =>
        Enum.TryParse(value, ignoreCase: false, out result) && Enum.IsDefined(result);
}
