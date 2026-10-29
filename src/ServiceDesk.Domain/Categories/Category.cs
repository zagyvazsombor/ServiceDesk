using ServiceDesk.Domain.Common;

namespace ServiceDesk.Domain.Categories;

public sealed class Category
{
    public const int CodeMinLength = 2;
    public const int CodeMaxLength = 30;
    public const int NameMinLength = 2;
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 500;

    // Used by EF Core and the factory method.
    private Category()
    {
    }

    public Guid Id { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public static Result<Category> Create(string code, string name, string description)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(description);

        var trimmedCode = code.Trim();
        if (!IsValidCode(trimmedCode))
        {
            return CategoryErrors.CodeInvalid;
        }

        var trimmedName = name.Trim();
        if (!IsValidName(trimmedName))
        {
            return CategoryErrors.NameLength;
        }

        var trimmedDescription = description.Trim();
        if (!IsValidDescription(trimmedDescription))
        {
            return CategoryErrors.DescriptionLength;
        }

        return new Category
        {
            Id = Guid.CreateVersion7(),
            Code = trimmedCode,
            Name = trimmedName,
            Description = trimmedDescription,
            IsActive = true,
        };
    }

    public Result Rename(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var trimmedName = name.Trim();
        if (!IsValidName(trimmedName))
        {
            return CategoryErrors.NameLength;
        }

        Name = trimmedName;
        return Result.Success();
    }

    public Result UpdateDescription(string description)
    {
        ArgumentNullException.ThrowIfNull(description);

        var trimmedDescription = description.Trim();
        if (!IsValidDescription(trimmedDescription))
        {
            return CategoryErrors.DescriptionLength;
        }

        Description = trimmedDescription;
        return Result.Success();
    }

    public void Activate() => IsActive = true;

    // BR-13: a category is never deleted, only deactivated; existing tickets keep it.
    public void Deactivate() => IsActive = false;

    // ^[A-Z_]+$ with 2–30 characters. Public, so the Application validators check the same rule; the caller trims the
    // code first, as Create does.
    public static bool IsValidCode(string code)
    {
        ArgumentNullException.ThrowIfNull(code);

        return code.Length is >= CodeMinLength and <= CodeMaxLength
            && code.All(static character => character is (>= 'A' and <= 'Z') or '_');
    }

    private static bool IsValidName(string name) =>
        name.Length is >= NameMinLength and <= NameMaxLength;

    private static bool IsValidDescription(string description) =>
        description.Length <= DescriptionMaxLength;
}
