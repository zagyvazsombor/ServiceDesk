using ServiceDesk.Domain.Categories;

namespace ServiceDesk.Infrastructure.Tests.Builders;

internal sealed class CategoryBuilder
{
    private string _code = "HARDWARE";
    private string _name = "Tesztkategória";

    public CategoryBuilder WithCode(string code)
    {
        _code = code;
        return this;
    }

    public CategoryBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public Category Build() =>
        Category.Create(_code, _name, "Integrációs teszthez létrehozott kategória.").Value;
}
