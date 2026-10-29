using FluentValidation;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Domain.Categories;

namespace ServiceDesk.Application.Categories.CreateCategory;

// The limits of AGENTS.md §10 and the unique code. The text is trimmed the same way as in Category.Create.
public sealed class CreateCategoryValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryValidator(ICategoryRepository categoryRepository)
    {
        ArgumentNullException.ThrowIfNull(categoryRepository);

        // Only a well-formed code is looked up, so an invalid one needs no database round trip. The unique index
        // refuses a duplicate saved concurrently (UnitOfWork).
        RuleFor(command => command.Code)
            .Cascade(CascadeMode.Stop)
            .Must(code => code is not null && Category.IsValidCode(code.Trim()))
            .WithMessage(CategoryErrors.CodeInvalid.Message)
            .MustAsync(async (code, ct) => !await categoryRepository.CodeExistsAsync(code.Trim(), ct))
            .WithMessage(CategoryErrors.CodeAlreadyExists.Message);

        RuleFor(command => command.Name)
            .Must(name => name?.Trim().Length is >= Category.NameMinLength and <= Category.NameMaxLength)
            .WithMessage(CategoryErrors.NameLength.Message);

        RuleFor(command => command.Description)
            .Must(description => description is null || description.Trim().Length <= Category.DescriptionMaxLength)
            .WithMessage(CategoryErrors.DescriptionLength.Message);
    }
}
