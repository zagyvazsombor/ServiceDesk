using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceDesk.Domain.Agents;
using ServiceDesk.Infrastructure.Identity;

namespace ServiceDesk.Infrastructure.Persistence.Configurations;

internal sealed class AgentConfiguration : IEntityTypeConfiguration<Agent>
{
    public void Configure(EntityTypeBuilder<Agent> builder)
    {
        builder.ToTable("Agents");
        builder.HasKey(agent => agent.Id);
        builder.Property(agent => agent.Id).ValueGeneratedNever();

        builder.Property(agent => agent.UserId).HasMaxLength(ColumnLengths.UserId);
        builder.Property(agent => agent.DisplayName).HasMaxLength(Agent.DisplayNameMaxLength);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(agent => agent.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // The competencies belong to the Agent aggregate.
        builder.HasMany(agent => agent.Competencies)
            .WithOne()
            .HasForeignKey(competency => competency.AgentId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(agent => agent.Competencies)
            .HasField("_competencies")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Every Admin user has exactly one agent profile (AGENTS.md §10).
        builder.HasIndex(agent => agent.UserId).IsUnique();
    }
}
