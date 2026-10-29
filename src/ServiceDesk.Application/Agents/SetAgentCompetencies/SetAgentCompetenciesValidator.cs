using FluentValidation;
using ServiceDesk.Domain.Agents;

namespace ServiceDesk.Application.Agents.SetAgentCompetencies;

public sealed class SetAgentCompetenciesValidator : AbstractValidator<SetAgentCompetenciesCommand>
{
    public SetAgentCompetenciesValidator()
    {
        RuleFor(command => command.CategoryIds)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .WithMessage(AgentErrors.CompetencyCategoryInvalid.Message)
            .Must(categoryIds => !categoryIds.Contains(Guid.Empty))
            .WithMessage(AgentErrors.CompetencyCategoryInvalid.Message);
    }
}
