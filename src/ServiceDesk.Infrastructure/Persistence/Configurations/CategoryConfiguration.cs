using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceDesk.Domain.Categories;

namespace ServiceDesk.Infrastructure.Persistence.Configurations;

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    // Named explicitly because the UnitOfWork recognizes a duplicate code by it; the name is the EF Core default.
    public const string CodeIndexName = "IX_Categories_Code";

    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.HasKey(category => category.Id);
        builder.Property(category => category.Id).ValueGeneratedNever();

        builder.Property(category => category.Code).HasMaxLength(Category.CodeMaxLength);
        builder.Property(category => category.Name).HasMaxLength(Category.NameMaxLength);
        builder.Property(category => category.Description).HasMaxLength(Category.DescriptionMaxLength);

        builder.HasIndex(category => category.Code).IsUnique().HasDatabaseName(CodeIndexName);
    }
}
