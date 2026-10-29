using FluentValidation;
using ServiceDesk.Domain.Tickets;

namespace ServiceDesk.Application.Tickets.ChangeImpactAndUrgency;

public sealed class ChangeImpactAndUrgencyValidator : AbstractValidator<ChangeImpactAndUrgencyCommand>
{
    public ChangeImpactAndUrgencyValidator()
    {
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
    }
}
