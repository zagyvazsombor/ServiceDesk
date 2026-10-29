using FluentValidation;
using ServiceDesk.Domain.Tickets;

namespace ServiceDesk.Application.Tickets.SuggestCategory;

// The same limits as CreateTicketValidator (AGENTS.md §10): the model gets only text that could become a ticket, and
// an invalid request does not use up the rate limit, which is checked after the validation.
public sealed class SuggestCategoryValidator : AbstractValidator<SuggestCategoryCommand>
{
    public SuggestCategoryValidator()
    {
        RuleFor(command => command.Title)
            .Must(title => title?.Trim().Length is >= Ticket.TitleMinLength and <= Ticket.TitleMaxLength)
            .WithMessage(TicketErrors.TitleLength.Message);

        RuleFor(command => command.Description)
            .Must(description => description?.Trim().Length is >= Ticket.DescriptionMinLength and <= Ticket.DescriptionMaxLength)
            .WithMessage(TicketErrors.DescriptionLength.Message);
    }
}
