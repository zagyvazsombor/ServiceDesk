using FluentValidation;
using ServiceDesk.Domain.Tickets;

namespace ServiceDesk.Application.Tickets.CreateTicket;

// BR-01: the required fields and the limits of AGENTS.md §10. The text is trimmed the same way as in Ticket.Create.
public sealed class CreateTicketValidator : AbstractValidator<CreateTicketCommand>
{
    public CreateTicketValidator()
    {
        RuleFor(command => command.Title)
            .Must(title => title?.Trim().Length is >= Ticket.TitleMinLength and <= Ticket.TitleMaxLength)
            .WithMessage(TicketErrors.TitleLength.Message);

        RuleFor(command => command.Description)
            .Must(description => description?.Trim().Length is >= Ticket.DescriptionMinLength and <= Ticket.DescriptionMaxLength)
            .WithMessage(TicketErrors.DescriptionLength.Message);

        // WithMessage belongs to the rule before it, so both rules get the message.
        RuleFor(command => command.Impact)
            .NotNull()
            .WithMessage(TicketErrors.ImpactInvalid.Message)
            .IsInEnum()
            .WithMessage(TicketErrors.ImpactInvalid.Message);

        RuleFor(command => command.Urgency)
            .NotNull()
            .WithMessage(TicketErrors.UrgencyInvalid.Message)
            .IsInEnum()
            .WithMessage(TicketErrors.UrgencyInvalid.Message);

        RuleFor(command => command.CategoryId)
            .NotEmpty()
            .WithMessage("Válassz kategóriát.");

        // BR-09: optional, but when given it refers to a saved suggestion.
        RuleFor(command => command.SuggestionId)
            .NotEqual(Guid.Empty)
            .WithMessage("Érvénytelen kategóriajavaslat, kérj újat, vagy válassz kézzel.");
    }
}
