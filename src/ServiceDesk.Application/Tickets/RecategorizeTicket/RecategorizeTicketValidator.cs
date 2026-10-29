using FluentValidation;

namespace ServiceDesk.Application.Tickets.RecategorizeTicket;

public sealed class RecategorizeTicketValidator : AbstractValidator<RecategorizeTicketCommand>
{
    public RecategorizeTicketValidator()
    {
        RuleFor(command => command.CategoryId)
            .NotEmpty()
            .WithMessage("Válassz kategóriát.");
    }
}
