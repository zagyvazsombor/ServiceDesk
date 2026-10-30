# Követhetőség: üzleti szabály → megvalósítás → tesztek

Állapot: 2026-10-30, a P-9B lépés után. Az üzleti szabályok szövege az [AGENTS.md](../AGENTS.md) §10-ben van.

- **Megvalósítás:** a szabályt érvényesítő fő osztályok és metódusok, rétegenként. A lista a forrás `// BR-xx`
  jelöléseiből indul (AGENTS.md §13), és kiegészül a szabályt érvényesítő, de nem jelölt pontokkal (pl. hibakatalógus,
  `UnitOfWork`). A DTO-k és a portok (interfészek) közül csak azok szerepelnek, amelyek maguk hordozzák a szabályt
  (pl. a commandból hiányzó mezők).
- **Tesztek:** minden `[Trait("BR", "BR-xx")]` jelölésű tesztmetódus, projektenként és tesztosztályonként. Egy metódus
  több szabályhoz is tartozhat. A tesztszám a szűrt futás eredménye, a `[Theory]` esetekkel együtt:

  ```bash
  dotnet test --solution ServiceDesk.slnx --filter "BR=BR-03"
  ```

- Az architektúra-szabályokat (AGENTS.md §4) nem BR-jelölés, hanem a `ServiceDesk.ArchitectureTests` projekt fedi le
  ([architecture.md](architecture.md)).

## Összesítés

| Szabály | Röviden | Tesztek (szűrt futás) | Jelölt tesztmetódusok | Tesztprojektek |
|---|---|---|---|---|
| [BR-01](#br-01) | Jegyet csak Bejelentő hoz létre | 38 | 26 | Domain.Tests, Application.Tests |
| [BR-02](#br-02) | A Bejelentő csak a saját jegyeit látja | 20 | 15 | Application.Tests, Infrastructure.Tests, Web.Tests |
| [BR-03](#br-03) | A lezárt jegy terminális | 82 | 27 | Domain.Tests, Application.Tests, Infrastructure.Tests, Web.Tests |
| [BR-04](#br-04) | Kiosztás csak aktív, kompetens ügyintézőnek | 27 | 22 | Domain.Tests, Application.Tests, Infrastructure.Tests, Web.Tests |
| [BR-05](#br-05) | Automatikus kiosztás a legkisebb terhelésűnek | 32 | 20 | Domain.Tests, Application.Tests, Infrastructure.Tests |
| [BR-06](#br-06) | Jogosult ügyintéző nélkül a jegy kiosztatlan marad | 25 | 16 | Domain.Tests, Application.Tests, Infrastructure.Tests, Web.Tests |
| [BR-07](#br-07) | Átkategorizálás | 70 | 16 | Domain.Tests, Application.Tests, Web.Tests |
| [BR-08](#br-08) | Az LLM csak létező, aktív kategóriát javasolhat | 41 | 23 | Domain.Tests, Application.Tests, Infrastructure.Tests, Web.Tests |
| [BR-09](#br-09) | Kategória csak emberi döntéssel | 37 | 33 | Domain.Tests, Application.Tests, Infrastructure.Tests, Web.Tests |
| [BR-10](#br-10) | Jegytörténet | 26 | 23 | Domain.Tests, Application.Tests, Infrastructure.Tests, Web.Tests |
| [BR-11](#br-11) | A prioritás a mátrixból számított | 23 | 12 | Domain.Tests, Application.Tests, Web.Tests |
| [BR-12](#br-12) | Ügyintéző inaktiválása | 25 | 21 | Domain.Tests, Application.Tests, Infrastructure.Tests, Web.Tests |
| [BR-13](#br-13) | Kategória csak inaktiválható | 53 | 46 | Domain.Tests, Application.Tests, Infrastructure.Tests, Web.Tests |

## BR-01

**Jegyet csak Bejelentő hoz létre.** Jegyet csak `Reporter` hozhat létre. Kötelező: cím, leírás, hatás, sürgősség, aktív kategória. A `ReporterId` a létrehozó, később nem változik.

### Megvalósítás

| Réteg | Típus | Metódus, tag |
|---|---|---|
| Domain | [`Ticket`](../src/ServiceDesk.Domain/Tickets/Ticket.cs) | `Create`, `ReporterId` |
| Application | [`CreateTicketHandler`](../src/ServiceDesk.Application/Tickets/CreateTicket/CreateTicketHandler.cs) | `HandleAsync` |
| Application | [`CreateTicketValidator`](../src/ServiceDesk.Application/Tickets/CreateTicket/CreateTicketValidator.cs) | a konstruktor szabályai: kötelező mezők, hosszkorlátok |
| Application | [`CreateTicketCommand`](../src/ServiceDesk.Application/Tickets/CreateTicket/CreateTicketCommand.cs) | nincs benne `ReporterId`, `Status`, `AssigneeAgentId`, `Priority` |
| Web | [`NewTicket`](../src/ServiceDesk.Web/Components/Pages/Tickets/NewTicket.razor) | `[Authorize(Roles = Roles.Reporter)]`, `SubmitAsync` |

### Tesztek – 38 teszt

- **Domain.Tests**
  - [`TicketCreateTests`](../tests/ServiceDesk.Domain.Tests/Tickets/TicketCreateTests.cs): `Create_ValidInput_ReturnsNewUnassignedTicket`, `Create_TitleLengthAtLimit_Succeeds`, `Create_TitleLengthOutOfRange_ReturnsTitleLengthError`, `Create_TitleWithSurroundingWhitespace_StoresTrimmedTitle`, `Create_TitleTooShortAfterTrim_ReturnsTitleLengthError`, `Create_DescriptionLengthAtLimit_Succeeds`, `Create_DescriptionLengthOutOfRange_ReturnsDescriptionLengthError`, `Create_WhitespaceOnlyDescription_ReturnsDescriptionLengthError`, `Create_UndefinedImpact_ReturnsImpactInvalidError`, `Create_UndefinedUrgency_ReturnsUrgencyInvalidError`, `Create_NullTitle_ThrowsArgumentNullException`, `Create_MissingReporterId_ThrowsArgumentException`
- **Application.Tests**
  - [`ReporterInputShapeTests`](../tests/ServiceDesk.Application.Tests/Common/Security/ReporterInputShapeTests.cs): `CreateTicketCommand_Properties_AreOnlyTheFieldsOfTheReporter`
  - [`CreateTicketHandlerTests`](../tests/ServiceDesk.Application.Tests/Tickets/CreateTicket/CreateTicketHandlerTests.cs): `HandleAsync_AnonymousUser_ReturnsForbiddenWithoutChanges`, `HandleAsync_Admin_ReturnsForbiddenWithoutChanges`, `HandleAsync_SignedInUserWithoutRole_ReturnsForbidden`, `HandleAsync_SeveralInvalidFields_ReturnsEveryFieldErrorWithoutChanges`, `HandleAsync_EligibleAgent_SavesAssignedTicketOnceAndReturnsItsId`
  - [`CreateTicketValidatorTests`](../tests/ServiceDesk.Application.Tests/Tickets/CreateTicket/CreateTicketValidatorTests.cs): `Validate_ValidCommand_HasNoErrors`, `Validate_TitleLength_FollowsLimits`, `Validate_DescriptionLength_FollowsLimits`, `Validate_TitleTooShortAfterTrim_ReturnsTitleError`, `Validate_NullTexts_ReturnsTitleAndDescriptionErrors`, `Validate_MissingImpactAndUrgency_ReturnsBothErrors`, `Validate_UndefinedImpactAndUrgency_ReturnsBothErrors`, `Validate_EmptyCategoryId_ReturnsCategoryError`

## BR-02

**A Bejelentő csak a saját jegyeit látja.** A `Reporter` csak a saját jegyeit listázhatja és nyithatja meg; idegen jegyre `NotFound`. Az `Admin` minden jegyet lát.

### Megvalósítás

| Réteg | Típus | Metódus, tag |
|---|---|---|
| Domain | [`TicketErrors`](../src/ServiceDesk.Domain/Tickets/TicketErrors.cs) | `NotFound` |
| Application | [`GetMyTicketsHandler`](../src/ServiceDesk.Application/Tickets/GetMyTickets/GetMyTicketsHandler.cs) | `HandleAsync` |
| Application | [`GetTicketDetailsHandler`](../src/ServiceDesk.Application/Tickets/GetTicketDetails/GetTicketDetailsHandler.cs) | `HandleAsync`, `LogReporterTicketNotFound` |
| Application | [`GetTicketQueueHandler`](../src/ServiceDesk.Application/Tickets/GetTicketQueue/GetTicketQueueHandler.cs) | `HandleAsync` |
| Infrastructure | [`TicketQueries`](../src/ServiceDesk.Infrastructure/Persistence/Queries/TicketQueries.cs) | `GetMyTicketsAsync`, `GetTicketDetailsAsync` |

### Tesztek – 20 teszt

- **Application.Tests**
  - [`ReporterInputShapeTests`](../tests/ServiceDesk.Application.Tests/Common/Security/ReporterInputShapeTests.cs): `ReporterInput_Properties_ContainNoServerControlledField`
  - [`GetMyTicketsHandlerTests`](../tests/ServiceDesk.Application.Tests/Tickets/GetMyTickets/GetMyTicketsHandlerTests.cs): `HandleAsync_Reporter_QueriesOwnTicketsWithPageSize20`
  - [`GetTicketDetailsHandlerTests`](../tests/ServiceDesk.Application.Tests/Tickets/GetTicketDetails/GetTicketDetailsHandlerTests.cs): `HandleAsync_ReporterForeignTicket_ReturnsNotFoundAndLogsWarningWithoutContent`, `HandleAsync_ReporterOwnTicket_ReturnsDetailsWithoutLog`, `HandleAsync_Admin_QueriesWithoutReporterFilter`
  - [`GetTicketQueueHandlerTests`](../tests/ServiceDesk.Application.Tests/Tickets/GetTicketQueue/GetTicketQueueHandlerTests.cs): `HandleAsync_Reporter_ReturnsForbiddenWithoutQuery`, `HandleAsync_AdminWithFilters_QueriesEveryTicketWithPageSize20`
- **Infrastructure.Tests**
  - [`TicketQueriesTests`](../tests/ServiceDesk.Infrastructure.Tests/Persistence/TicketQueriesTests.cs): `GetMyTicketsAsync_TicketsOfTwoReporters_ReturnsOnlyOwnNewestFirst`, `GetMyTicketsAsync_SecondPage_ReturnsRemainingTicketsWithTotalCount`, `GetMyTicketsAsync_PagePastTheLast_ReturnsNoItemsWithTotalCount`, `GetTicketDetailsAsync_ForeignTicketWithReporterFilter_ReturnsNull`, `GetTicketDetailsAsync_OwnNewTicketWithReporterFilter_ReturnsDetailsWithoutAssignee`
- **Web.Tests**
  - [`MyTicketsTests`](../tests/ServiceDesk.Web.Tests/Pages/Tickets/MyTicketsTests.cs): `Render_OwnTickets_ShowsRowsWithHungarianValues`
  - [`TicketDetailsAdminTests`](../tests/ServiceDesk.Web.Tests/Pages/Tickets/TicketDetailsAdminTests.cs): `Render_Reporter_DoesNotShowAdminPanel`
  - [`TicketDetailsTests`](../tests/ServiceDesk.Web.Tests/Pages/Tickets/TicketDetailsTests.cs): `Render_NotFoundResult_ShowsFriendlyNotFoundStateWithoutSnackbar`

## BR-03

**A lezárt jegy terminális.** A `Closed` jegy nem osztható ki, nem osztható át, és semmilyen adata nem módosítható.

### Megvalósítás

| Réteg | Típus | Metódus, tag |
|---|---|---|
| Domain | [`TicketWorkflow`](../src/ServiceDesk.Domain/Tickets/TicketWorkflow.cs) | `IsTerminal`, `CanAssign`, `CanChangeImpactAndUrgency` |
| Domain | [`Ticket`](../src/ServiceDesk.Domain/Tickets/Ticket.cs) | `AssignTo`, `ChangeImpactAndUrgency`, `Recategorize`, `Transition` |
| Domain | [`TicketErrors`](../src/ServiceDesk.Domain/Tickets/TicketErrors.cs) | `ClosedCannotBeAssigned`, `ClosedCannotBeModified` |
| Application | [`AssignTicketManuallyHandler`](../src/ServiceDesk.Application/Tickets/AssignTicketManually/AssignTicketManuallyHandler.cs) | `HandleAsync` |
| Application | [`GetAssignableAgentsHandler`](../src/ServiceDesk.Application/Tickets/GetAssignableAgents/GetAssignableAgentsHandler.cs) | `HandleAsync` |
| Application | [`ChangeImpactAndUrgencyHandler`](../src/ServiceDesk.Application/Tickets/ChangeImpactAndUrgency/ChangeImpactAndUrgencyHandler.cs) | `HandleAsync` |
| Application | [`RecategorizeTicketHandler`](../src/ServiceDesk.Application/Tickets/RecategorizeTicket/RecategorizeTicketHandler.cs) | `HandleAsync` |
| Application | [`RetryAutoAssignHandler`](../src/ServiceDesk.Application/Tickets/RetryAutoAssign/RetryAutoAssignHandler.cs) | `HandleAsync` |
| Application | [`CloseTicketHandler`](../src/ServiceDesk.Application/Tickets/CloseTicket/CloseTicketHandler.cs) | `HandleAsync` |
| Application | [`TicketAssignmentService`](../src/ServiceDesk.Application/Tickets/TicketAssignmentService.cs) | `AutoAssignAsync` |
| Infrastructure | [`TicketQueries`](../src/ServiceDesk.Infrastructure/Persistence/Queries/TicketQueries.cs) | `GetTicketQueueAsync` |
| Infrastructure | [`UnitOfWork`](../src/ServiceDesk.Infrastructure/Persistence/UnitOfWork.cs) | `SaveChangesAsync` |
| Web | [`TicketAdminPanel`](../src/ServiceDesk.Web/Components/Tickets/TicketAdminPanel.razor) | `IsTerminal`, `CloseAsync` |

### Tesztek – 82 teszt

- **Domain.Tests**
  - [`TicketAssignmentTests`](../tests/ServiceDesk.Domain.Tests/Tickets/TicketAssignmentTests.cs): `AssignTo_ClosedTicket_ReturnsClosedCannotBeAssignedError`, `AssignTo_ClosedTicketAndInactiveAgent_ReturnsClosedErrorFirst`, `AssignTo_NotClosedTicketWithAssignee_ReassignsWithoutStatusChange`
  - [`TicketChangeTests`](../tests/ServiceDesk.Domain.Tests/Tickets/TicketChangeTests.cs): `ChangeImpactAndUrgency_ClosedTicket_ReturnsClosedCannotBeModifiedAndKeepsValues`, `Recategorize_ClosedTicket_ReturnsClosedCannotBeModified`
  - [`TicketStateMachineTests`](../tests/ServiceDesk.Domain.Tests/Tickets/TicketStateMachineTests.cs): `Operation_InEveryStatus_ReachesExpectedStatusOrFails`
  - [`TicketWorkflowTests`](../tests/ServiceDesk.Domain.Tests/Tickets/TicketWorkflowTests.cs): `Can_EveryOperationInStatus_IsAllowedOnlyByStateMachine`, `IsTerminal_Status_IsTrueOnlyForClosed`
- **Application.Tests**
  - [`AssignTicketManuallyHandlerTests`](../tests/ServiceDesk.Application.Tests/Tickets/AssignTicketManually/AssignTicketManuallyHandlerTests.cs): `HandleAsync_ClosedTicket_ReturnsClosedCannotBeAssignedWithoutSave`
  - [`ChangeImpactAndUrgencyHandlerTests`](../tests/ServiceDesk.Application.Tests/Tickets/ChangeImpactAndUrgency/ChangeImpactAndUrgencyHandlerTests.cs): `HandleAsync_ClosedTicket_ReturnsClosedCannotBeModifiedWithoutSave`
  - [`GetAssignableAgentsHandlerTests`](../tests/ServiceDesk.Application.Tests/Tickets/GetAssignableAgents/GetAssignableAgentsHandlerTests.cs): `HandleAsync_ClosedTicket_ReturnsClosedCannotBeAssigned`
  - [`GetTicketQueueHandlerTests`](../tests/ServiceDesk.Application.Tests/Tickets/GetTicketQueue/GetTicketQueueHandlerTests.cs): `HandleAsync_ClosedStatusWithoutIncludeClosed_ReturnsValidationErrorWithoutQuery`, `HandleAsync_ClosedStatusWithIncludeClosed_IsValid`
  - [`RecategorizeTicketHandlerTests`](../tests/ServiceDesk.Application.Tests/Tickets/RecategorizeTicket/RecategorizeTicketHandlerTests.cs): `HandleAsync_ClosedTicket_ReturnsClosedCannotBeModifiedWithoutAssignment`
  - [`RetryAutoAssignHandlerTests`](../tests/ServiceDesk.Application.Tests/Tickets/RetryAutoAssign/RetryAutoAssignHandlerTests.cs): `HandleAsync_ClosedTicket_ReturnsAutoAssignmentNotAllowed`
  - [`TicketStatusHandlersTests`](../tests/ServiceDesk.Application.Tests/Tickets/TicketStatusHandlersTests.cs): `HandleAsync_ClosedTicket_ReturnsInvalidTransitionWithoutSave`
- **Infrastructure.Tests**
  - [`TicketQueriesTests`](../tests/ServiceDesk.Infrastructure.Tests/Persistence/TicketQueriesTests.cs): `GetTicketQueueAsync_DefaultQuery_LeavesOutClosedTickets`
  - [`CloseAndAssignConflictTests`](../tests/ServiceDesk.Infrastructure.Tests/UseCases/CloseAndAssignConflictTests.cs): `AssignTicketManually_TicketClosedAfterLoad_ReturnsConcurrencyConflictAndTicketStaysClosed`, `CloseTicket_TicketReassignedAfterLoad_ReturnsConcurrencyConflictAndTicketStaysResolved`, `AssignTicketManually_TicketClosedBeforeLoad_ReturnsClosedCannotBeAssigned`
- **Web.Tests**
  - [`TicketQueueTests`](../tests/ServiceDesk.Web.Tests/Pages/Admin/TicketQueueTests.cs): `Render_DefaultFilters_QueriesFirstPageWithoutClosedTickets`, `IncludeClosed_SwitchedOffWithClosedStatus_ClearsStatusFilter`
  - [`TicketDetailsAdminTests`](../tests/ServiceDesk.Web.Tests/Pages/Tickets/TicketDetailsAdminTests.cs): `Render_ClosedTicket_HidesAssignmentAndStatusButtons`, `Render_OpenTicket_ShowsOnlyAllowedActions`, `Close_NotConfirmed_DoesNotRunCloseTicket`, `Close_Confirmed_RunsCloseTicketAndReloads`, `Assign_CandidatesRefused_ShowsSnackbarWithoutDialog`

## BR-04

**Kiosztás csak aktív, kompetens ügyintézőnek.** Kiosztás csak `IsActive` ügyintézőnek, akinek a kompetenciái között szerepel a jegy kategóriája. Kategória nélküli jegy nem osztható ki; a `Ticket.Create` kategóriát követel meg.

### Megvalósítás

| Réteg | Típus | Metódus, tag |
|---|---|---|
| Domain | [`Agent`](../src/ServiceDesk.Domain/Agents/Agent.cs) | `IsEligibleFor` |
| Domain | [`Ticket`](../src/ServiceDesk.Domain/Tickets/Ticket.cs) | `AssignTo` |
| Domain | [`AgentErrors`](../src/ServiceDesk.Domain/Agents/AgentErrors.cs) | `Inactive`, `MissingCompetency` |
| Application | [`TicketAssignmentService`](../src/ServiceDesk.Application/Tickets/TicketAssignmentService.cs) | `AutoAssignAsync` |
| Application | [`AssignTicketManuallyHandler`](../src/ServiceDesk.Application/Tickets/AssignTicketManually/AssignTicketManuallyHandler.cs) | `HandleAsync` |
| Application | [`GetAssignableAgentsHandler`](../src/ServiceDesk.Application/Tickets/GetAssignableAgents/GetAssignableAgentsHandler.cs) | `HandleAsync` |
| Infrastructure | [`AgentRepository`](../src/ServiceDesk.Infrastructure/Persistence/Repositories/AgentRepository.cs) | `GetEligibleCandidatesAsync` |
| Infrastructure | [`TicketQueries`](../src/ServiceDesk.Infrastructure/Persistence/Queries/TicketQueries.cs) | `GetAssignableAgentsAsync` |
| Web | [`AssignTicketDialog`](../src/ServiceDesk.Web/Components/Tickets/AssignTicketDialog.razor) | `Agents` |

### Tesztek – 27 teszt

- **Domain.Tests**
  - [`AgentTests`](../tests/ServiceDesk.Domain.Tests/Agents/AgentTests.cs): `IsEligibleFor_ActiveAgentWithCompetency_ReturnsTrue`, `IsEligibleFor_ActiveAgentWithoutCompetency_ReturnsFalse`, `IsEligibleFor_InactiveAgentWithCompetency_ReturnsFalse`
  - [`TicketAssignmentTests`](../tests/ServiceDesk.Domain.Tests/Tickets/TicketAssignmentTests.cs): `AssignTo_ActiveAgentWithCompetency_AssignsTicket`, `AssignTo_AgentWithoutCompetency_ReturnsMissingCompetencyError`, `AssignTo_InactiveAgent_ReturnsAgentInactiveError`
- **Application.Tests**
  - [`AgentActivationHandlersTests`](../tests/ServiceDesk.Application.Tests/Agents/AgentActivationHandlersTests.cs): `HandleAsync_AgentInOtherState_ChangesStateKeepsCompetenciesSavesAndLogs`
  - [`SetAgentCompetenciesHandlerTests`](../tests/ServiceDesk.Application.Tests/Agents/SetAgentCompetencies/SetAgentCompetenciesHandlerTests.cs): `HandleAsync_ActiveCategories_ReplacesCompetenciesSavesAndLogs`
  - [`AssignTicketManuallyHandlerTests`](../tests/ServiceDesk.Application.Tests/Tickets/AssignTicketManually/AssignTicketManuallyHandlerTests.cs): `HandleAsync_NewTicketAndEligibleAgent_AssignsAsAdminSavesAndLogs`, `HandleAsync_AssignedTicketToOtherAgent_ReassignsWithoutStatusChange`, `HandleAsync_InactiveAgent_ReturnsAgentInactiveWithoutSave`, `HandleAsync_AgentWithoutCompetency_ReturnsMissingCompetencyWithoutSave`
  - [`GetAssignableAgentsHandlerTests`](../tests/ServiceDesk.Application.Tests/Tickets/GetAssignableAgents/GetAssignableAgentsHandlerTests.cs): `HandleAsync_OpenTicket_ReturnsAssignableAgents`
  - [`RecategorizeTicketHandlerTests`](../tests/ServiceDesk.Application.Tests/Tickets/RecategorizeTicket/RecategorizeTicketHandlerTests.cs): `HandleAsync_InactiveAssigneeWithCompetency_ReassignsToEligibleAgent`
  - [`TicketAssignmentServiceTests`](../tests/ServiceDesk.Application.Tests/Tickets/TicketAssignmentServiceTests.cs): `AutoAssignAsync_LoadedAgentWithoutCompetency_ReturnsMissingCompetency`
- **Infrastructure.Tests**
  - [`AgentRepositoryTests`](../tests/ServiceDesk.Infrastructure.Tests/Persistence/AgentRepositoryTests.cs): `GetEligibleCandidatesAsync_MixedAgents_ReturnsOnlyActiveCompetentAgents`, `GetEligibleCandidatesAsync_NoActiveCompetentAgent_ReturnsEmptyList`
  - [`TicketQueriesTests`](../tests/ServiceDesk.Infrastructure.Tests/Persistence/TicketQueriesTests.cs): `GetAssignableAgentsAsync_SeveralAgents_ReturnsActiveCompetentAgentsLeastLoadedThenByName`
  - [`MasterDataUseCaseTests`](../tests/ServiceDesk.Infrastructure.Tests/UseCases/MasterDataUseCaseTests.cs): `DeactivateAgent_AgentWithOpenTickets_TicketsUnchangedAndNewTicketGoesToOtherAgent`, `DeactivateAgent_OnlyEligibleAgent_NewTicketStaysNewWithoutAssignee`
- **Web.Tests**
  - [`AssignTicketDialogTests`](../tests/ServiceDesk.Web.Tests/Components/Tickets/AssignTicketDialogTests.cs): `Render_Candidates_ListsLoadAndDisablesCurrentAssignee`
  - [`TicketDetailsAdminTests`](../tests/ServiceDesk.Web.Tests/Pages/Tickets/TicketDetailsAdminTests.cs): `Assign_AgentChosenInDialog_ShowsCandidatesAndRunsAssignment`

## BR-05

**Automatikus kiosztás a legkisebb terhelésűnek.** Létrehozáskor és átkategorizáláskor a jogosult ügyintézők közül a legkisebb terhelésű kapja a jegyet (a nyitott jegyek prioritás-súlyösszege). Egyezésnél a legrégebbi `LastAssignedAt` (`null` a legrégebbi), végül az `Agent.Id` dönt.

### Megvalósítás

| Réteg | Típus | Metódus, tag |
|---|---|---|
| Domain | [`LeastLoadedAssignmentStrategy`](../src/ServiceDesk.Domain/Assignment/LeastLoadedAssignmentStrategy.cs) | `SelectAgent` |
| Domain | [`AgentCandidate`](../src/ServiceDesk.Domain/Assignment/AgentCandidate.cs) | `Load` |
| Domain | [`PriorityMatrix`](../src/ServiceDesk.Domain/Tickets/PriorityMatrix.cs) | `GetLoadWeight` |
| Domain | [`Agent`](../src/ServiceDesk.Domain/Agents/Agent.cs) | `RecordAssignment` |
| Application | [`TicketAssignmentService`](../src/ServiceDesk.Application/Tickets/TicketAssignmentService.cs) | `AutoAssignAsync` |
| Application | [`CreateTicketHandler`](../src/ServiceDesk.Application/Tickets/CreateTicket/CreateTicketHandler.cs) | `HandleAsync` |
| Application | [`RecategorizeTicketHandler`](../src/ServiceDesk.Application/Tickets/RecategorizeTicket/RecategorizeTicketHandler.cs) | `UpdateAssignmentAsync` |
| Application | [`RetryAutoAssignHandler`](../src/ServiceDesk.Application/Tickets/RetryAutoAssign/RetryAutoAssignHandler.cs) | `HandleAsync` |
| Application | [`DependencyInjection`](../src/ServiceDesk.Application/DependencyInjection.cs) | `AddApplication` |
| Infrastructure | [`AgentRepository`](../src/ServiceDesk.Infrastructure/Persistence/Repositories/AgentRepository.cs) | `GetEligibleCandidatesAsync` |
| Infrastructure | [`TicketQueries`](../src/ServiceDesk.Infrastructure/Persistence/Queries/TicketQueries.cs) | `GetAssignableAgentsAsync`, `GetAgentsAsync` |

### Tesztek – 32 teszt

- **Domain.Tests**
  - [`AgentTests`](../tests/ServiceDesk.Domain.Tests/Agents/AgentTests.cs): `RecordAssignment_Now_SetsLastAssignedAt`
  - [`AgentCandidateTests`](../tests/ServiceDesk.Domain.Tests/Assignment/AgentCandidateTests.cs): `Load_NoOpenTickets_ReturnsZero`, `Load_OpenTickets_ReturnsSumOfPriorityWeights`, `Load_OneCritical_IsGreaterThanLowAndPlanningTogether`
  - [`LeastLoadedAssignmentStrategyTests`](../tests/ServiceDesk.Domain.Tests/Assignment/LeastLoadedAssignmentStrategyTests.cs): `SelectAgent_DifferentLoads_ReturnsLeastLoadedAgent`, `SelectAgent_OneCriticalVersusLowAndPlanning_ReturnsAgentWithLowAndPlanning`, `SelectAgent_EqualLoad_ReturnsAgentWithOldestLastAssignedAt`, `SelectAgent_EqualLoadAndOneNeverAssigned_ReturnsNeverAssignedAgent`, `SelectAgent_EqualLoadAndLastAssignedAt_ReturnsSmallestAgentId`, `SelectAgent_AnyInputOrder_ReturnsSameAgent`
  - [`PriorityMatrixTests`](../tests/ServiceDesk.Domain.Tests/Tickets/PriorityMatrixTests.cs): `GetLoadWeight_Priority_ReturnsWeight`
  - [`TicketAssignmentTests`](../tests/ServiceDesk.Domain.Tests/Tickets/TicketAssignmentTests.cs): `AssignTo_EligibleAgent_RecordsAssignmentTimeOnAgent`
- **Application.Tests**
  - [`CreateTicketHandlerTests`](../tests/ServiceDesk.Application.Tests/Tickets/CreateTicket/CreateTicketHandlerTests.cs): `HandleAsync_EligibleAgent_SavesAssignedTicketOnceAndReturnsItsId`
  - [`RecategorizeTicketHandlerTests`](../tests/ServiceDesk.Application.Tests/Tickets/RecategorizeTicket/RecategorizeTicketHandlerTests.cs): `HandleAsync_AssigneeNotEligibleAndCandidateExists_ReassignsKeepingStatus`, `HandleAsync_NewTicketWithCandidate_AssignsTicketAutomatically`
  - [`RetryAutoAssignHandlerTests`](../tests/ServiceDesk.Application.Tests/Tickets/RetryAutoAssign/RetryAutoAssignHandlerTests.cs): `HandleAsync_NewTicketWithCandidate_AssignsAsSystemSavesAndReturnsAgentId`
  - [`TicketAssignmentServiceTests`](../tests/ServiceDesk.Application.Tests/Tickets/TicketAssignmentServiceTests.cs): `AutoAssignAsync_SeveralCandidates_AssignsLeastLoadedAgentAsSystem`
- **Infrastructure.Tests**
  - [`AgentRepositoryTests`](../tests/ServiceDesk.Infrastructure.Tests/Persistence/AgentRepositoryTests.cs): `GetEligibleCandidatesAsync_TicketsInEveryStatus_ReturnsOnlyOpenTicketPriorities`
  - [`TicketQueriesTests`](../tests/ServiceDesk.Infrastructure.Tests/Persistence/TicketQueriesTests.cs): `GetAssignableAgentsAsync_SeveralAgents_ReturnsActiveCompetentAgentsLeastLoadedThenByName`, `GetAgentsAsync_AgentsWithCompetenciesAndTickets_ReturnsEveryAgentWithCompetenciesAndLoad`

## BR-06

**Jogosult ügyintéző nélkül a jegy kiosztatlan marad.** Ha nincs jogosult ügyintéző, a jegy `New` állapotban, felelős nélkül marad; ez nem hiba. Admin később kézzel kioszthatja, vagy újrafuttathatja az automatikus kiosztást.

### Megvalósítás

| Réteg | Típus | Metódus, tag |
|---|---|---|
| Domain | [`LeastLoadedAssignmentStrategy`](../src/ServiceDesk.Domain/Assignment/LeastLoadedAssignmentStrategy.cs) | `SelectAgent` |
| Domain | [`TicketWorkflow`](../src/ServiceDesk.Domain/Tickets/TicketWorkflow.cs) | `CanRetryAutoAssign` |
| Domain | [`TicketErrors`](../src/ServiceDesk.Domain/Tickets/TicketErrors.cs) | `AutoAssignmentNotAllowed` |
| Application | [`TicketAssignmentService`](../src/ServiceDesk.Application/Tickets/TicketAssignmentService.cs) | `AutoAssignAsync` |
| Application | [`CreateTicketHandler`](../src/ServiceDesk.Application/Tickets/CreateTicket/CreateTicketHandler.cs) | `HandleAsync` |
| Application | [`RetryAutoAssignHandler`](../src/ServiceDesk.Application/Tickets/RetryAutoAssign/RetryAutoAssignHandler.cs) | `HandleAsync` |
| Application | [`RecategorizeTicketHandler`](../src/ServiceDesk.Application/Tickets/RecategorizeTicket/RecategorizeTicketHandler.cs) | `UpdateAssignmentAsync` |
| Web | [`TicketAdminPanel`](../src/ServiceDesk.Web/Components/Tickets/TicketAdminPanel.razor) | `RetryAutoAssignAsync` |
| Web | [`AssignTicketDialog`](../src/ServiceDesk.Web/Components/Tickets/AssignTicketDialog.razor) | `Agents` |

### Tesztek – 25 teszt

- **Domain.Tests**
  - [`LeastLoadedAssignmentStrategyTests`](../tests/ServiceDesk.Domain.Tests/Assignment/LeastLoadedAssignmentStrategyTests.cs): `SelectAgent_NoCandidates_ReturnsNull`, `SelectAgent_SingleCandidate_ReturnsThatAgent`
  - [`TicketWorkflowTests`](../tests/ServiceDesk.Domain.Tests/Tickets/TicketWorkflowTests.cs): `Can_EveryOperationInStatus_IsAllowedOnlyByStateMachine`
- **Application.Tests**
  - [`CreateTicketHandlerTests`](../tests/ServiceDesk.Application.Tests/Tickets/CreateTicket/CreateTicketHandlerTests.cs): `HandleAsync_NoEligibleAgent_SavesNewTicketWithoutAssignee`
  - [`RecategorizeTicketHandlerTests`](../tests/ServiceDesk.Application.Tests/Tickets/RecategorizeTicket/RecategorizeTicketHandlerTests.cs): `HandleAsync_NewTicketWithoutCandidate_SavesNewCategoryAndStaysNew`
  - [`RetryAutoAssignHandlerTests`](../tests/ServiceDesk.Application.Tests/Tickets/RetryAutoAssign/RetryAutoAssignHandlerTests.cs): `HandleAsync_AnonymousUser_ReturnsForbiddenWithoutLoading`, `HandleAsync_Reporter_ReturnsForbiddenWithoutLoading`, `HandleAsync_NewTicketWithCandidate_AssignsAsSystemSavesAndReturnsAgentId`, `HandleAsync_NewTicketWithoutCandidate_ReturnsNullWithoutSave`, `HandleAsync_TicketNotNew_ReturnsAutoAssignmentNotAllowed`
  - [`TicketAssignmentServiceTests`](../tests/ServiceDesk.Application.Tests/Tickets/TicketAssignmentServiceTests.cs): `AutoAssignAsync_NoCandidate_ReturnsNullAndLeavesTicketNewWithoutAssignee`
- **Infrastructure.Tests**
  - [`TicketQueriesTests`](../tests/ServiceDesk.Infrastructure.Tests/Persistence/TicketQueriesTests.cs): `GetAssignableAgentsAsync_NobodyEligible_ReturnsTicketStatusWithoutAgents`
  - [`MasterDataUseCaseTests`](../tests/ServiceDesk.Infrastructure.Tests/UseCases/MasterDataUseCaseTests.cs): `DeactivateAgent_OnlyEligibleAgent_NewTicketStaysNewWithoutAssignee`
- **Web.Tests**
  - [`AssignTicketDialogTests`](../tests/ServiceDesk.Web.Tests/Components/Tickets/AssignTicketDialogTests.cs): `Render_NoCandidates_ShowsWarningAndDisablesSubmit`
  - [`TicketDetailsAdminTests`](../tests/ServiceDesk.Web.Tests/Pages/Tickets/TicketDetailsAdminTests.cs): `Render_OpenTicket_ShowsOnlyAllowedActions`, `RetryAutoAssign_NobodyEligible_ShowsInfoSnackbarWithoutReload`

## BR-07

**Átkategorizálás.** Csak Admin, `New`, `Assigned` vagy `InProgress` állapotban. Ha a felelős jogosult az új kategóriára, a kiosztás marad; ha nem, újrakiosztás (BR-05); ha senki sem jogosult, a felelős törlődik, és a jegy `New` lesz.

### Megvalósítás

| Réteg | Típus | Metódus, tag |
|---|---|---|
| Domain | [`TicketWorkflow`](../src/ServiceDesk.Domain/Tickets/TicketWorkflow.cs) | `CanRecategorize`, `CanUnassign` |
| Domain | [`Ticket`](../src/ServiceDesk.Domain/Tickets/Ticket.cs) | `Recategorize`, `Unassign` |
| Domain | [`TicketErrors`](../src/ServiceDesk.Domain/Tickets/TicketErrors.cs) | `RecategorizationNotAllowed` |
| Application | [`RecategorizeTicketHandler`](../src/ServiceDesk.Application/Tickets/RecategorizeTicket/RecategorizeTicketHandler.cs) | `HandleAsync`, `UpdateAssignmentAsync` |
| Web | [`TicketAdminPanel`](../src/ServiceDesk.Web/Components/Tickets/TicketAdminPanel.razor) | `RecategorizeAsync` |
| Web | [`RecategorizeTicketDialog`](../src/ServiceDesk.Web/Components/Tickets/RecategorizeTicketDialog.razor) | `Categories` |

### Tesztek – 70 teszt

- **Domain.Tests**
  - [`TicketChangeTests`](../tests/ServiceDesk.Domain.Tests/Tickets/TicketChangeTests.cs): `Recategorize_ActiveCategoryInAllowedStatus_ChangesCategoryAndKeepsAssignment`, `Recategorize_ResolvedTicket_ReturnsRecategorizationNotAllowed`, `Unassign_OpenAssignedTicket_ClearsAssigneeAndReturnsToNew`, `Unassign_TicketWithoutOpenAssignment_ReturnsInvalidTransition`
  - [`TicketStateMachineTests`](../tests/ServiceDesk.Domain.Tests/Tickets/TicketStateMachineTests.cs): `Operation_InEveryStatus_ReachesExpectedStatusOrFails`
  - [`TicketWorkflowTests`](../tests/ServiceDesk.Domain.Tests/Tickets/TicketWorkflowTests.cs): `Can_EveryOperationInStatus_IsAllowedOnlyByStateMachine`
- **Application.Tests**
  - [`RecategorizeTicketHandlerTests`](../tests/ServiceDesk.Application.Tests/Tickets/RecategorizeTicket/RecategorizeTicketHandlerTests.cs): `HandleAsync_AnonymousUser_ReturnsForbiddenWithoutLoading`, `HandleAsync_Reporter_ReturnsForbiddenWithoutLoading`, `HandleAsync_ResolvedTicket_ReturnsRecategorizationNotAllowed`, `HandleAsync_AssigneeEligibleForNewCategory_KeepsAssignment`, `HandleAsync_AssigneeNotEligibleAndCandidateExists_ReassignsKeepingStatus`, `HandleAsync_AssigneeNotEligibleAndNoCandidate_UnassignsTicketToNew`, `HandleAsync_InactiveAssigneeWithCompetency_ReassignsToEligibleAgent`
- **Web.Tests**
  - [`RecategorizeTicketDialogTests`](../tests/ServiceDesk.Web.Tests/Components/Tickets/RecategorizeTicketDialogTests.cs): `Submit_OtherCategorySelected_ClosesWithCategoryId`
  - [`TicketDetailsAdminTests`](../tests/ServiceDesk.Web.Tests/Pages/Tickets/TicketDetailsAdminTests.cs): `Render_OpenTicket_ShowsOnlyAllowedActions`, `Recategorize_CategoryChosenInDialog_PassesActiveCategoriesAndRunsRecategorization`

## BR-08

**Az LLM csak létező, aktív kategóriát javasolhat.** Érvénytelen, hiányzó vagy késő válasz esetén nincs javaslat (`AiErrors.NoSuggestion`), és a jegy rögzítése ettől nem bukik el.

### Megvalósítás

| Réteg | Típus | Metódus, tag |
|---|---|---|
| Domain | [`CategorySuggestion`](../src/ServiceDesk.Domain/CategorySuggestions/CategorySuggestion.cs) | `Create` |
| Application | [`SuggestCategoryHandler`](../src/ServiceDesk.Application/Tickets/SuggestCategory/SuggestCategoryHandler.cs) | `HandleAsync` |
| Application | [`AiErrors`](../src/ServiceDesk.Application/Common/Errors/AiErrors.cs) | `NoSuggestion` |
| Infrastructure | [`LlmCategorySuggester`](../src/ServiceDesk.Infrastructure/Ai/LlmCategorySuggester.cs) | `SuggestAsync`, `Validate` |
| Infrastructure | [`LlmCategoryPrompt`](../src/ServiceDesk.Infrastructure/Ai/LlmCategoryPrompt.cs) | `SystemTemplate`, `BuildTicketMessage` |
| Web | [`NewTicket`](../src/ServiceDesk.Web/Components/Pages/Tickets/NewTicket.razor) | `RequestSuggestionAsync` |

### Tesztek – 41 teszt

- **Domain.Tests**
  - [`CategorySuggestionTests`](../tests/ServiceDesk.Domain.Tests/CategorySuggestions/CategorySuggestionTests.cs): `Create_InactiveCategory_ReturnsCategoryInactiveError`, `Create_ConfidenceAtLimit_Succeeds`, `Create_ConfidenceOutsideRange_ThrowsArgumentOutOfRangeException`, `Create_ReasoningAtMaxLength_Succeeds`, `Create_ReasoningTooLong_ThrowsArgumentOutOfRangeException`
- **Application.Tests**
  - [`SuggestCategoryHandlerTests`](../tests/ServiceDesk.Application.Tests/Tickets/SuggestCategory/SuggestCategoryHandlerTests.cs): `HandleAsync_NoActiveCategory_ReturnsNoSuggestionWithoutCallingTheModel`, `HandleAsync_SuggesterHasNoSuggestion_ReturnsNoSuggestionWithoutSaving`, `HandleAsync_SuggestedCodeIsNotAnActiveCategory_ReturnsNoSuggestionWithoutSaving`, `HandleAsync_ValidCommand_SendsOnlyTheTrimmedTextAndTheActiveCategoriesToTheModel`
- **Infrastructure.Tests**
  - [`AiServiceRegistrationTests`](../tests/ServiceDesk.Infrastructure.Tests/Ai/AiServiceRegistrationTests.cs): `AddInfrastructure_OllamaNotRunning_SuggestionReturnsNoSuggestion`
  - [`LlmCategoryPromptTests`](../tests/ServiceDesk.Infrastructure.Tests/Ai/LlmCategoryPromptTests.cs): `SuggestAsync_SystemMessage_ListsTheCategoriesAndTheRules`
  - [`LlmCategorySuggesterTests`](../tests/ServiceDesk.Infrastructure.Tests/Ai/LlmCategorySuggesterTests.cs): `SuggestAsync_ValidAnswer_ReturnsSuggestionWithModelAndPromptVersion`, `SuggestAsync_CodeInOtherCaseWithSpaces_ReturnsCodeOfTheCategory`, `SuggestAsync_CodeNotAmongActiveCategories_ReturnsNoSuggestionWithWarning`, `SuggestAsync_InvalidJson_ReturnsNoSuggestionWithWarning`, `SuggestAsync_ModelSlowerThanTimeout_ReturnsNoSuggestionWithWarning`, `SuggestAsync_AnswerJustBeforeTimeout_ReturnsSuggestion`, `SuggestAsync_ModelUnreachable_ReturnsNoSuggestionWithWarning`, `SuggestAsync_Confidence_IsClampedBetweenZeroAndOne`, `SuggestAsync_TooLongReasoning_IsCutTo500Characters`, `SuggestAsync_ReasoningCutInsideSurrogatePair_DropsTheHalfCharacter`, `SuggestAsync_ReasoningWithControlCharacters_ReturnsSingleLinePlainText`
- **Web.Tests**
  - [`NewTicketTests`](../tests/ServiceDesk.Web.Tests/Pages/Tickets/NewTicketTests.cs): `RequestSuggestion_Unavailable_ShowsFriendlyMessageAndManualChoiceStillCreatesTicket`

## BR-09

**Kategória csak emberi döntéssel.** A Bejelentő a létrehozó űrlapon elfogadja vagy felülbírálja a javaslatot, illetve kézzel választ. A javaslat, a döntés (`Accepted` vagy `Overridden`), a döntéshozó és az időpont mentésre kerül.

### Megvalósítás

| Réteg | Típus | Metódus, tag |
|---|---|---|
| Domain | [`CategorySuggestion`](../src/ServiceDesk.Domain/CategorySuggestions/CategorySuggestion.cs) | `Create`, `Decide` |
| Domain | [`CategorySuggestionErrors`](../src/ServiceDesk.Domain/CategorySuggestions/CategorySuggestionErrors.cs) | `NotFound`, `AlreadyDecided` |
| Application | [`SuggestCategoryHandler`](../src/ServiceDesk.Application/Tickets/SuggestCategory/SuggestCategoryHandler.cs) | `HandleAsync` |
| Application | [`CreateTicketHandler`](../src/ServiceDesk.Application/Tickets/CreateTicket/CreateTicketHandler.cs) | `HandleAsync`, `LogSuggestionDecided` |
| Application | [`CreateTicketValidator`](../src/ServiceDesk.Application/Tickets/CreateTicket/CreateTicketValidator.cs) | `SuggestionId` |
| Infrastructure | [`TicketQueries`](../src/ServiceDesk.Infrastructure/Persistence/Queries/TicketQueries.cs) | `GetTicketDetailsAsync` |
| Web | [`NewTicket`](../src/ServiceDesk.Web/Components/Pages/Tickets/NewTicket.razor) | `AcceptSuggestion`, `ChooseOtherCategoryAsync` |
| Web | [`TicketCategorySuggestionInfo`](../src/ServiceDesk.Web/Components/Tickets/TicketCategorySuggestionInfo.razor) | (a komponens egésze) |
| Web | [`TicketLabels`](../src/ServiceDesk.Web/Components/Tickets/TicketLabels.cs) | `SuggestionDecisionText` |

### Tesztek – 37 teszt

- **Domain.Tests**
  - [`CategorySuggestionTests`](../tests/ServiceDesk.Domain.Tests/CategorySuggestions/CategorySuggestionTests.cs): `Create_ValidInput_ReturnsPendingSuggestion`, `Decide_PendingSuggestion_RecordsTicketDecisionAndTime`, `Decide_AlreadyDecided_ReturnsAlreadyDecidedAndKeepsFirstDecision`
- **Application.Tests**
  - [`CreateTicketHandlerTests`](../tests/ServiceDesk.Application.Tests/Tickets/CreateTicket/CreateTicketHandlerTests.cs): `HandleAsync_SuggestionOfChosenCategory_DecidesAcceptedAndSavesOnce`, `HandleAsync_SuggestionOfOtherCategory_DecidesOverriddenAndKeepsChosenCategory`, `HandleAsync_SuggestionOfOtherUser_ReturnsNotFoundWithoutChangesAndLogsWarning`, `HandleAsync_UnknownSuggestion_ReturnsNotFoundWithoutChanges`, `HandleAsync_AlreadyDecidedSuggestion_ReturnsAlreadyDecidedWithoutChanges`, `HandleAsync_EmptySuggestionId_ReturnsValidationErrorWithoutLoadingSuggestion`, `HandleAsync_SuggestionDecided_LogsDecisionWithoutReasoning`
  - [`CreateTicketValidatorTests`](../tests/ServiceDesk.Application.Tests/Tickets/CreateTicket/CreateTicketValidatorTests.cs): `Validate_EmptySuggestionId_ReturnsSuggestionError`, `Validate_NoOrSavedSuggestionId_IsValid`
  - [`SuggestCategoryHandlerTests`](../tests/ServiceDesk.Application.Tests/Tickets/SuggestCategory/SuggestCategoryHandlerTests.cs): `HandleAsync_AnonymousUser_ReturnsForbiddenWithoutCallingTheModel`, `HandleAsync_Admin_ReturnsForbiddenWithoutCallingTheModel`, `HandleAsync_SignedInUserWithoutRole_ReturnsForbidden`, `HandleAsync_ValidSuggestion_SavesPendingSuggestionOnceAndReturnsIt`
- **Infrastructure.Tests**
  - [`TicketQueriesTests`](../tests/ServiceDesk.Infrastructure.Tests/Persistence/TicketQueriesTests.cs): `GetTicketDetailsAsync_DecidedSuggestion_ReturnedOnlyWithoutReporterFilter`, `GetTicketDetailsAsync_TicketWithoutSuggestion_ReturnsNoSuggestion`
  - [`CategorySuggestionDecisionTests`](../tests/ServiceDesk.Infrastructure.Tests/UseCases/CategorySuggestionDecisionTests.cs): `CreateTicket_PendingSuggestion_SavesTicketAndDecisionTogether`, `CreateTicket_AcceptedSuggestion_AdminDetailsShowItAndReporterDetailsDoNot`, `CreateTicket_SuggestionOfOtherReporter_RefusedAndNothingSaved`
- **Web.Tests**
  - [`TicketLabelsTests`](../tests/ServiceDesk.Web.Tests/Components/Tickets/TicketLabelsTests.cs): `SuggestionDecision_Decision_ReturnsHungarianTextAndColor`
  - [`NewTicketTests`](../tests/ServiceDesk.Web.Tests/Pages/Tickets/NewTicketTests.cs): `SuggestionButton_TitleAndDescriptionTooShort_DisabledUntilBothValid`, `RequestSuggestion_WhileRunning_DisablesButtonAndShowsProgress`, `RequestSuggestion_Success_ShowsCategoryConfidenceAndReasoningAsPlainText`, `AcceptSuggestion_SetsSuggestedCategory`, `ChooseOtherCategory_AfterAccept_ClearsSuggestedCategory`, `EditDescription_AfterSuggestion_ShowsStaleWarning`, `Submit_AcceptedSuggestion_RecordsAcceptedDecisionWithTicket`
  - [`TicketDetailsAdminTests`](../tests/ServiceDesk.Web.Tests/Pages/Tickets/TicketDetailsAdminTests.cs): `Render_AdminWithAcceptedSuggestion_ShowsSuggestionConfidenceDecisionAndReasoning`, `Render_AdminWithOverriddenSuggestion_ShowsOverridden`, `Render_AdminWithoutSuggestion_ShowsThatTicketHasNoSuggestion`, `Render_Reporter_DoesNotShowSuggestionInfo`

## BR-10

**Jegytörténet.** Létrehozás, kiosztás, átosztás, státuszváltás, átkategorizálás, valamint hatás- vagy sürgősség-módosítás esetén `TicketHistoryEntry` keletkezik: ki, mikor, mit, régi → új érték.

### Megvalósítás

| Réteg | Típus | Metódus, tag |
|---|---|---|
| Domain | [`Ticket`](../src/ServiceDesk.Domain/Tickets/Ticket.cs) | `Create`, `AssignTo`, `ChangeStatus`, `Recategorize`, `ChangeImpactAndUrgency`, `Unassign` |
| Domain | [`TicketHistoryEntry`](../src/ServiceDesk.Domain/Tickets/TicketHistoryEntry.cs) | `Create`, `Sequence` |
| Infrastructure | [`TicketConfiguration`](../src/ServiceDesk.Infrastructure/Persistence/Configurations/TicketConfiguration.cs) | `Configure` |
| Infrastructure | [`TicketRepository`](../src/ServiceDesk.Infrastructure/Persistence/Repositories/TicketRepository.cs) | `GetByIdAsync` |
| Infrastructure | [`TicketQueries`](../src/ServiceDesk.Infrastructure/Persistence/Queries/TicketQueries.cs) | `GetTicketDetailsAsync` |
| Web | [`TicketDetails`](../src/ServiceDesk.Web/Components/Pages/Tickets/TicketDetails.razor) | `History` |

### Tesztek – 26 teszt

- **Domain.Tests**
  - [`TicketHistoryTests`](../tests/ServiceDesk.Domain.Tests/Tickets/TicketHistoryTests.cs): `Create_ValidInput_AddsCreatedEntryByReporter`, `AssignTo_NewTicketBySystem_AddsAssignedAndStatusChangedEntries`, `AssignTo_AssignedTicketByAdmin_AddsReassignedEntry`, `Start_AssignedTicket_AddsStatusChangedEntry`, `Recategorize_DifferentCategory_AddsRecategorizedEntry`, `Recategorize_SameCategory_AddsNoEntry`, `ChangeImpactAndUrgency_BothChanged_AddsImpactAndUrgencyEntries`, `ChangeImpactAndUrgency_OnlyUrgencyChanged_AddsUrgencyEntryOnly`, `ChangeImpactAndUrgency_NothingChanged_AddsNoEntry`, `Unassign_InProgressTicket_AddsSystemEntries`, `Start_NewTicket_FailsWithoutEntry`, `Create_ValidInput_FirstEntryHasSequenceOne`, `AssignTo_NewTicket_NumbersEntriesOfOneOperationInOrder`, `ChangeImpactAndUrgency_BothChanged_NumbersImpactBeforeUrgency`, `History_MainPathToClosed_SequenceRunsFromOneWithoutGaps`, `AssignTo_AfterFailedOperation_ContinuesSequenceWithoutGap`
- **Application.Tests**
  - [`AssignTicketManuallyHandlerTests`](../tests/ServiceDesk.Application.Tests/Tickets/AssignTicketManually/AssignTicketManuallyHandlerTests.cs): `HandleAsync_NewTicketAndEligibleAgent_AssignsAsAdminSavesAndLogs`
  - [`ChangeImpactAndUrgencyHandlerTests`](../tests/ServiceDesk.Application.Tests/Tickets/ChangeImpactAndUrgency/ChangeImpactAndUrgencyHandlerTests.cs): `HandleAsync_OpenTicket_RecalculatesPrioritySavesAndLogs`
  - [`TicketStatusHandlersTests`](../tests/ServiceDesk.Application.Tests/Tickets/TicketStatusHandlersTests.cs): `HandleAsync_TicketInSourceStatus_ChangesStatusSavesAndLogs`
- **Infrastructure.Tests**
  - [`TicketQueriesTests`](../tests/ServiceDesk.Infrastructure.Tests/Persistence/TicketQueriesTests.cs): `GetTicketDetailsAsync_ReassignedAndRecategorizedTicket_ResolvesNamesInHistoryOrder`
  - [`TicketRepositoryTests`](../tests/ServiceDesk.Infrastructure.Tests/Persistence/TicketRepositoryTests.cs): `GetByIdAsync_SavedTicket_ReturnsTicketWithHistoryInSequenceOrder`, `SaveChangesAsync_ChangeOnLoadedTicket_AppendsHistoryEntry`
- **Web.Tests**
  - [`TicketDetailsTests`](../tests/ServiceDesk.Web.Tests/Pages/Tickets/TicketDetailsTests.cs): `Render_TicketWithHistory_ShowsHungarianValuesInHeaderAndTimeline`

## BR-11

**A prioritás a mátrixból számított.** A prioritás mindig a Hatás × Sürgősség mátrixból számított érték, közvetlenül nem állítható. A Bejelentő létrehozáskor adja meg a hatást és a sürgősséget, az Admin triázskor módosíthatja őket.

### Megvalósítás

| Réteg | Típus | Metódus, tag |
|---|---|---|
| Domain | [`PriorityMatrix`](../src/ServiceDesk.Domain/Tickets/PriorityMatrix.cs) | `Calculate` |
| Domain | [`Ticket`](../src/ServiceDesk.Domain/Tickets/Ticket.cs) | `Create`, `ChangeImpactAndUrgency`, `Priority` |
| Application | [`ChangeImpactAndUrgencyHandler`](../src/ServiceDesk.Application/Tickets/ChangeImpactAndUrgency/ChangeImpactAndUrgencyHandler.cs) | `HandleAsync` |
| Application | [`ChangeImpactAndUrgencyCommand`](../src/ServiceDesk.Application/Tickets/ChangeImpactAndUrgency/ChangeImpactAndUrgencyCommand.cs) | csak `Impact` és `Urgency`, nincs `Priority` |
| Infrastructure | [`TicketConfiguration`](../src/ServiceDesk.Infrastructure/Persistence/Configurations/TicketConfiguration.cs) | `Configure` |
| Web | [`NewTicket`](../src/ServiceDesk.Web/Components/Pages/Tickets/NewTicket.razor) | `ExpectedPriority` |
| Web | [`TicketAdminPanel`](../src/ServiceDesk.Web/Components/Tickets/TicketAdminPanel.razor) | `PriorityPreview`, `SaveTriageAsync` |

### Tesztek – 23 teszt

- **Domain.Tests**
  - [`PriorityMatrixTests`](../tests/ServiceDesk.Domain.Tests/Tickets/PriorityMatrixTests.cs): `Calculate_MatrixCell_ReturnsPriority`, `Calculate_UndefinedValue_ThrowsArgumentOutOfRangeException`
  - [`TicketChangeTests`](../tests/ServiceDesk.Domain.Tests/Tickets/TicketChangeTests.cs): `ChangeImpactAndUrgency_NewValues_RecalculatesPriority`, `ChangeImpactAndUrgency_UndefinedUrgency_ReturnsUrgencyInvalidAndKeepsPriority`, `ChangeImpactAndUrgency_ResolvedTicket_RecalculatesPriority`
  - [`TicketCreateTests`](../tests/ServiceDesk.Domain.Tests/Tickets/TicketCreateTests.cs): `Create_ImpactAndUrgency_SetsPriorityFromMatrix`
- **Application.Tests**
  - [`ReporterInputShapeTests`](../tests/ServiceDesk.Application.Tests/Common/Security/ReporterInputShapeTests.cs): `CreateTicketCommand_Properties_AreOnlyTheFieldsOfTheReporter`
  - [`ChangeImpactAndUrgencyHandlerTests`](../tests/ServiceDesk.Application.Tests/Tickets/ChangeImpactAndUrgency/ChangeImpactAndUrgencyHandlerTests.cs): `HandleAsync_Reporter_ReturnsForbiddenWithoutLoading`, `HandleAsync_OpenTicket_RecalculatesPrioritySavesAndLogs`
  - [`CreateTicketHandlerTests`](../tests/ServiceDesk.Application.Tests/Tickets/CreateTicket/CreateTicketHandlerTests.cs): `HandleAsync_EligibleAgent_SavesAssignedTicketOnceAndReturnsItsId`
- **Web.Tests**
  - [`NewTicketTests`](../tests/ServiceDesk.Web.Tests/Pages/Tickets/NewTicketTests.cs): `SelectImpactAndUrgency_HighAndHigh_ShowsCriticalExpectedPriority`
  - [`TicketDetailsAdminTests`](../tests/ServiceDesk.Web.Tests/Pages/Tickets/TicketDetailsAdminTests.cs): `Triage_HighImpact_ShowsPriorityPreviewAndRunsUseCase`

## BR-12

**Ügyintéző inaktiválása.** Az inaktiválás nem érinti a már kiosztott jegyeket; ezeket csak Admin oszthatja át kézzel. Inaktív ügyintéző új jegyet nem kaphat (BR-04).

### Megvalósítás

| Réteg | Típus | Metódus, tag |
|---|---|---|
| Domain | [`Agent`](../src/ServiceDesk.Domain/Agents/Agent.cs) | `Deactivate`, `Activate`, `IsEligibleFor` |
| Domain | [`Ticket`](../src/ServiceDesk.Domain/Tickets/Ticket.cs) | `AssignTo` |
| Application | [`DeactivateAgentHandler`](../src/ServiceDesk.Application/Agents/DeactivateAgent/DeactivateAgentHandler.cs) | `HandleAsync` |
| Application | [`ActivateAgentHandler`](../src/ServiceDesk.Application/Agents/ActivateAgent/ActivateAgentHandler.cs) | `HandleAsync` |
| Application | [`AssignTicketManuallyHandler`](../src/ServiceDesk.Application/Tickets/AssignTicketManually/AssignTicketManuallyHandler.cs) | `HandleAsync` |
| Infrastructure | [`TicketQueries`](../src/ServiceDesk.Infrastructure/Persistence/Queries/TicketQueries.cs) | `GetAgentsAsync`, `GetTicketQueueFiltersAsync` |
| Web | [`Agents`](../src/ServiceDesk.Web/Components/Pages/Admin/Agents.razor) | `DeactivateAsync` |
| Web | [`TicketLabels`](../src/ServiceDesk.Web/Components/Tickets/TicketLabels.cs) | `NameWithActiveState` |

### Tesztek – 25 teszt

- **Domain.Tests**
  - [`AgentTests`](../tests/ServiceDesk.Domain.Tests/Agents/AgentTests.cs): `Deactivate_ActiveAgent_BecomesInactiveAndKeepsCompetencies`, `Activate_InactiveAgent_BecomesActive`, `IsEligibleFor_InactiveAgentWithCompetency_ReturnsFalse`
  - [`TicketAssignmentTests`](../tests/ServiceDesk.Domain.Tests/Tickets/TicketAssignmentTests.cs): `AssignTo_InactiveAgent_ReturnsAgentInactiveError`, `Deactivate_AgentWithAssignedTicket_TicketKeepsAssigneeAndCanProgress`, `AssignTo_TicketOfInactiveAgentToActiveAgent_ReassignsTicket`
- **Application.Tests**
  - [`AgentActivationHandlersTests`](../tests/ServiceDesk.Application.Tests/Agents/AgentActivationHandlersTests.cs): `HandleAsync_Reporter_ReturnsForbiddenWithoutLoading`, `HandleAsync_AgentInOtherState_ChangesStateKeepsCompetenciesSavesAndLogs`, `HandleAsync_DeactivateAgentWithOpenTicket_LeavesTicketUnchanged`
  - [`ListAgentsHandlerTests`](../tests/ServiceDesk.Application.Tests/Agents/ListAgents/ListAgentsHandlerTests.cs): `HandleAsync_Admin_ReturnsAgentsWithInactiveOnes`
  - [`AssignTicketManuallyHandlerTests`](../tests/ServiceDesk.Application.Tests/Tickets/AssignTicketManually/AssignTicketManuallyHandlerTests.cs): `HandleAsync_TicketOfInactiveAgentToActiveAgent_Reassigns`, `HandleAsync_InactiveAgent_ReturnsAgentInactiveWithoutSave`
  - [`GetTicketQueueFiltersHandlerTests`](../tests/ServiceDesk.Application.Tests/Tickets/GetTicketQueueFilters/GetTicketQueueFiltersHandlerTests.cs): `HandleAsync_Admin_ReturnsCategoriesAndAgentsWithInactiveEntries`
  - [`RecategorizeTicketHandlerTests`](../tests/ServiceDesk.Application.Tests/Tickets/RecategorizeTicket/RecategorizeTicketHandlerTests.cs): `HandleAsync_InactiveAssigneeWithCompetency_ReassignsToEligibleAgent`
- **Infrastructure.Tests**
  - [`TicketQueriesTests`](../tests/ServiceDesk.Infrastructure.Tests/Persistence/TicketQueriesTests.cs): `GetTicketQueueFiltersAsync_ActiveAndInactiveEntries_ReturnsEveryCategoryAndAgentByName`, `GetAgentsAsync_AgentsWithCompetenciesAndTickets_ReturnsEveryAgentWithCompetenciesAndLoad`
  - [`MasterDataUseCaseTests`](../tests/ServiceDesk.Infrastructure.Tests/UseCases/MasterDataUseCaseTests.cs): `DeactivateAgent_AgentWithOpenTickets_TicketsUnchangedAndNewTicketGoesToOtherAgent`, `DeactivateAgent_OnlyEligibleAgent_NewTicketStaysNewWithoutAssignee`
- **Web.Tests**
  - [`TicketLabelsTests`](../tests/ServiceDesk.Web.Tests/Components/Tickets/TicketLabelsTests.cs): `NameWithActiveState_Name_MarksOnlyInactive`
  - [`AgentsTests`](../tests/ServiceDesk.Web.Tests/Pages/Admin/AgentsTests.cs): `Deactivate_NotConfirmed_DoesNotRunDeactivateAgent`, `Deactivate_Confirmed_RunsDeactivateAgentAndReloads`

## BR-13

**Kategória csak inaktiválható.** Kategória nem törölhető, csak inaktiválható. Inaktív kategória új jegyhez nem választható és nem javasolható; a meglévő jegyeken és a meglévő kompetenciákon megmarad. Új kompetenciaként csak aktív kategória vehető fel.

### Megvalósítás

| Réteg | Típus | Metódus, tag |
|---|---|---|
| Domain | [`Category`](../src/ServiceDesk.Domain/Categories/Category.cs) | `Deactivate`, `Activate` |
| Domain | [`Ticket`](../src/ServiceDesk.Domain/Tickets/Ticket.cs) | `Create`, `Recategorize` |
| Domain | [`Agent`](../src/ServiceDesk.Domain/Agents/Agent.cs) | `UpdateCompetencies` |
| Domain | [`CategorySuggestion`](../src/ServiceDesk.Domain/CategorySuggestions/CategorySuggestion.cs) | `Create` |
| Application | [`DeactivateCategoryHandler`](../src/ServiceDesk.Application/Categories/DeactivateCategory/DeactivateCategoryHandler.cs) | `HandleAsync` |
| Application | [`ActivateCategoryHandler`](../src/ServiceDesk.Application/Categories/ActivateCategory/ActivateCategoryHandler.cs) | `HandleAsync` |
| Application | [`SetAgentCompetenciesHandler`](../src/ServiceDesk.Application/Agents/SetAgentCompetencies/SetAgentCompetenciesHandler.cs) | `HandleAsync` |
| Application | [`SuggestCategoryHandler`](../src/ServiceDesk.Application/Tickets/SuggestCategory/SuggestCategoryHandler.cs) | `HandleAsync` |
| Infrastructure | [`CategoryRepository`](../src/ServiceDesk.Infrastructure/Persistence/Repositories/CategoryRepository.cs) | `GetActiveAsync` |
| Infrastructure | [`TicketQueries`](../src/ServiceDesk.Infrastructure/Persistence/Queries/TicketQueries.cs) | `GetActiveCategoriesAsync`, `GetCategoriesAsync`, `GetTicketQueueFiltersAsync` |
| Infrastructure | [`AgentCompetencyConfiguration`](../src/ServiceDesk.Infrastructure/Persistence/Configurations/AgentCompetencyConfiguration.cs) | `Configure` |
| Web | [`Categories`](../src/ServiceDesk.Web/Components/Pages/Admin/Categories.razor) | `DeactivateAsync` |
| Web | [`AgentCompetenciesDialog`](../src/ServiceDesk.Web/Components/MasterData/AgentCompetenciesDialog.razor) | `Categories` |
| Web | [`RecategorizeTicketDialog`](../src/ServiceDesk.Web/Components/Tickets/RecategorizeTicketDialog.razor) | `Categories` |

### Tesztek – 53 teszt

- **Domain.Tests**
  - [`AgentTests`](../tests/ServiceDesk.Domain.Tests/Agents/AgentTests.cs): `UpdateCompetencies_ActiveCategories_ReplacesCompetencySet`, `UpdateCompetencies_NewInactiveCategory_ReturnsCategoryInactiveAndKeepsCompetencies`, `UpdateCompetencies_KeptCompetencyOfDeactivatedCategory_KeepsIt`, `UpdateCompetencies_DeactivatedCategoryLeftOut_KeepsIt`, `UpdateCompetencies_ActiveCategoryLeftOut_RemovesIt`
  - [`CategoryTests`](../tests/ServiceDesk.Domain.Tests/Categories/CategoryTests.cs): `Deactivate_ActiveCategory_BecomesInactiveAndKeepsData`, `Deactivate_InactiveCategory_StaysInactive`, `Activate_InactiveCategory_BecomesActive`, `Deactivate_CategoryOfExistingTicket_TicketKeepsCategoryAndCanProgress`
  - [`CategorySuggestionTests`](../tests/ServiceDesk.Domain.Tests/CategorySuggestions/CategorySuggestionTests.cs): `Create_InactiveCategory_ReturnsCategoryInactiveError`
  - [`TicketChangeTests`](../tests/ServiceDesk.Domain.Tests/Tickets/TicketChangeTests.cs): `Recategorize_ActiveCategoryInAllowedStatus_ChangesCategoryAndKeepsAssignment`, `Recategorize_InactiveCategory_ReturnsCategoryInactiveError`
  - [`TicketCreateTests`](../tests/ServiceDesk.Domain.Tests/Tickets/TicketCreateTests.cs): `Create_ActiveCategory_StoresCategoryId`, `Create_InactiveCategory_ReturnsCategoryInactiveError`
- **Application.Tests**
  - [`SetAgentCompetenciesHandlerTests`](../tests/ServiceDesk.Application.Tests/Agents/SetAgentCompetencies/SetAgentCompetenciesHandlerTests.cs): `HandleAsync_NewInactiveCategory_ReturnsCategoryInactiveAndKeepsCompetencies`, `HandleAsync_KeptCompetencyOfDeactivatedCategory_KeepsItAndSaves`, `HandleAsync_DeactivatedCategoryLeftOut_KeepsItSavesAndLogsResultingCount`
  - [`CategoryActivationHandlersTests`](../tests/ServiceDesk.Application.Tests/Categories/CategoryActivationHandlersTests.cs): `HandleAsync_Reporter_ReturnsForbiddenWithoutLoading`, `HandleAsync_CategoryInOtherState_ChangesStateSavesAndLogs`
  - [`ListCategoriesHandlerTests`](../tests/ServiceDesk.Application.Tests/Categories/ListCategories/ListCategoriesHandlerTests.cs): `HandleAsync_Admin_ReturnsCategoriesWithInactiveOnes`
  - [`UpdateCategoryHandlerTests`](../tests/ServiceDesk.Application.Tests/Categories/UpdateCategory/UpdateCategoryHandlerTests.cs): `HandleAsync_InactiveCategory_UpdatesAndKeepsItInactive`
  - [`CreateTicketHandlerTests`](../tests/ServiceDesk.Application.Tests/Tickets/CreateTicket/CreateTicketHandlerTests.cs): `HandleAsync_InactiveCategory_ReturnsCategoryInactive`
  - [`GetActiveCategoriesHandlerTests`](../tests/ServiceDesk.Application.Tests/Tickets/GetActiveCategories/GetActiveCategoriesHandlerTests.cs): `HandleAsync_SignedInUser_ReturnsActiveCategories`
  - [`GetTicketQueueFiltersHandlerTests`](../tests/ServiceDesk.Application.Tests/Tickets/GetTicketQueueFilters/GetTicketQueueFiltersHandlerTests.cs): `HandleAsync_Admin_ReturnsCategoriesAndAgentsWithInactiveEntries`
  - [`RecategorizeTicketHandlerTests`](../tests/ServiceDesk.Application.Tests/Tickets/RecategorizeTicket/RecategorizeTicketHandlerTests.cs): `HandleAsync_InactiveCategory_ReturnsCategoryInactive`
- **Infrastructure.Tests**
  - [`CategoryRepositoryTests`](../tests/ServiceDesk.Infrastructure.Tests/Persistence/CategoryRepositoryTests.cs): `GetActiveAsync_ActiveAndInactiveCategories_ReturnsOnlyActiveOnesByCode`
  - [`TicketQueriesTests`](../tests/ServiceDesk.Infrastructure.Tests/Persistence/TicketQueriesTests.cs): `GetActiveCategoriesAsync_ActiveAndInactiveCategories_ReturnsOnlyActiveByName`, `GetTicketQueueFiltersAsync_ActiveAndInactiveEntries_ReturnsEveryCategoryAndAgentByName`, `GetCategoriesAsync_ActiveAndInactiveCategories_ReturnsEveryCategoryByName`, `GetAgentsAsync_AgentsWithCompetenciesAndTickets_ReturnsEveryAgentWithCompetenciesAndLoad`
  - [`MasterDataUseCaseTests`](../tests/ServiceDesk.Infrastructure.Tests/UseCases/MasterDataUseCaseTests.cs): `DeactivateCategory_ThenCreateTicket_CategoryNotOfferedAndRefused`, `SetAgentCompetencies_NewInactiveCategory_RefusedAndCompetenciesUnchanged`, `SetAgentCompetencies_KeptCompetencyOfDeactivatedCategory_SavesNewSet`, `SetAgentCompetencies_DeactivatedCategoryLeftOut_KeepsIt`, `CreateCategory_NewCode_SavesActiveCategoryOfferedForNewTickets`
- **Web.Tests**
  - [`AgentCompetenciesDialogTests`](../tests/ServiceDesk.Web.Tests/Components/MasterData/AgentCompetenciesDialogTests.cs): `Render_Competencies_OffersOnlyActiveCategoriesAndShowsKeptInactiveOne`, `Submit_SelectionChanged_ReturnsOnlyChosenActiveCategories`
  - [`RecategorizeTicketDialogTests`](../tests/ServiceDesk.Web.Tests/Components/Tickets/RecategorizeTicketDialogTests.cs): `Render_InactiveCurrentCategory_SelectsNothing`
  - [`TicketLabelsTests`](../tests/ServiceDesk.Web.Tests/Components/Tickets/TicketLabelsTests.cs): `NameWithActiveState_Name_MarksOnlyInactive`
  - [`AgentsTests`](../tests/ServiceDesk.Web.Tests/Pages/Admin/AgentsTests.cs): `Render_Agents_ShowsStateCompetencyChipsAndLoad`, `EditCompetencies_DialogOk_PassesActiveCategoriesAndRunsSetAgentCompetencies`
  - [`CategoriesTests`](../tests/ServiceDesk.Web.Tests/Pages/Admin/CategoriesTests.cs): `Render_Categories_ListsActiveAndInactiveWithoutDeleteAction`, `Deactivate_NotConfirmed_DoesNotRunDeactivateCategory`, `Deactivate_Confirmed_RunsDeactivateCategoryAndReloads`, `Activate_NotConfirmed_DoesNotRunActivateCategory`
  - [`TicketQueueTests`](../tests/ServiceDesk.Web.Tests/Pages/Admin/TicketQueueTests.cs): `SelectCategory_InactiveCategory_IsOfferedWithMarkerAndFiltersQuery`
