using ServiceDesk.Domain.Categories;

namespace ServiceDesk.Infrastructure.Tests.Builders;

internal sealed class CategoryBuilder
{
    private string _code = "HARDWARE";

    public CategoryBuilder WithCode(string code)
    {
        _code = code;
        return this;
    }

    public Category Build() =>
        Category.Create(_code, "Tesztkategória", "Integrációs teszthez létrehozott kategória.").Value;
}
