using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceDesk.Domain.Categories;
using ServiceDesk.Domain.CategorySuggestions;
using ServiceDesk.Domain.Tickets;
using ServiceDesk.Infrastructure.Identity;

namespace ServiceDesk.Infrastructure.Persistence.Configurations;

internal sealed class CategorySuggestionConfiguration : IEntityTypeConfiguration<CategorySuggestion>
{
    public void Configure(EntityTypeBuilder<CategorySuggestion> builder)
    {
        builder.ToTable("CategorySuggestions");
        builder.HasKey(suggestion => suggestion.Id);
        builder.Property(suggestion => suggestion.Id).ValueGeneratedNever();

        builder.Property(suggestion => suggestion.RequestedByUserId).HasMaxLength(ColumnLengths.UserId);
        builder.Property(suggestion => suggestion.Reasoning).HasMaxLength(CategorySuggestion.ReasoningMaxLength);
        builder.Property(suggestion => suggestion.Model).HasMaxLength(CategorySuggestion.ModelMaxLength);
        builder.Property(suggestion => suggestion.PromptVersion).HasMaxLength(CategorySuggestion.PromptVersionMaxLength);
        builder.Property(suggestion => suggestion.Decision).HasConversion<string>().HasMaxLength(ColumnLengths.Enum);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(suggestion => suggestion.RequestedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(suggestion => suggestion.SuggestedCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Ticket>()
            .WithMany()
            .HasForeignKey(suggestion => suggestion.TicketId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
