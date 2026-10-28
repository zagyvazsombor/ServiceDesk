using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceDesk.Domain.Agents;
using ServiceDesk.Domain.Categories;

namespace ServiceDesk.Infrastructure.Persistence.Configurations;

internal sealed class AgentCompetencyConfiguration : IEntityTypeConfiguration<AgentCompetency>
{
    public void Configure(EntityTypeBuilder<AgentCompetency> builder)
    {
        builder.ToTable("AgentCompetencies");
        builder.HasKey(competency => new { competency.AgentId, competency.CategoryId });

        // BR-13: a category is never deleted, only deactivated.
        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(competency => competency.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
