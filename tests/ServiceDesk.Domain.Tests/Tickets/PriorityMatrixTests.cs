using ServiceDesk.Domain.Tickets;
using Shouldly;

namespace ServiceDesk.Domain.Tests.Tickets;

public sealed class PriorityMatrixTests
{
    [Theory]
    [Trait("BR", "BR-11")]
    [InlineData(Impact.High, Urgency.High, Priority.Critical)]
    [InlineData(Impact.High, Urgency.Medium, Priority.High)]
    [InlineData(Impact.High, Urgency.Low, Priority.Medium)]
    [InlineData(Impact.Medium, Urgency.High, Priority.High)]
    [InlineData(Impact.Medium, Urgency.Medium, Priority.Medium)]
    [InlineData(Impact.Medium, Urgency.Low, Priority.Low)]
    [InlineData(Impact.Low, Urgency.High, Priority.Medium)]
    [InlineData(Impact.Low, Urgency.Medium, Priority.Low)]
    [InlineData(Impact.Low, Urgency.Low, Priority.Planning)]
    public void Calculate_MatrixCell_ReturnsPriority(Impact impact, Urgency urgency, Priority expected)
    {
        var priority = PriorityMatrix.Calculate(impact, urgency);

        priority.ShouldBe(expected);
    }

    [Theory]
    [Trait("BR", "BR-11")]
    [InlineData((Impact)99, Urgency.High)]
    [InlineData(Impact.High, (Urgency)99)]
    public void Calculate_UndefinedValue_ThrowsArgumentOutOfRangeException(Impact impact, Urgency urgency)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => PriorityMatrix.Calculate(impact, urgency));
    }

    [Theory]
    [Trait("BR", "BR-05")]
    [InlineData(Priority.Critical, 5)]
    [InlineData(Priority.High, 4)]
    [InlineData(Priority.Medium, 3)]
    [InlineData(Priority.Low, 2)]
    [InlineData(Priority.Planning, 1)]
    public void GetLoadWeight_Priority_ReturnsWeight(Priority priority, int expected)
    {
        var weight = PriorityMatrix.GetLoadWeight(priority);

        weight.ShouldBe(expected);
    }
}
