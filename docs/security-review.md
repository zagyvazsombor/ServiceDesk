# Biztonsági és minőségi átvizsgálás (P-9A)

- Dátum: 2026-10-30
- Hatókör: a teljes `src/` és `tests/` (a generált migrációk nélkül), AGENTS.md §8, §11, §15, §17.
- Módszer: kódolvasás (mind a 26 handler), mintakeresés a teljes kódbázisban, a hiányok pótlása tesztekkel,
  futtatás: `dotnet build`, `dotnet test --solution ServiceDesk.slnx`, `dotnet format --verify-no-changes`.
- Jelölés: a tesztnevek `Osztály.Metódus` alakúak. A táblázatban felsorolt tesztek mind léteznek és zöldek
  (az ellenőrzés módja a 10. szakaszban).

## 1. Use case-szint (2. és 3. szint)

Közös szabály: minden handler első lépése a `CurrentUserAuthorization.RequireRole` vagy `RequireAuthenticated`.
Névtelen felhasználó és rossz szerepkör esetén is `AuthErrors.Forbidden` a válasz. A tiltott ágban a handler
semmit nem tölt be és nem ment (a tesztek `…WithoutLoading`, `…WithoutQuery`, `…WithoutChanges` része ezt ellenőrzi).
A közös ellenőrzés tesztjei:

- `CurrentUserAuthorizationTests.RequireAuthenticated_AnonymousUser_ReturnsForbidden`
- `CurrentUserAuthorizationTests.RequireAuthenticated_AuthenticatedWithoutUserId_ReturnsForbidden`
- `CurrentUserAuthorizationTests.RequireRole_AnonymousUser_ReturnsForbidden`
- `CurrentUserAuthorizationTests.RequireRole_SignedInUserWithOtherRole_ReturnsForbidden`
- `CurrentUserAuthorizationTests.RequireRole_SignedInUserWithoutRole_ReturnsForbidden`

A megtagadott use case-t a `UseCaseRunner` naplózza Warning szinten:
`UseCaseRunnerTests.RunAsync_ForbiddenResult_LogsWarningWithHandlerUserAndErrorCode`.

### 1.1 Jegyek – Bejelentő és közös

| Handler | Szükséges szerepkör | Erőforrás-ellenőrzés (3. szint) | Bizonyító tesztek |
|---|---|---|---|
| `CreateTicketHandler` | Reporter | A `ReporterId` a bejelentkezett felhasználó (BR-01). A command-ban nincs `ReporterId`, `Status`, `AssigneeAgentId`, `Priority` (over-posting, §8). A `SuggestionId` csak a saját javaslatra mutathat; idegenre `NotFound` és Warning (BR-09). | `CreateTicketHandlerTests.HandleAsync_AnonymousUser_ReturnsForbiddenWithoutChanges`<br>`CreateTicketHandlerTests.HandleAsync_Admin_ReturnsForbiddenWithoutChanges`<br>`CreateTicketHandlerTests.HandleAsync_SignedInUserWithoutRole_ReturnsForbidden`<br>`CreateTicketHandlerTests.HandleAsync_EligibleAgent_SavesAssignedTicketOnceAndReturnsItsId`<br>`CreateTicketHandlerTests.HandleAsync_SuggestionOfOtherUser_ReturnsNotFoundWithoutChangesAndLogsWarning`<br>`CategorySuggestionDecisionTests.CreateTicket_SuggestionOfOtherReporter_RefusedAndNothingSaved`<br>`ReporterInputShapeTests.CreateTicketCommand_Properties_AreOnlyTheFieldsOfTheReporter` (új) |
| `GetMyTicketsHandler` | Reporter | A `ReporterId`-szűrés a lekérdezés része; a query-ben nincs azonosító (BR-02). | `GetMyTicketsHandlerTests.HandleAsync_AnonymousUser_ReturnsForbiddenWithoutQuery`<br>`GetMyTicketsHandlerTests.HandleAsync_Admin_ReturnsForbiddenWithoutQuery`<br>`GetMyTicketsHandlerTests.HandleAsync_Reporter_QueriesOwnTicketsWithPageSize20`<br>`TicketQueriesTests.GetMyTicketsAsync_TicketsOfTwoReporters_ReturnsOnlyOwnNewestFirst` |
| `GetTicketDetailsHandler` | Admin vagy Reporter | Reporter: a `ReporterId`-szűrés a lekérdezés része; idegen jegyre `NotFound` és Warning, a jegy tartalma nélkül (BR-02). Admin: minden jegy. A Reporter nem látja az AI-javaslatot. | `GetTicketDetailsHandlerTests.HandleAsync_AnonymousUser_ReturnsForbiddenWithoutQuery`<br>`GetTicketDetailsHandlerTests.HandleAsync_SignedInUserWithoutRole_ReturnsForbiddenWithoutQuery`<br>`GetTicketDetailsHandlerTests.HandleAsync_ReporterForeignTicket_ReturnsNotFoundAndLogsWarningWithoutContent`<br>`GetTicketDetailsHandlerTests.HandleAsync_Admin_QueriesWithoutReporterFilter`<br>`TicketQueriesTests.GetTicketDetailsAsync_ForeignTicketWithReporterFilter_ReturnsNull`<br>`TicketQueriesTests.GetTicketDetailsAsync_DecidedSuggestion_ReturnedOnlyWithoutReporterFilter` |
| `SuggestCategoryHandler` | Reporter | Felhasználónkénti korlát: 5 kérés/perc (§8). A javaslat `RequestedByUserId`-ja a bejelentkezett felhasználó. A modell nem kap felhasználó-azonosítót és e-mail-címet (§9). | `SuggestCategoryHandlerTests.HandleAsync_AnonymousUser_ReturnsForbiddenWithoutCallingTheModel`<br>`SuggestCategoryHandlerTests.HandleAsync_Admin_ReturnsForbiddenWithoutCallingTheModel`<br>`SuggestCategoryHandlerTests.HandleAsync_SignedInUserWithoutRole_ReturnsForbidden`<br>`SuggestCategoryHandlerTests.HandleAsync_RateLimitExceeded_ReturnsRateLimitedWithWarningAndWithoutCallingTheModel`<br>`SuggestCategoryHandlerTests.HandleAsync_ValidSuggestion_SavesPendingSuggestionOnceAndReturnsIt`<br>`SuggestionRateLimiterTests.TryAcquire_SixthRequestWithinTheWindow_IsRefused`<br>`SuggestionRateLimiterTests.TryAcquire_OtherUserAfterTheLimitOfTheFirst_IsAllowed`<br>`LlmCategoryPromptTests.SuggestAsync_OutgoingRequest_ContainsNoEmailAddressOrUserIdentity` |
| `GetActiveCategoriesHandler` | bármely bejelentkezett felhasználó | Nincs: a kategóriák nem személyes adatok; csak az aktívak jönnek vissza (BR-13). | `GetActiveCategoriesHandlerTests.HandleAsync_AnonymousUser_ReturnsForbiddenWithoutQuery`<br>`GetActiveCategoriesHandlerTests.HandleAsync_SignedInUser_ReturnsActiveCategories` |
| `GetCurrentUserProfileHandler` | bármely bejelentkezett felhasználó | Mindig a saját profil: a query-ben nincs azonosító. Érvényes cookie mellett törölt felhasználóra `Forbidden`. | `GetCurrentUserProfileHandlerTests.HandleAsync_AnonymousUser_ReturnsForbiddenWithoutQuery`<br>`GetCurrentUserProfileHandlerTests.HandleAsync_Reporter_ReturnsOwnProfileWithoutAgent`<br>`GetCurrentUserProfileHandlerTests.HandleAsync_SignedInUserWithoutRole_ReturnsOwnProfile`<br>`GetCurrentUserProfileHandlerTests.HandleAsync_UserNoLongerExists_ReturnsForbidden` |

A Bejelentő által küldhető összes command és query (`CreateTicketCommand`, `SuggestCategoryCommand`,
`GetMyTicketsQuery`, `GetTicketDetailsQuery`, `GetActiveCategoriesQuery`, `GetCurrentUserProfileQuery`) szerveroldali
mezőt nem tartalmaz: `ReporterInputShapeTests.ReporterInput_Properties_ContainNoServerControlledField` (új).

### 1.2 Jegyek – Admin

Az Admin minden jegyet lát és kezel (BR-02), ezért ezeknél nincs felhasználó szerinti erőforrás-szűrés. A jegy
állapotára vonatkozó szabályokat (BR-03, BR-04, BR-07) a domain ellenőrzi; ezeket a BR-tesztek fedik (9. szakasz).

| Handler | Szükséges szerepkör | Erőforrás-ellenőrzés (3. szint) | Bizonyító tesztek |
|---|---|---|---|
| `GetTicketQueueHandler` | Admin | Nincs (Admin: minden jegy). | `GetTicketQueueHandlerTests.HandleAsync_AnonymousUser_ReturnsForbiddenWithoutQuery`<br>`GetTicketQueueHandlerTests.HandleAsync_Reporter_ReturnsForbiddenWithoutQuery` |
| `GetTicketQueueFiltersHandler` | Admin | Nincs. | `GetTicketQueueFiltersHandlerTests.HandleAsync_AnonymousUser_ReturnsForbiddenWithoutQuery`<br>`GetTicketQueueFiltersHandlerTests.HandleAsync_Reporter_ReturnsForbiddenWithoutQuery` |
| `GetAssignableAgentsHandler` | Admin | Lezárt jegyhez nincs kiosztható ügyintéző (BR-03). | `GetAssignableAgentsHandlerTests.HandleAsync_AnonymousUser_ReturnsForbiddenWithoutQuery`<br>`GetAssignableAgentsHandlerTests.HandleAsync_Reporter_ReturnsForbiddenWithoutQuery` |
| `AssignTicketManuallyHandler` | Admin | BR-03 és BR-04 a domainben. | `AssignTicketManuallyHandlerTests.HandleAsync_AnonymousUser_ReturnsForbiddenWithoutLoading`<br>`AssignTicketManuallyHandlerTests.HandleAsync_Reporter_ReturnsForbiddenWithoutLoading` |
| `RetryAutoAssignHandler` | Admin | Csak felelős nélküli, nem lezárt jegy (BR-03, BR-06). | `RetryAutoAssignHandlerTests.HandleAsync_AnonymousUser_ReturnsForbiddenWithoutLoading`<br>`RetryAutoAssignHandlerTests.HandleAsync_Reporter_ReturnsForbiddenWithoutLoading` |
| `RecategorizeTicketHandler` | Admin | BR-03, BR-07 és BR-13 a domainben. | `RecategorizeTicketHandlerTests.HandleAsync_AnonymousUser_ReturnsForbiddenWithoutLoading`<br>`RecategorizeTicketHandlerTests.HandleAsync_Reporter_ReturnsForbiddenWithoutLoading` |
| `ChangeImpactAndUrgencyHandler` | Admin | BR-03 és BR-11 a domainben. | `ChangeImpactAndUrgencyHandlerTests.HandleAsync_AnonymousUser_ReturnsForbiddenWithoutLoading`<br>`ChangeImpactAndUrgencyHandlerTests.HandleAsync_Reporter_ReturnsForbiddenWithoutLoading` |
| `StartWorkHandler` | Admin | Állapotgép (§10) a domainben. | `TicketStatusHandlersTests.HandleAsync_AnonymousUser_ReturnsForbiddenWithoutLoading(StartWork)`<br>`TicketStatusHandlersTests.HandleAsync_Reporter_ReturnsForbiddenWithoutLoading(StartWork)` |
| `ResolveTicketHandler` | Admin | Állapotgép a domainben. | `TicketStatusHandlersTests.HandleAsync_AnonymousUser_ReturnsForbiddenWithoutLoading(ResolveTicket)`<br>`TicketStatusHandlersTests.HandleAsync_Reporter_ReturnsForbiddenWithoutLoading(ResolveTicket)` |
| `ReopenTicketHandler` | Admin | Állapotgép a domainben. | `TicketStatusHandlersTests.HandleAsync_AnonymousUser_ReturnsForbiddenWithoutLoading(ReopenTicket)`<br>`TicketStatusHandlersTests.HandleAsync_Reporter_ReturnsForbiddenWithoutLoading(ReopenTicket)` |
| `CloseTicketHandler` | Admin | Állapotgép a domainben; a `Closed` terminális (BR-03). | `TicketStatusHandlersTests.HandleAsync_AnonymousUser_ReturnsForbiddenWithoutLoading(CloseTicket)`<br>`TicketStatusHandlersTests.HandleAsync_Reporter_ReturnsForbiddenWithoutLoading(CloseTicket)` |

### 1.3 Törzsadatok – Admin

| Handler | Szükséges szerepkör | Erőforrás-ellenőrzés (3. szint) | Bizonyító tesztek |
|---|---|---|---|
| `ListCategoriesHandler` | Admin | Nincs. | `ListCategoriesHandlerTests.HandleAsync_AnonymousUser_ReturnsForbiddenWithoutQuery`<br>`ListCategoriesHandlerTests.HandleAsync_Reporter_ReturnsForbiddenWithoutQuery` |
| `CreateCategoryHandler` | Admin | Nincs; egyedi kód (validátor és egyedi index). | `CreateCategoryHandlerTests.HandleAsync_AnonymousUser_ReturnsForbiddenWithoutChanges`<br>`CreateCategoryHandlerTests.HandleAsync_Reporter_ReturnsForbiddenWithoutChanges` |
| `UpdateCategoryHandler` | Admin | Nincs. | `UpdateCategoryHandlerTests.HandleAsync_AnonymousUser_ReturnsForbiddenWithoutLoading`<br>`UpdateCategoryHandlerTests.HandleAsync_Reporter_ReturnsForbiddenWithoutLoading` |
| `ActivateCategoryHandler` | Admin | Nincs. | `CategoryActivationHandlersTests.HandleAsync_AnonymousUser_ReturnsForbiddenWithoutLoading(Activate)`<br>`CategoryActivationHandlersTests.HandleAsync_Reporter_ReturnsForbiddenWithoutLoading(Activate)` |
| `DeactivateCategoryHandler` | Admin | Nincs (BR-13: nincs törlés). | `CategoryActivationHandlersTests.HandleAsync_AnonymousUser_ReturnsForbiddenWithoutLoading(Deactivate)`<br>`CategoryActivationHandlersTests.HandleAsync_Reporter_ReturnsForbiddenWithoutLoading(Deactivate)` |
| `ListAgentsHandler` | Admin | Nincs. | `ListAgentsHandlerTests.HandleAsync_AnonymousUser_ReturnsForbiddenWithoutQuery`<br>`ListAgentsHandlerTests.HandleAsync_Reporter_ReturnsForbiddenWithoutQuery` |
| `SetAgentCompetenciesHandler` | Admin | Új kompetencia csak aktív kategóriára (BR-13). | `SetAgentCompetenciesHandlerTests.HandleAsync_AnonymousUser_ReturnsForbiddenWithoutLoading`<br>`SetAgentCompetenciesHandlerTests.HandleAsync_Reporter_ReturnsForbiddenWithoutLoading` |
| `ActivateAgentHandler` | Admin | Nincs. | `AgentActivationHandlersTests.HandleAsync_AnonymousUser_ReturnsForbiddenWithoutLoading(Activate)`<br>`AgentActivationHandlersTests.HandleAsync_Reporter_ReturnsForbiddenWithoutLoading(Activate)` |
| `DeactivateAgentHandler` | Admin | A kiosztott jegyek maradnak (BR-12). | `AgentActivationHandlersTests.HandleAsync_AnonymousUser_ReturnsForbiddenWithoutLoading(Deactivate)`<br>`AgentActivationHandlersTests.HandleAsync_Reporter_ReturnsForbiddenWithoutLoading(Deactivate)` |

Összesen 26 handler; a `DependencyInjectionTests` ellenőrzi, hogy mindegyik scoped regisztrációt kap.

## 2. UI-szint (1. szint) – csak kényelmi réteg

| Védelem | Bizonyító tesztek |
|---|---|
| Az oldalak útvonala és `[Authorize(Roles = …)]` attribútuma (6 oldal) | `PageAuthorizationTests.AuthorizeAttribute_ProtectedPage_RequiresSignInAndExpectedRole` |
| A menü csak a szerepkör linkjeit mutatja | `NavMenuTests.Render_Reporter_ShowsReporterLinksOnly`<br>`NavMenuTests.Render_Admin_ShowsAdminLinksOnly`<br>`NavMenuTests.Render_Anonymous_ShowsRegisterAndLoginOnly` |
| Szerepkör miatt elutasított oldal (HTTP-kérés): az `AccessDenied` oldalra visz, Warning a naplóban (új) | `AccessDeniedLoggingTests.Get_AdminPageAsReporter_RedirectsToAccessDeniedAndLogsWarning`<br>`AccessDeniedLoggingTests.Get_PageOfOwnRole_LogsNoDenial`<br>`AccessDeniedLoggingResultHandlerTests.HandleAsync_Forbidden_LogsWarningWithPathAndUserAndForbids`<br>`AccessDeniedLoggingResultHandlerTests.HandleAsync_ChallengeOfAnonymousUser_ChallengesWithoutLog`<br>`AccessDeniedLoggingResultHandlerTests.HandleAsync_Success_CallsNextWithoutLog` |
| Szerepkör miatt elutasított oldal (circuiten belüli navigáció): magyar üzenet, Warning a naplóban (új) | `NotAuthorizedAlertTests.Render_SignedInUserWithoutRole_ShowsMessageAndLogsWarningWithPathAndUser` |

## 3. Identity és fiókok

| Szabály | Bizonyító tesztek |
|---|---|
| Jelszó legalább 10 karakter; lockout 5 hibás próbálkozás után 15 percre (§8) | `IdentityConfigurationTests.AddInfrastructure_IdentityOptions_FollowSecurityRules`<br>`IdentityConfigurationTests.CreateAsync_PasswordWith9Characters_FailsWithPasswordTooShort`<br>`IdentityConfigurationTests.CreateAsync_PasswordWith10Characters_Succeeds`<br>`IdentityConfigurationTests.CheckPasswordSignInAsync_FiveWrongPasswords_LocksOutFor15Minutes` |
| `RequireConfirmedAccount = false` (ADR-005) | `IdentityConfigurationTests.CheckPasswordSignInAsync_UnconfirmedEmail_Succeeds` |
| A szerepkörök minden környezetben létrejönnek, idempotensen; demóadat csak Development (ADR-009, új) | `ProductionStartupTests.Start_ProductionTwice_CreatesTheRolesOnceWithoutDemoData`<br>`DatabaseSeederTests.SeedRolesAsync_RunTwice_CreatesOnlyTheTwoRolesOnce`<br>`DatabaseSeederTests.InitializeDatabaseAsync_RolesSeededBefore_AddsOnlyTheDemoData`<br>`DatabaseSeederTests.InitializeDatabaseAsync_RunTwice_DoesNotChangeRecordCounts` |
| A regisztráció `Reporter` szerepkört ad; a szerepkör nem jön a klienstől (§8, új) | `ProductionStartupTests.PostRegister_Production_CreatesUserWithTheReporterRoleOnly` |
| Önkiszolgáló fióktörlés nincs (§6 kivétel, új) | `PersonalDataPagesTests.GetPersonalData_SignedInUser_KeepsDownloadAndMenuWithoutDeleteLink`<br>`PersonalDataPagesTests.GetDeletePersonalData_SignedInUser_ShowsNoticeWithoutForm`<br>`PersonalDataPagesTests.PostDeletePersonalData_FormOfTheTemplate_IsRefusedAndKeepsTheUser` |
| Felhasználóra hivatkozó jegy mellett a felhasználó nem törölhető (`Restrict` FK) | `UserForeignKeyTests.SaveChangesAsync_DeleteUserWithTicket_ThrowsForeignKeyViolation` |
| Kijelentkezés antiforgery-tokennel a hiba- és 404-es oldalról is (ADR-006) | `LogoutFormTests.Get_StatusCodeOrErrorPage_RendersStaticallyWithLogoutToken`<br>`LogoutFormTests.PostLogout_TokenOfStatusCodeOrErrorPage_SignsOutAndRedirectsBack` |

## 4. HTTP biztonsági fejlécek (ADR-008, új)

A `SecurityHeadersMiddleware` a pipeline elején, a `Response.OnStarting`-ban állítja be a fejléceket. Így a
kivételkezelő oldalán (amely előtte törli a fejléceket), a státuszkód-oldalakon és a statikus fájlokon is
megjelennek, és felülírják az antiforgery `X-Frame-Options: SAMEORIGIN` értékét.

| Fejléc | Érték | Bizonyító tesztek |
|---|---|---|
| `X-Content-Type-Options` | `nosniff` | `SecurityHeadersTests.Get_AnyResponse_HasEverySecurityHeaderOnce` (interaktív oldal, Account-űrlap, 404, 500, statikus fájl) |
| `Referrer-Policy` | `strict-origin-when-cross-origin` | ugyanez |
| `X-Frame-Options` | `DENY` | ugyanez |
| `Content-Security-Policy` (kikényszerített) | `frame-ancestors 'none'`: a keretrendszer saját fejléce, az alapértelmezett `'self'` helyett (a böngésző ezt előnyben részesíti az `X-Frame-Options`-szel szemben) | `SecurityHeadersTests.Get_InteractivePage_FrameworkFrameAncestorsPolicyIsNone`<br>`SecurityHeadersTests.Get_AnyResponse_HasEverySecurityHeaderOnce` |
| `Content-Security-Policy-Report-Only` | `default-src 'self'; script-src 'self' 'sha256-…'; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; font-src 'self' https://fonts.gstatic.com; img-src 'self' data:; connect-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'` | `SecurityHeadersTests.Get_Page_ScriptSourceAllowsOnlySelfAndTheImportMapOfThePage` (a hash egyezik az oldal importmapjével; 4 oldaltípus)<br>`SecurityHeadersTests.Get_StaticAsset_ScriptSourceAllowsOnlySelf`<br>`SecurityHeadersTests.Get_Page_PolicyAllowsTheSourcesOfMudBlazorAndTheCircuit` |

A hash futásidőben, ugyanabból az `ImportMapDefinition`-ből készül, amelyet az `<ImportMap />` kiír. Kézi
ellenőrzés: böngészőben, bejelentkezve, a fő oldalakon nem jelent meg CSP-jelentés a konzolban, és a SignalR-kapcsolat
működött (P-9A napló).

## 5. Naplózás (§8, §11)

| Szabály | Állapot | Bizonyító tesztek |
|---|---|---|
| Megtagadott use case → Warning | megvan | `UseCaseRunnerTests.RunAsync_ForbiddenResult_LogsWarningWithHandlerUserAndErrorCode` |
| Szerepkör miatt elutasított oldal → Warning | új (P-9A) | 2. szakasz |
| Idegen jegy (BR-02) → Warning, tartalom nélkül | megvan | `GetTicketDetailsHandlerTests.HandleAsync_ReporterForeignTicket_ReturnsNotFoundAndLogsWarningWithoutContent` |
| Idegen javaslat (BR-09) → Warning | megvan | `CreateTicketHandlerTests.HandleAsync_SuggestionOfOtherUser_ReturnsNotFoundWithoutChangesAndLogsWarning` |
| Rate limit túllépése → Warning | megvan | `SuggestCategoryHandlerTests.HandleAsync_RateLimitExceeded_ReturnsRateLimitedWithWarningAndWithoutCallingTheModel` |
| Jegycím és -leírás nem kerül a naplóba | megvan | `CreateTicketHandlerTests.HandleAsync_Success_LogsTicketCreatedWithoutContent`<br>`SuggestCategoryHandlerTests.HandleAsync_ValidSuggestion_LogsBusinessEventWithoutText`<br>`LlmCategorySuggesterTests.SuggestAsync_InvalidAnswerRepeatingTheTicket_LogsWarningWithoutTicketText` |
| Az LLM indoklása nem kerül a naplóba | megvan | `CreateTicketHandlerTests.HandleAsync_SuggestionDecided_LogsDecisionWithoutReasoning` |
| Konkurenciaütközés → Warning | megvan | `UnitOfWorkTests.SaveChangesAsync_SameTicketChangedInTwoContexts_ReturnsConcurrencyConflict`<br>`UnitOfWorkTests.SaveChangesAsync_StaleRowVersion_ReturnsConcurrencyConflict` |
| Váratlan kivétel → Error | megvan | `UseCaseRunnerTests.RunAsync_HandlerThrows_LogsErrorAndRethrows` |

Átnézve, mintakereséssel: a naplóhívások mind message template-et használnak (`[LoggerMessage]` vagy `Log…(template,
…)`), string-interpoláció nincs. Jelszó, cookie, API-kulcs és jegyleírás egyik naplóhívás paraméterei között sem
szerepel. `EnableSensitiveDataLogging` és HTTP-naplózó middleware nincs. A sablon Identity-oldalai csak
felhasználó-azonosítót naplóznak. A lockoutot Warning szinten naplózzák.

## 6. Kimenet-kódolás és AI (§6, §9)

| Szabály | Bizonyító tesztek |
|---|---|
| Felhasználói és LLM-szöveg sima szövegként, `MarkupString` nélkül | `TicketDetailsTests.Render_TicketWithMarkupInDescription_ShowsDescriptionAsPlainText`<br>`NewTicketTests.RequestSuggestion_Success_ShowsCategoryConfidenceAndReasoningAsPlainText`<br>`TicketDetailsAdminTests.Render_AdminWithAcceptedSuggestion_ShowsSuggestionConfidenceDecisionAndReasoning` |
| A jegy határolók közt, külön üzenetben; a határolók a szövegben semlegesítve | `LlmCategoryPromptTests.SuggestAsync_Messages_PutTheTicketBetweenDelimitersInASeparateUserMessage`<br>`LlmCategoryPromptTests.SuggestAsync_DelimitersInTheTicketText_AreNeutralized` |
| Nincs tool- vagy function calling | `LlmCategorySuggesterTests.SuggestAsync_Request_AsksForJsonSchemaWithoutTools` |
| A modell válasza validált (BR-08) | `LlmCategorySuggesterTests.SuggestAsync_CodeNotAmongActiveCategories_ReturnsNoSuggestionWithWarning`<br>`LlmCategorySuggesterTests.SuggestAsync_Confidence_IsClampedBetweenZeroAndOne`<br>`LlmCategorySuggesterTests.SuggestAsync_TooLongReasoning_IsCutTo500Characters` |

## 7. Tiltólista (§17) – ellenőrzés a teljes kódbázisban

| Tétel | Ellenőrzés | Eredmény |
|---|---|---|
| Régi Blazor/ASP.NET-minták, `Startup.cs` | keresés: `_Host`, `_Layout.cshtml`, `MapFallbackToPage`, `AddServerSideBlazor`, `MapBlazorHub`, `blazor.server.js`, `render-mode=`, `<CascadingAuthenticationState`, `InteractiveWebAssembly`, `InteractiveAuto` | nincs találat; `@rendermode` csak az `App.razor` sablonlogikájában (§6) |
| REST API vagy controller üzleti funkcióra | `MapGet`, `MapPost`, `Controller` | csak a sablon Identity-végpontjai (`IdentityComponentsEndpointRouteBuilderExtensions`) |
| MediatR, AutoMapper, Swashbuckle, Newtonsoft.Json, FluentAssertions, Moq, Scrutor, Docker | `Directory.Packages.props`, csproj-ok, `git ls-files` | nincs. A `System.Security.Cryptography.Pkcs` tranzitív biztonsági pin (P-1A), nem közvetlen hivatkozás |
| `DateTime.Now`/`UtcNow`, `.Result`, `.Wait()`, `async void` | keresés | nincs; a `dialog.Result` találatok a MudBlazor `IDialogReference.Result` `Task`-jai, `await`-tel |
| Publikus setter domain entitáson | `{ get; set; }` / `init` a Domainben | nincs |
| EF Core, ASP.NET Core, MudBlazor a Domainben/Applicationben | architektúra-tesztek (`DependencyRuleTests`) | zöld |
| DbContext, repository, `UserManager` oldal-komponensben; `IHttpContextAccessor` | `@inject`/`[Inject]` a `Components`-ben (Account kívül); `DependencyRuleTests.WebPages_AllTypes_DoNotDependOnForbiddenType` | csak `IUseCaseRunner`, `ISnackbar`, `IDialogService`, `NavigationManager`, `ILogger` |
| `MarkupString`, `FromSqlRaw` | keresés | nincs (csak tiltó kommentek) |
| Generikus repository; üzleti logika „Service” osztályban | `IRepository<`, `class …Service` | csak a §5 szerinti `TicketAssignmentService` |
| Titok a repóban; `EnsureCreated()` | `appsettings*.json`, keresés | nincs API-kulcs. A `Seed:DefaultPassword` a §7 szerint az `appsettings.Development.json`-ban van (ismert korlát). `EnsureCreated` nincs; a Web.Tests fixture-je `EnsureDeleted` + `MigrateAsync`-et használ |
| LLM tool calling, automatikus művelet a modell kimenetéből | `ChatOptions` | `Tools` nincs; a javaslatról ember dönt (BR-09) |

Eredmény: kódsértés nem volt. A formátum-ellenőrzés (`dotnet format --verify-no-changes`) eltéréseit javítottam: 27 db,
sorvég és import-sorrend. A `Components/_Imports.razor` 6 nem használt usingját töröltem.

## 8. Teszt nélküli vagy hiányos pontok, amelyeket ez a lépés pótolt

- Over-posting (§8): nem volt teszt a command-ok alakjára → `ReporterInputShapeTests`.
- Oldalszintű megtagadás: nem került a naplóba → `AccessDeniedLoggingResultHandler`, `NotAuthorizedAlert` és tesztjeik.
- Szerepkörök Development-en kívül: nem jöttek létre, a regisztráció hibát adott → `RoleSeeder` és tesztjei.
- Fióktörlés: a sablon oldala a `Restrict` FK miatt kivételre futhatott → tájékoztató oldal és tesztjei.
- Biztonsági fejlécek: nem voltak → `SecurityHeadersMiddleware` és tesztjei.

## 9. BR-lefedettség (`[Trait("BR", …)]` jelölésű tesztek száma projektenként)

| BR | Domain | Application | Infrastructure | Web |
|---|---|---|---|---|
| BR-01 | 12 | 14 | 0 | 0 |
| BR-02 | 0 | 7 | 5 | 3 |
| BR-03 | 8 | 8 | 4 | 7 |
| BR-04 | 6 | 9 | 5 | 2 |
| BR-05 | 12 | 5 | 3 | 0 |
| BR-06 | 3 | 8 | 2 | 3 |
| BR-07 | 6 | 7 | 0 | 3 |
| BR-08 | 5 | 4 | 13 | 1 |
| BR-09 | 3 | 13 | 5 | 12 |
| BR-10 | 16 | 3 | 3 | 1 |
| BR-11 | 6 | 4 | 0 | 2 |
| BR-12 | 6 | 8 | 4 | 3 |
| BR-13 | 14 | 11 | 10 | 11 |

Mind a 13 BR-hez van jelölt teszt. A BR-02-nek nincs Domain-tesztje: a szabály a lekérdezés szűrése (3. szint),
domain-logikája nincs. Ellenőrzés: `dotnet test --solution ServiceDesk.slnx --filter "BR=BR-02"`.

## 10. A táblázat ellenőrzése

A fenti dokumentumban szereplő összes `Osztály.Metódus` tesztnevet szkript kereste ki a `tests/**/*.cs` fájlokban
(osztály és metódus együtt); mindegyik megtalálható. A teljes futás zöld (P-9A napló).

## 11. Ismert korlátok

Forrás: az összes prompt-napló (`docs/prompts/P-1A` … `P-8B`) ismert korlátai és a P-9A új korlátai.
A „Megszűnt” részben a már feloldott korlátok szerepelnek.

### 11.1 Nyitott korlátok

| # | Korlát (forrás) | Kockázat | Indok | Lehetséges javítás |
|---|---|---|---|---|
| 1 | **Az `Agent` táblán nincs konkurenciatoken – 1. versenyhelyzet.** Ha egy ügyintézőt egy kiosztás (automatikus vagy kézi) betöltése és mentése között inaktiválnak, a jegyet még megkaphatja (BR-04, BR-12). (P-6A, P-7A) | Közepes: egy inaktív ügyintéző egy jegyet kaphat; kézi átosztással javítható. | A felhasználó elfogadta (P-7A). Ritka: az ütközés csak egy szűk időablakban történhet. | `RowVersion` az `Agent`-en; a kiosztás az `Agent`-et is módosítja (`LastAssignedAt`), így az ütközés `ConcurrencyConflict` lenne. |
| 2 | **Az `Agent` (és a `Category`) táblán nincs konkurenciatoken – 2. versenyhelyzet.** Ha egy kategóriát egy jegy létrehozása vagy egy kompetencia-beállítás betöltése és mentése között inaktiválnak, a jegy vagy a kompetencia inaktív kategóriára kerülhet (BR-13). (P-7A) | Közepes: egy jegy vagy kompetencia inaktív kategóriát kap; Admin átkategorizálhatja. | Elfogadva (P-7A). | `RowVersion` a `Category`-n, és a kategória „használata” optimista ellenőrzéssel (vagy tranzakció `SERIALIZABLE` szinten). |
| 3 | Két egyszerre futó automatikus kiosztás ugyanazt a legkevésbé terhelt ügyintézőt választhatja; a `LastAssignedAt`-nél az utolsó írás nyer. (P-3A, P-5A, P-6A) | Alacsony: csak az elosztás egyenletessége, BR-04 nem sérül. | A terhelés olvasása és a kiosztás között nincs zárolás. | Ugyanaz, mint az 1.: `Agent`-konkurenciatoken és újrapróbálás. |
| 4 | Két egyszerre szerkesztett ügyintéző- vagy kategóriaadat közül az utolsó mentés nyer; két egyidejű, azonos kompetencia-felvétel PK-sértése váratlan hibaként jelenik meg. (P-7A, P-7B) | Alacsony: adatvesztés egy egyidejű szerkesztésnél. | A törzsadatokon nincs konkurenciatoken. | `RowVersion` és ütközésjelzés; a PK-sértés `Conflict` eredménnyé alakítása a `UnitOfWork`-ben. |
| 5 | **A seed-jelszó helye:** a `Seed:DefaultPassword` az `appsettings.Development.json`-ban, a repóban van. (§7, P-3A) | Alacsony: aki a repót látja, ismeri a demófiókok jelszavát. Élesben a demóseeder nem fut (ADR-009), ott ezek a fiókok nem jönnek létre. | A §7 írja elő (egyszerű demó). | User Secrets (`dotnet user-secrets set Seed:DefaultPassword …`) vagy környezeti változó; a repóból törlés. |
| 6 | **Angol Account-oldalak:** az Identity-oldalak, a `NotFound`, az `Error`, a `ReconnectModal` és a közvetlen URL-en látott `AccessDenied` angol szövegűek; a `PersonalData` bevezetője még „download or delete”-et említ. (P-1B, P-4A, P-4A-fix1) | Alacsony (UX, §6): a felhasználó vegyes nyelvű felületet lát. | A sablon oldalait csak a dokumentált pontokon módosítjuk (§6). | Külön lépésben a sablonoldalak magyarítása (a §6 kivételének bővítésével). |
| 7 | A szerepkörök a cookie claimjeiből jönnek; egy szerepkör elvétele a revalidációs időközig (30 perc) vagy újbóli belépésig nem hat. (P-4B) | Közepes: egy elvett Admin-jog legfeljebb 30 percig még él. | A sablon `IdentityRevalidatingAuthenticationStateProvider` mechanizmusa; szerepkör-kezelő felület nincs (Admin csak seedből). | Szerepkör-változáskor `UpdateSecurityStampAsync`; rövidebb revalidációs időköz. |
| 8 | Az `ExternalLogin.razor` szerepkör nélkül hozna létre felhasználót. (P-4A) | Alacsony, látens: külső szolgáltató nincs beállítva, az ág nem érhető el. | A prompt nem engedte az oldalt módosítani. | Külső szolgáltató felvételekor ott is `AddToRoleAsync(user, Roles.Reporter)`. |
| 9 | A modellhívás kivételét (`LogFailed`) teljes kivétellel naplózzuk; egy szolgáltatói hibaüzenet elvben visszaidézheti a kérés egy részét. (P-8A) | Alacsony: jegyszöveg-részlet kerülhet a naplóba (§8). | A kivétel típusa és üzenete a hibakereséshez kell. | Csak a kivétel típusának és a HTTP-állapotkódnak a naplózása. |
| 10 | A rate limiter folyamaton belüli és valós idő szerint számol; több példánynál példányonként számolna. A sikertelen (`NoSuggestion`) kérés is fogyasztja a keretet. (P-8A) | Alacsony: a projekt egypéldányos. | A `System.Threading.RateLimiting` nem fogad `TimeProvider`-t. | Elosztott limiter (pl. adatbázis- vagy cache-alapú), ha több példány lesz. |
| 11 | Az e-mail-cím felismerése mintaalapú (idézőjeles helyi rész, IP-literál nem cserélődik); más személyes adat (név, telefonszám) nem cserélődik. (P-8A) | Alacsony: személyes adat juthat a modellhez (lokális Ollama esetén a gépen marad). | A §9 csak az azonosítót és az e-mail-címet tiltja. | Bővebb PII-szűrő, ha külső szolgáltatót használunk. |
| 12 | Az LLM-indoklásból a HTML-jelölés nem törlődik (csak a vezérlőkarakterek). (P-8A) | Nincs: a UI szövegként kódolja (6. szakasz). | Nincs rá szükség, a kódolás véd. | – |
| 13 | El nem döntött (`Pending`) javaslatok az adatbázisban maradnak; takarítás nincs. (P-8A, P-8B) | Alacsony: adatgyűlés. | A javaslat a döntésig nem köthető jegyhez. | Időzített takarítás a régi `Pending` sorokra. |
| 14 | A `GetMyTickets` és a `GetTicketQueue` oldalszámának nincs felső korlátja; extrém értéknél túlcsordulás, kivétel. (P-5A, P-6A) | Alacsony: csak a saját circuitet érinti (ErrorBoundary). | A felületről ilyen érték nem érkezik. | Felső korlát a validátorban. |
| 15 | **CSP csak Report-Only**, `report-uri` nélkül; a jelentések csak a böngészőkonzolban látszanak. A `style-src` `'unsafe-inline'`-t enged. (P-9A, ADR-008) | Közepes: a CSP nem véd XSS ellen, amíg nincs kikényszerítve. | A feladat Report-Only módot kért; a MudBlazor inline stílust használ. | Megfigyelési időszak után kikényszerített CSP; jelentésgyűjtő végpont (új funkció, külön döntés). |
| 16 | A CSP-hash az `ImportMap` komponens kimenetére épül (`ImportMapDefinition.ToString()`, CR LF → LF). Ha egy keretrendszer-frissítés megváltoztatja a kiírást, a hash eltér. (P-9A) | Alacsony: Report-Only módban csak jelentés lesz belőle. | Nincs publikus API a kiírt szövegre. | A `SecurityHeadersTests` jelzi az eltérést; szükség esetén nonce az `<ImportMap nonce=…>`-on. |
| 17 | Developmenten kívül nincs automatikus migráció: a sémát telepíteni kell, különben az indulás a szerepkör-seednél hibát ad. (P-9A, ADR-009) | Alacsony: telepítési lépés. | A §7 az automatikus migrációt csak Developmentben engedi. | Telepítéskor `dotnet ef database update` vagy migrációs szkript. |
| 18 | A HTTP-tesztek nagy része teszt-hitelesítési sémát és csonkolt szolgáltatásokat használ; az interaktív (circuitből indított) kijelentkezést csak a kézi protokoll fedi. (P-4A-fix1) | Alacsony. | A HTTP-tesztek nem vezérlik a circuitet. | Böngészőalapú E2E-teszt (nem engedélyezett csomag, külön döntés). |
| 19 | A `Restrict` FK-teszt csak a `Ticket.ReporterId`-t fedi; az `Agent.UserId` és a `CategorySuggestion.RequestedByUserId` ugyanígy van konfigurálva, de külön teszt nincs. (P-3B) | Alacsony. | A konfiguráció azonos. | Két további `UserForeignKeyTests`-eset. |
| 20 | A NetArchTest a nem használt projekt- vagy csomaghivatkozást és az attribútumban lévő `typeof(...)`-t nem látja. (P-1A) | Alacsony: egy tiltott hivatkozás a csproj-ban észrevétlen maradhat. | IL-alapú elemzés. | Csproj-szintű teszt a `PackageReference`-listára. |
| 21 | A sorszám-ütközés felismerése SQL Server-specifikus (2601-es hibaszám, indexnév). (P-3A) | Nincs a rögzített stacken (§3). | SQL Server LocalDB a stack. | Szolgáltatófüggetlen kivételfordító, ha a stack változna. |
| 22 | A tesztekhez helyi `MSSQLLocalDB` kell; a `ServiceDesk_Tests` és a `ServiceDesk_WebTests` adatbázis a futások között megmarad; egy kézzel átírt, már alkalmazott migrációnál a `ServiceDesk_Tests`-et kézzel kell törölni. A Web-szintű adatbázistesztek saját adatbázist használnak (a §7 csak a `ServiceDesk_Tests`-et említi). (P-3B, P-9A) | Alacsony. | A Respawn-reset és a projektek párhuzamos futása miatt kell a külön adatbázis. | – |
| 23 | Egy váratlan kivétel kétszer is megjelenhet a naplóban (a runner Error-szinten és a keretrendszer `ErrorBoundary`-naplózója). (P-1B, P-4B) | Nincs (zaj). | A keretrendszer viselkedése. | – |
| 24 | A Reporter `NotFound` Warning-naplója a nem létező és az idegen jegyet egyformán rögzíti. (P-4B, P-5A) | Nincs: szándékos (BR-02). | A két eset nem különböztethető meg. | – |
| 25 | A kiosztható ügyintézők és a törzsadatlisták sorrendje az adatbázis rendezését követi, nem a `hu-HU` kultúrát; a törzsadatlisták nem lapoznak és nem rendezhetők. (P-6A, P-7A, P-7B) | Nincs (UX). | Kevés adat. | Kollációs rendezés vagy memóriabeli `hu-HU` rendezés. |
| 26 | Inaktív kategóriájú kompetencia nem vehető el, csak a kategória újraaktiválása után. (P-7B, K2) | Nincs (UX). | BR-13 szerinti döntés. | – |
| 27 | Felületi apróságok: elavult oldalon kattintva `InvalidTransition` jön „Frissítés” akció nélkül; a lezárt jegyre kért státuszváltás közös üzenetet kap; a jegysor szűrői nem kerülnek az URL-be; az Átosztás gombja „Kiosztás”; a saját jegyek listája nem rendezhető; a kategórialista inaktiválás után nem töltődik újra; a MudBlazor beépített szövegei nincsenek magyarítva. (P-5B, P-6A, P-6B) | Nincs (UX). | Hatókörön kívül. | Külön UI-lépés. |
| 28 | Egy jegy kategóriadöntése a rögzítéskori kategóriából következik (kézzel ugyanazt választva `Accepted`); az Admin-kártya nem mutatja külön a Bejelentő választását; `Temperature = 0` mellett is eltérhet az indoklás; az Ollama hidegindítása túllépheti a 20 s-ot. (P-8B) | Nincs (viselkedés). | A prompt szabálya és az ADR-007. | – |
| 29 | A domain nem kényszeríti ki, hogy az `Unassign` csak BR-07 esetén fusson; a kiosztási stratégia nem szűr, a jelöltlistát (BR-04) és a nyitott jegyek prioritásait az Application állítja össze; ismétlődő `AgentId` nincs ellenőrizve. (P-2A, P-2B) | Alacsony: csak programozási hibánál. Az `AssignTo` BR-04-ellenőrzése véd. | A domain nem ismeri az összes ügyintézőt. | – |
| 30 | A `Guid.CreateVersion7()` a rendszerórát olvassa (egyetlen implicit időlekérdezés); a `TicketLabels` az `Europe/Budapest` IANA-zónát használja (ICU kell); a seeder a meglévő seed-sorokat nem frissíti. (P-2A, P-3A, P-5B) | Nincs. | A §5 írja elő; a .NET 10 ICU-t használ; idempotens seed. | – |
| 31 | Fejlesztői adatok: a fejlesztői `ServiceDesk` adatbázisban a korábbi kézi próbák tesztfiókjai (`teszt.p1b`, `teszt.p1b2`, `teszt.p4a`, szerepkör nélkül) és jegyei maradtak. (P-1B, P-3A, P-4A, P-5B, P-6B) | Nincs (csak fejlesztői adatbázis). | Kézi ellenőrzések nyoma. | `dotnet ef database drop`, majd indítás. |
| 32 | Kisebb UI-korlátok: az AppBar-név csak új circuitben frissül; az `ErrorBoundary` tartalékában nincs Kijelentkezés gomb; a statikus Account-oldalak kérésenként lekérdezik a profilt; közvetlenül megnyitott 404-es oldalról kijelentkezve a visszairányítás a `/not-found`-ra visz. (P-4A-fix1, P-4B) | Nincs. | Jóváhagyott döntések. | – |
| 33 | Az oldaltesztek a runner helyettesítése miatt a `SetAgentCompetencies` és a `Deactivate…` commandok azonosítóit nem ellenőrzik (a dialógus- és use case-tesztek fedik); a gomb aktiválását a felület a validátor tükörképével ellenőrzi. (P-7B, P-8B) | Nincs. | – | – |

### 11.2 Megszűnt korlátok

| Korlát | Forrás | Megszűnt |
|---|---|---|
| A szerepkörök csak a Development-seederben jöttek létre; máshol a regisztráció hibát adott | P-4A | P-9A (`RoleSeeder`, ADR-009) |
| A sablon személyesadat-törlő oldala a `Restrict` FK miatt hibára futhatott | P-3A, P-3B | P-9A (tájékoztató oldal, §6 kivétel) |
| A megtagadott oldalhozzáférés nem került a naplóba | P-9A átvizsgálás | P-9A (`AccessDeniedLoggingResultHandler`, `NotAuthorizedAlert`) |
| A formátum-ellenőrzés Windows-on elbukott (LF-fájlok, rögzítetlen sorvég) | P-9A átvizsgálás | P-9A (`.editorconfig` `end_of_line = lf`, `.gitattributes`) |
| A leállított LocalDB első indítása elbuktatta az Infrastructure-teszteket | P-9A átvizsgálás | P-9A (`Connect Timeout=60`) |
| A duplikált kategóriakód kivételként jött ki | P-3B | P-7A (`CategoryErrors.CodeAlreadyExists`) |
| Nem volt integrációs teszt; a tiszta `RowVersion`-ág nem futott | P-3A | P-3B |
| Az Application `DependencyInjectionTests` üresen teljesült | P-4A | P-4B |
| Az előzmények értékei angol enumnevek | P-2A, P-5A | P-5B (`TicketLabels`) |
| Azonos időbélyegű történetbejegyzések sorrendje | P-2A | P-3A (`Sequence`) |
| A `SetCompetencies` nem ellenőrizte a kategóriát | P-2A | P-7A (`UpdateCompetencies`, BR-13) |
| Az ErrorBoundary csak Warning szinten naplózott | P-1B | P-4A (`UseCaseRunner`, Error szint) |
| A meg nem erősített fiókok nem tudtak belépni | P-1B | ADR-005 (`RequireConfirmedAccount = false`) |
| Az AI-javaslat `Pending` maradt, döntés nem volt | P-8A | P-8B (a döntés a létrehozáskor) |
