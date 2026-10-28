using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceDesk.Domain.Tickets;
using ServiceDesk.Infrastructure.Identity;

namespace ServiceDesk.Infrastructure.Persistence.Configurations;

internal sealed class TicketHistoryEntryConfiguration : IEntityTypeConfiguration<TicketHistoryEntry>
{
    // Shadow foreign key to the owning ticket; the relationship is configured in TicketConfiguration.
    public const string TicketIdProperty = "TicketId";

    // Named explicitly because the UnitOfWork recognizes a concurrent append by it.
    public const string SequenceIndexName = "IX_TicketHistoryEntries_TicketId_Sequence";

    public void Configure(EntityTypeBuilder<TicketHistoryEntry> builder)
    {
        builder.ToTable("TicketHistoryEntries");
        builder.HasKey(entry => entry.Id);
        // The domain sets the key, so an entry added to a loaded ticket is inserted, not updated.
        builder.Property(entry => entry.Id).ValueGeneratedNever();
        builder.Property<Guid>(TicketIdProperty);

        builder.Property(entry => entry.ActorUserId).HasMaxLength(ColumnLengths.UserId);
        builder.Property(entry => entry.Action).HasConversion<string>().HasMaxLength(ColumnLengths.Enum);
        builder.Property(entry => entry.OldValue).HasMaxLength(TicketHistoryEntry.ValueMaxLength);
        builder.Property(entry => entry.NewValue).HasMaxLength(TicketHistoryEntry.ValueMaxLength);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(entry => entry.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // The order of the entries within a ticket; queries sort by Sequence.
        builder.HasIndex(TicketIdProperty, nameof(TicketHistoryEntry.Sequence))
            .IsUnique()
            .HasDatabaseName(SequenceIndexName);
    }
}
