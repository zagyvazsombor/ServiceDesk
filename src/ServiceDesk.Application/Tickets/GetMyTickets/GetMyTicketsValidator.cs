using FluentValidation;

namespace ServiceDesk.Application.Tickets.GetMyTickets;

public sealed class GetMyTicketsValidator : AbstractValidator<GetMyTicketsQuery>
{
    public GetMyTicketsValidator()
    {
        RuleFor(query => query.PageNumber)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Az oldalszám legalább 1 legyen.");
    }
}
