using ServiceDesk.Domain.Common;

namespace ServiceDesk.Domain.Agents;

public static class AgentErrors
{
    public static readonly Error DisplayNameLength = new(
        "Validation.DisplayName",
        "A megjelenítendő név hossza 2 és 100 karakter között legyen.",
        ErrorType.Validation);

    public static readonly Error CompetencyCategoryInvalid = new(
        "Validation.CategoryIds",
        "A kompetenciák között érvénytelen kategória szerepel.",
        ErrorType.Validation);

    public static readonly Error Inactive = new(
        "Agent.Inactive",
        "Inaktív ügyintéző nem kaphat jegyet.",
        ErrorType.BusinessRule);

    public static readonly Error MissingCompetency = new(
        "Agent.MissingCompetency",
        "Az ügyintéző nem rendelkezik a jegy kategóriájához tartozó kompetenciával.",
        ErrorType.BusinessRule);
}
