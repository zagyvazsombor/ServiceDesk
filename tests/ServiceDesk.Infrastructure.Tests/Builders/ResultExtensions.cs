using ServiceDesk.Domain.Common;

namespace ServiceDesk.Infrastructure.Tests.Builders;

internal static class ResultExtensions
{
    // A builder only produces valid test data; a failed domain operation is a mistake in the test arrangement.
    public static void EnsureSuccess(this Result result)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Test arrangement failed: {result.Error.Code}");
        }
    }
}
