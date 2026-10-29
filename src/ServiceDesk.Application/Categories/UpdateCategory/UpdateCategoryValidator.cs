using FluentValidation;
using ServiceDesk.Domain.Categories;

namespace ServiceDesk.Application.Categories.UpdateCategory;

// The limits of AGENTS.md §10. The text is trimmed the same way as in Category.Rename and UpdateDescription.
public sealed class UpdateCategoryValidator : AbstractValidator<UpdateCategoryCommand>
{
    public UpdateCategoryValidator()
    {
        RuleFor(command => command.Name)
            .Must(name => name?.Trim().Length is >= Category.NameMinLength and <= Category.NameMaxLength)
            .WithMessage(CategoryErrors.NameLength.Message);

        RuleFor(command => command.Description)
            .Must(description => description is null || description.Trim().Length <= Category.DescriptionMaxLength)
            .WithMessage(CategoryErrors.DescriptionLength.Message);
    }
}
