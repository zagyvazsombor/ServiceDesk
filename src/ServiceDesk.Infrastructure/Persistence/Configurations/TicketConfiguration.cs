using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceDesk.Domain.Agents;
using ServiceDesk.Domain.Categories;
using ServiceDesk.Domain.Tickets;
using ServiceDesk.Infrastructure.Identity;

namespace ServiceDesk.Infrastructure.Persistence.Configurations;

internal sealed class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    private const string RowVersionProperty = "RowVersion";

    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.ToTable("Tickets");
        builder.HasKey(ticket => ticket.Id);
        builder.Property(ticket => ticket.Id).ValueGeneratedNever();

        builder.Property(ticket => ticket.ReporterId).HasMaxLength(ColumnLengths.UserId);
        builder.Property(ticket => ticket.Title).HasMaxLength(Ticket.TitleMaxLength);
        builder.Property(ticket => ticket.Description).HasMaxLength(Ticket.DescriptionMaxLength);
        builder.Property(ticket => ticket.Impact).HasConversion<string>().HasMaxLength(ColumnLengths.Enum);
        builder.Property(ticket => ticket.Urgency).HasConversion<string>().HasMaxLength(ColumnLengths.Enum);
        // BR-11: a column for filtering and sorting; only the domain sets it, from the Impact × Urgency matrix.
        builder.Property(ticket => ticket.Priority).HasConversion<string>().HasMaxLength(ColumnLengths.Enum);
        builder.Property(ticket => ticket.Status).HasConversion<string>().HasMaxLength(ColumnLengths.Enum);

        // AGENTS.md §7: optimistic concurrency token, unknown to the domain.
        builder.Property<byte[]>(RowVersionProperty).IsRowVersion();

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(ticket => ticket.ReporterId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(ticket => ticket.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Agent>()
            .WithMany()
            .HasForeignKey(ticket => ticket.AssigneeAgentId)
            .OnDelete(DeleteBehavior.Restrict);

        // BR-10: the history belongs to the Ticket aggregate.
        builder.HasMany(ticket => ticket.History)
            .WithOne()
            .HasForeignKey(TicketHistoryEntryConfiguration.TicketIdProperty)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(ticket => ticket.History)
            .HasField("_history")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(ticket => ticket.ReporterId);
        builder.HasIndex(ticket => ticket.AssigneeAgentId);
        builder.HasIndex(ticket => ticket.Status);
    }
}
