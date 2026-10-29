using FluentValidation;

namespace ServiceDesk.Application.Tickets.AssignTicketManually;

public sealed class AssignTicketManuallyValidator : AbstractValidator<AssignTicketManuallyCommand>
{
    public AssignTicketManuallyValidator()
    {
        RuleFor(command => command.AgentId)
            .NotEmpty()
            .WithMessage("Válassz ügyintézőt.");
    }
}
