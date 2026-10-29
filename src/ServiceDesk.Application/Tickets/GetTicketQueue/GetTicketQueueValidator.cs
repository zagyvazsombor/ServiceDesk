using FluentValidation;

namespace ServiceDesk.Application.Tickets.GetTicketQueue;

public sealed class GetTicketQueueValidator : AbstractValidator<GetTicketQueueQuery>
{
    public GetTicketQueueValidator()
    {
        RuleFor(query => query.PageNumber)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Az oldalszám legalább 1 legyen.");

        RuleFor(query => query.Status)
            .IsInEnum()
            .WithMessage("Válassz érvényes állapotot.");

        RuleFor(query => query.Priority)
            .IsInEnum()
            .WithMessage("Válassz érvényes prioritást.");

        RuleFor(query => query.AssigneeAgentId)
            .Null()
            .WithMessage("A felelős szerinti és a kiosztatlan jegyekre szűrés nem kapcsolható be egyszerre.")
            .When(query => query.UnassignedOnly);
    }
}
