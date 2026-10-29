using ServiceDesk.Domain.Common;

namespace ServiceDesk.Domain.Categories;

public static class CategoryErrors
{
    public static readonly Error CodeInvalid = new(
        "Validation.Code",
        "A kód 2–30 karakter hosszú lehet, és csak nagybetűt (A–Z) és aláhúzásjelet tartalmazhat.",
        ErrorType.Validation);

    public static readonly Error NameLength = new(
        "Validation.Name",
        "A név hossza 2 és 100 karakter között legyen.",
        ErrorType.Validation);

    public static readonly Error DescriptionLength = new(
        "Validation.Description",
        "A leírás legfeljebb 500 karakter lehet.",
        ErrorType.Validation);

    public static readonly Error NotFound = new(
        "Category.NotFound",
        "A kategória nem található.",
        ErrorType.NotFound);

    public static readonly Error Inactive = new(
        "Category.Inactive",
        "Inaktív kategória nem választható.",
        ErrorType.BusinessRule);
}
