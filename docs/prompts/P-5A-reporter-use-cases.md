# P-5A – Bejelentői use case-ek és automatikus kiosztás
- Dátum: 2026-10-29
- Eszköz és modell: Claude Code (asztali alkalmazás), Claude Opus 5.5 (`claude-opus-5-5`)
- Cél és érintett szabályok:
  - `TicketAssignmentService.AutoAssignAsync`; `CreateTicket`; `ITicketQueries` és implementációja (`GetMyTickets`,
    `GetActiveCategories`, `GetTicketDetails`); validátorok; tesztek.
  - A jóváhagyáskor kért bővítések: mezőhiba-lista az `Error`-on (K6), Warning-napló a Reporter `NotFound`
    eredményére (BR-02).
  - Üzleti szabályok: BR-01, BR-02, BR-04, BR-05, BR-06, BR-10, BR-11, BR-13.
  - AGENTS.md: §1, §3, §5, §7, §8, §10, §11, §12, §13, §14.
- Átadott kontextus:
  - `AGENTS.md`, `CLAUDE.md`, `docs/prompts/P-4B-authorization.md`.
  - Domain: `Ticket.cs`, `TicketErrors.cs`, `TicketHistoryEntry.cs`, `TicketHistoryAction.cs`, `PriorityMatrix.cs`,
    a `Tickets` enumjai, `Agent.cs`, `AgentErrors.cs`, `Assignment/*`, `Category.cs`, `CategoryErrors.cs`, `Common/*`.
  - Application: minden meglévő fájl (portok, hibák, `CurrentUserAuthorization`, `DependencyInjection`,
    `GetCurrentUserProfile`).
  - Infrastructure: `DependencyInjection.cs`, `AppDbContext.cs`, `UserQueries.cs`, a repositoryk, `UnitOfWork.cs`,
    a `Ticket`, `TicketHistoryEntry` és `Agent` konfigurációja.
  - Tesztek: Application (`TestUsers`, `DependencyInjectionTests`, handler-tesztek), ArchitectureTests,
    Infrastructure (fixture, `IntegrationTest`, builderek, `UserQueriesTests`, `AgentRepositoryTests`),
    Domain (`TestData`, `ResultTests`), Web (`RecordingLogger`, `ServiceDeskWebApplicationFactory`).
  - `Directory.Packages.props`, `Directory.Build.props`, `.editorconfig`.

## Prompt
[P-5A] Bejelentői use case-ek és automatikus kiosztás
Kontextus: AGENTS.md §5, §8, §10 (BR-01, BR-02, BR-04, BR-05, BR-06, BR-10, BR-11, BR-13), §12. A 4. lépés elkészült.

Feladat:
1. TicketAssignmentService (Application): AutoAssignAsync(ticket, ct). A GetEligibleCandidatesAsync eredményéből a LeastLoadedAssignmentStrategy választ. Ha van jelölt: Ticket.AssignTo(agent, actorUserId: null, now) és Agent.RecordAssignment(now). Ha nincs, a jegy New marad (BR-06). Idő: TimeProvider.
2. CreateTicket (Title, Description, Impact, Urgency, CategoryId): csak Reporter (BR-01); a ReporterId az ICurrentUser-ből jön; a kategória létező és aktív (BR-13); létrehozás, majd AutoAssignAsync, egyetlen SaveChanges. Visszaadja az új jegy azonosítóját.
3. ITicketQueries és Infrastructure-implementációja AsNoTracking-projekcióval:
   - GetMyTickets (Reporter): csak saját jegyek (BR-02), legújabb elöl, oldalméret 20.
   - GetActiveCategories: az aktív kategóriák (kód, név, leírás) a létrehozó űrlaphoz; bármely bejelentkezett felhasználó.
   - GetTicketDetails(id): Reporter csak a sajátját kapja, idegen jegyre TicketErrors.NotFound (BR-02); Admin bármelyiket. Tartalom: alapadatok, prioritás, kategória, felelős neve, előzmények időrendben.
4. Validátorok a §10 mezőkorlátaival; a validációs hibák kódja Validation.<Property>.
5. Tesztek (NSubstitute, [Trait("BR", …)]):
   - CreateTicket: Admin és névtelen felhasználó → Forbidden; inaktív kategória → hiba; van jelölt → kiosztva; nincs jelölt → New.
   - GetTicketDetails: idegen jegy → NotFound; saját jegy és Admin → siker.

Ne csináld: UI (P-5B), AI-javaslat (8. lépés), Admin-műveletek (6. lépés).

Elfogadás: a build és minden teszt zöld; minden új handlerhez van autorizációs teszt.

Zárás: AGENTS.md §1 és §14, napló: P-5A-reporter-use-cases.

Jóváhagyás a terv után (eltérésekkel):
Jóváhagyom a K1–K5 javaslataival, a következő módosításokkal:
- K6: ne maradjon ismert korlát. A Domain/Common Error kapjon opcionális, csak olvasható mezőhiba-listát (FieldErrors: mezőnév → üzenetek), amelyet a Validation típusú hibák töltenek ki. A ValidatorExtensions az összes FluentValidation-hibát átadja; a fő Code maradjon az első hiba Validation.<Property> kódja a visszafelé kompatibilitás miatt. Teszt: több hibás mezőnél az összes mezőhiba megjelenik a Result-ban. Rögzítsd az AGENTS.md §11-ben is.
- BR-02 naplózás: a GetTicketDetails handler Reporter esetén a NotFound eredményt Warning szinten naplózza (UserId, TicketId), a jegy tartalma nélkül. Teszt rá.
Mehet a megvalósítás.

(A munka egy használati korlát miatt megszakadt; a felhasználó „Folytasd” üzenetére fejeződött be.)

## Eredmény
- Létrehozott és módosított fájlok:
  - `AGENTS.md` §11: új pont a `FieldErrors`-ról (K6). **A szabályrendszer ezzel a lépéssel bővült**; más szabály nem
    változott.
  - `Directory.Packages.props`: `Microsoft.Extensions.Logging.Abstractions` 10.0.14 (Application) és
    `Microsoft.Extensions.TimeProvider.Testing` 10.10.0 (Tesztek); mindkettő a §3 engedélyezett listáján van.
  - `src/ServiceDesk.Domain`:
    - `Common/Error.cs`: `FieldErrors` tulajdonság, `Error.Validation(...)` factory, tartalom szerinti egyenlőség;
    - `Tickets/TicketErrors.cs`: `NotFound`; `Categories/CategoryErrors.cs`: `NotFound`.
  - `src/ServiceDesk.Application`:
    - `ServiceDesk.Application.csproj`: `Microsoft.Extensions.Logging.Abstractions`;
    - `DependencyInjection.cs`: `TryAddSingleton(TimeProvider.System)`,
      `IAssignmentStrategy` → `LeastLoadedAssignmentStrategy` (singleton), `TicketAssignmentService` (scoped);
    - új `Common/Validation/ValidatorExtensions.cs`, `Common/Paging/PagedResult.cs`,
      `Common/Persistence/ITicketQueries.cs`, `Tickets/TicketAssignmentService.cs`;
    - új feature-mappák:
      - `Tickets/CreateTicket/`: `CreateTicketCommand`, `CreateTicketHandler`, `CreateTicketValidator`;
      - `Tickets/GetMyTickets/`: `GetMyTicketsQuery`, `GetMyTicketsHandler`, `GetMyTicketsValidator`, `MyTicketDto`;
      - `Tickets/GetActiveCategories/`: `GetActiveCategoriesQuery`, `GetActiveCategoriesHandler`, `ActiveCategoryDto`;
      - `Tickets/GetTicketDetails/`: `GetTicketDetailsQuery`, `GetTicketDetailsHandler`, `TicketDetailsDto`,
        `TicketHistoryEntryDto`.
  - `src/ServiceDesk.Infrastructure`: új `Persistence/Queries/TicketQueries.cs`; `DependencyInjection.cs`:
    `AddScoped<ITicketQueries, TicketQueries>()`.
  - Tesztek:
    - `tests/ServiceDesk.Domain.Tests`: új `Common/ErrorTests.cs` (8).
    - `tests/ServiceDesk.Application.Tests`:
      - `ServiceDesk.Application.Tests.csproj` (`Microsoft.Extensions.TimeProvider.Testing`);
      - új segédek: `TestData.cs`, `RecordingLogger.cs`, `LogEntry.cs` (a Web.Tests mintájára);
      - új tesztek: `Common/Validation/ValidatorExtensionsTests.cs` (3), `Tickets/TicketAssignmentServiceTests.cs` (4),
        `Tickets/CreateTicket/CreateTicketHandlerTests.cs` (10), `Tickets/CreateTicket/CreateTicketValidatorTests.cs` (14),
        `Tickets/GetMyTickets/GetMyTicketsHandlerTests.cs` (4),
        `Tickets/GetActiveCategories/GetActiveCategoriesHandlerTests.cs` (3),
        `Tickets/GetTicketDetails/GetTicketDetailsHandlerTests.cs` (6);
      - `DependencyInjectionTests.cs` (+2).
    - `tests/ServiceDesk.Infrastructure.Tests`: új `Persistence/TicketQueriesTests.cs` (8); a builderek opcionális
      beállítást kaptak: `CategoryBuilder.WithName`, `AgentBuilder.WithDisplayName`, `TicketBuilder.CreatedAt`.
      Az alapértékek nem változtak, így a meglévő tesztek változatlanok.
  - Ez a napló. Migráció nem kellett, mert a modell nem változott. ADR nem készült (lásd lent).
- Döntések és indoklásuk:
  - **A felhasználó döntései (a terv jóváhagyásakor):**
    - **K1 – `RecordAssignment`:** a `Ticket.AssignTo` sikeres kiosztáskor már meghívja az
      `agent.RecordAssignment(now)`-t, ezért a szolgáltatás nem hívja még egyszer. A teszt a `LastAssignedAt`
      értékét ellenőrzi.
    - **K2 – nem létező kategória:** `CategoryErrors.NotFound` (`Category.NotFound`, `ErrorType.NotFound`).
    - **K3 – kötelező enumok:** a command `Impact?` és `Urgency?` mezőt kapott, mert az `Impact.High` és az
      `Urgency.High` értéke 0, így egy kitöltetlen mező csendben „Magas” lenne. A validátor `NotNull` + `IsInEnum`;
      a `CategoryId` `Guid`, `NotEmpty` szabállyal.
    - **K4 – nevek feloldása az előzményekben:** a `TicketQueries` a szereplőt névre fordítja (ügyintézői név, ennek
      hiányában e-mail; `null` = rendszer). A kiosztási sorok (`Assigned`, `Reassigned`, `Unassigned`) ügyintéző-,
      az átkategorizálási sorok kategóriaazonosítóit is névre fordítja. A többi érték (pl. állapotnév) változatlan.
      - Lekérdezések: egy a jegyre az előzményekkel, utána egy-egy a szereplők, ügyintézők és kategóriák nevére
        (`Contains`-szel). A szám állandó, nincs N+1. Üres azonosítólistánál az ügyintézői és a kategórianév-lekérdezés
        elmarad.
    - **K5 – üzleti napló:** a `CreateTicketHandler` a sikeres mentés után Information szinten naplóz (`TicketId`,
      `ReporterId`, `AssigneeAgentId`), cím és leírás nélkül (§8). Ehhez az Application először kapott naplózócsomagot.
      A naplóhívás `[LoggerMessage]` forrásgenerátorral készült (a handler ezért `partial`), message template-tel (§11).
    - **K6 – `Error.FieldErrors` (módosítással):**
      - `IReadOnlyDictionary<string, IReadOnlyList<string>>`, kulcsa a tulajdonság neve (pl. `Title`). Csak olvasható
        (`private init`); csak az `Error.Validation(...)` tölti ki. Más hibánál üres, nem `null`, így a hívónak nem
        kell null-ellenőrzés.
      - Az `Error.Validation(...)` a `Code`-ot (`Validation.<Property>`) és a `Message`-et az első hibából veszi; ugyanaz
        a mező több üzenettel csoportosítva, sorrendben szerepel.
      - A `record` szintetizált egyenlősége a szótárt referencia szerint hasonlítaná. Ezért saját `Equals` és
        `GetHashCode` készült, amely a mezőhibákat tartalom szerint veti össze. A statikus hibakonstansok egyenlősége
        nem változott.
      - `ValidatorExtensions.ValidateToResultAsync`: az összes FluentValidation-hibát átadja.
    - **BR-02 napló (módosítással):** a `GetTicketDetailsHandler` Reporter esetén `NotFound` eredménynél Warninget ír
      (`UserId`, `TicketId`), jegytartalom nélkül. Admin esetén nem naplóz. A napló a nem létező és az idegen jegyet
      nem különbözteti meg, mert a szűrt lekérdezés egyikről sem ad adatot.
  - **Handler-sorrend (§5):**
    - `CreateTicket`:
      - (1) `RequireRole(Reporter)`;
      - (2) validáció;
      - (3) a kategória betöltése;
      - (4) `Ticket.Create` (inaktív kategóriánál `CategoryErrors.Inactive`, BR-13), `Add`, `AutoAssignAsync`;
      - (5) egyetlen `SaveChangesAsync`;
      - (6) az új jegy `Guid`-ja.
    - `GetMyTickets`: (1) `RequireRole(Reporter)`, (2) validáció (`PageNumber ≥ 1`), (3) lekérdezés a saját Id-val
      és 20-as oldalmérettel.
    - `GetActiveCategories`: (1) `RequireAuthenticated`; nincs bemenet, így validáció sem.
    - `GetTicketDetails`: (1) `RequireAuthenticated`, majd szerepkör szerint Admin → szűrő nélkül, Reporter → saját
      Id-s szűrő, egyéb → `Forbidden`. A szűrő a lekérdezés része (§8, 3. szint). Validáció nincs, mert az üres
      `Guid` egyszerűen `NotFound`.
  - **`TicketAssignmentService`:**
    - Sem nem autorizál, sem nem ment; ezt a hívó handler végzi.
    - A jelölteket a repository adja (BR-04), a választást az injektált `IAssignmentStrategy` végzi (BR-05).
    - A kiválasztott ügyintézőt teljes aggregátumként tölti be, és a kiosztás `Ticket.AssignTo(agent, null, now)`,
      amely a BR-03/BR-04 szabályt újra ellenőrzi.
    - Ha a betöltött ügyintéző mégsem jogosult (elavult jelöltlista), a domain hibája visszajut, és a jegy nem mentődik.
    - Ha a kiválasztott ügyintéző nem tölthető be, az programozási hiba (ügyintéző nem törölhető), ezért kivétel.
  - **`TimeProvider`:** az `AddApplication` `TryAddSingleton`-nel regisztrálja, így egy korábban regisztrált óra
    (pl. teszt) megmarad.
  - **Lapozás:** `PagedResult<T>(Items, PageNumber, PageSize, TotalCount)`, 1-től számozott oldalakkal. A sorrend
    `CreatedAt` szerint csökkenő, egyezésnél `Id`, hogy a lapok határa stabil legyen. Az utolsón túli oldal üres,
    a `TotalCount` értékével.
  - **DTO-k:**
    - Az `ActiveCategoryDto` `Id`-t is tartalmaz, mert a `CreateTicketCommand.CategoryId` erre hivatkozik.
    - A `TicketDetailsDto`-ban a bejelentő e-mail-címe is szerepel (Adminnak hasznos, Reporternek a saját címe).
    - Az ügyintéző- és kategóriaazonosítók nem kerültek a DTO-ba, mert ehhez a lépéshez nem kellenek.
  - **ADR nem készült.** A `FieldErrors` a meglévő hibamodell bővítése, amelyet az AGENTS.md §11 rögzít; a többi
    elem a meglévő rétegszerkezetbe illeszkedik (ADR-002).

## Ellenőrzés
- dotnet build: Build succeeded – 0 Warning(s), 0 Error(s) (`--no-incremental` újrafordítással).
- dotnet test: Passed – 362/362 zöld.
  - Projektenként: Domain.Tests 221 (+8), Application.Tests 62 (+46), Infrastructure.Tests 29 (+8),
    Web.Tests 36, ArchitectureTests 14.
  - Kiinduló állapot: 300/300.
- Közbenső hibák és javításuk:
  - Az első futáskor a `Validate_MissingImpactAndUrgency_ReturnsBothErrors` piros lett. A `NotNull` szabályhoz nem
    tartozott `WithMessage`, ezért a FluentValidation angol alapüzenetét adta. Mindkét szabály saját üzenetet kapott.
  - Egy későbbi futáskor az összes Infrastructure-teszt hibát adott, mert a LocalDB-példány nem indult el
    („SQL Server process failed to start”, az eseménynaplóban 575-ös hiba). Egy korábban indult, a kezelő által
    nem látott `sqlservr.exe` folyamat foglalta a példányt. A felhasználó jóváhagyásával készült a leállítás; a
    folyamat addigra magától kilépett, ezután a `sqllocaldb start MSSQLLocalDB` sikeres volt. Kódváltozás nem kellett.
- Javasolt kézi ellenőrzés (a felület a P-5B-ben készül, addig csak adatbázis-szinten):
  - A seed-adatokkal (a P-5B felülete után):
    - `bejelento.dora` HARDWARE-jegye Kiss Annához kerül, mert csak neki van HARDWARE-kompetenciája.
    - Egy NETWORK-jegy Kiss Annához vagy Tóth Csabához kerül: a kisebb terhelésű kapja, egyezésnél az, akinek a
      `LastAssignedAt`-je régebbi (BR-05).
    - Egy ACCESS-jegy Nagy Bélához kerül.
  - `bejelento.erik` nem nyithatja meg `dora` jegyét: `NotFound`, és a naplóban Warning a két azonosítóval.

## Ismert korlátok
- Két, egyszerre rögzített jegy ugyanazt a legkevésbé terhelt ügyintézőt kaphatja, mert a terhelés olvasása és a
  kiosztás között nincs zárolás. Az `Agent` táblán nincs konkurenciatoken; ez csak az elosztás egyenletességét
  érinti, BR-04-et nem.
- A `GetMyTickets` oldalszámának nincs felső korlátja; extrém nagy oldalszámnál (`(PageNumber - 1) * 20` túlcsordul)
  a lekérdezés kivételt dobna. A felületről ilyen érték nem érkezik.
- Az előzmények állapot-, hatás- és sürgősségértékei angol enumnevek (pl. `InProgress`); a magyar megjelenítés a
  felület (P-5B) feladata.
- A Reporter `NotFound` Warning-naplója a nem létező és az idegen jegyet egyformán rögzíti.

## Emberi javítás
Kézi forráskód-módosításra nem volt szükség. A K1–K5 tervezési pontokat és a K6 szerinti bővítést (Error.FieldErrors lista bevezetése és AGENTS.md §11 rögzítése, valamint a BR-02 szerinti Warning naplózás a GetTicketDetails query-ben) jóváhagytam. A használati korlát miatti megszakadás után a munkamenet folytatódott, a FluentValidation üzenetek finomhangolása és a LocalDB újraindítása után mind a 362 teszt hibátlanul zölden lefutott.
## Commit
feat(tickets): implement reporter use cases, ticket assignment service and queries
