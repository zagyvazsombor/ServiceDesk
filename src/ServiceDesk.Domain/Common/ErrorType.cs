namespace ServiceDesk.Domain.Common;

public enum ErrorType
{
    Validation,
    NotFound,
    Forbidden,
    Conflict,
    BusinessRule,
    Unavailable,
}
