using ServiceDesk.Domain.Agents;
using ServiceDesk.Domain.Categories;
using ServiceDesk.Infrastructure.Identity;

namespace ServiceDesk.Infrastructure.Tests.Builders;

internal sealed class AgentBuilder(ApplicationUser user)
{
    private readonly List<Category> _competencies = [];
    private bool _isActive = true;

    public AgentBuilder WithCompetencies(params Category[] categories)
    {
        _competencies.AddRange(categories);
        return this;
    }

    public AgentBuilder Inactive()
    {
        _isActive = false;
        return this;
    }

    public Agent Build()
    {
        var agent = Agent.Create(user.Id, "Kiss Anna").Value;
        agent.SetCompetencies(_competencies.Select(category => category.Id)).EnsureSuccess();
        if (!_isActive)
        {
            agent.Deactivate();
        }

        return agent;
    }
}
