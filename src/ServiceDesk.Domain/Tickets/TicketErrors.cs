using ServiceDesk.Domain.Common;

namespace ServiceDesk.Domain.Tickets;

public static class TicketErrors
{
    public static readonly Error TitleLength = new(
        "Validation.Title",
        "A cím hossza 5 és 200 karakter között legyen.",
        ErrorType.Validation);

    public static readonly Error DescriptionLength = new(
        "Validation.Description",
        "A leírás hossza 10 és 4000 karakter között legyen.",
        ErrorType.Validation);

    public static readonly Error ImpactInvalid = new(
        "Validation.Impact",
        "Válassz érvényes hatást.",
        ErrorType.Validation);

    public static readonly Error UrgencyInvalid = new(
        "Validation.Urgency",
        "Válassz érvényes sürgősséget.",
        ErrorType.Validation);

    public static readonly Error InvalidTransition = new(
        "Ticket.InvalidTransition",
        "A jegy jelenlegi állapotában ez a művelet nem hajtható végre.",
        ErrorType.BusinessRule);

    public static readonly Error ClosedCannotBeAssigned = new(
        "Ticket.ClosedCannotBeAssigned",
        "Lezárt jegy nem osztható ki és nem osztható át.",
        ErrorType.BusinessRule);

    public static readonly Error ClosedCannotBeModified = new(
        "Ticket.ClosedCannotBeModified",
        "Lezárt jegy adatai nem módosíthatók.",
        ErrorType.BusinessRule);

    public static readonly Error RecategorizationNotAllowed = new(
        "Ticket.RecategorizationNotAllowed",
        "Átkategorizálni csak új, kiosztott vagy folyamatban lévő jegyet lehet.",
        ErrorType.BusinessRule);
}
