# P-6A – Admin use case-ek: sor, triázs, kiosztás, státuszok
- Dátum: 2026-10-29
- Eszköz és modell: Claude Code (asztali alkalmazás), Claude Opus 5.5 (`claude-opus-5-5`)
- Cél és érintett szabályok:
  - Admin handlerek: jegysor, triázs, átkategorizálás, kézi kiosztás a jelöltlistával, automatikus kiosztás
    újrafuttatása, a négy státuszváltás; két új lekérdezés; tesztek.
  - A jóváhagyáskor kért módosítás (K4): a `TicketAssignmentService.AutoAssignAsync` visszatérése `Result<Guid?>`.
  - Üzleti szabályok: BR-03, BR-04, BR-05, BR-06, BR-07, BR-10, BR-11, BR-12, BR-13.
  - AGENTS.md: §1, §5, §7 (konkurencia), §8, §10, §11, §12, §13, §14.
- Átadott kontextus:
  - `AGENTS.md`, `CLAUDE.md`, `docs/prompts/P-5A-reporter-use-cases.md`.
  - Domain: `Ticket.cs`, `TicketErrors.cs`, `TicketHistoryEntry.cs`, a `Tickets` enumjai, `PriorityMatrix.cs`,
    `Agent.cs`, `AgentCompetency.cs`, `AgentErrors.cs`, `Assignment/*`, `Category.cs`, `CategoryErrors.cs`, `Common/*`.
  - Application: minden meglévő fájl (portok, hibák, `CurrentUserAuthorization`, `ValidatorExtensions`,
    `DependencyInjection`, `TicketAssignmentService`, a P-5A handlerei).
  - Infrastructure: `DependencyInjection.cs`, `AppDbContext.cs`, `TicketQueries.cs`, `UserQueries.cs`, a repositoryk,
    `UnitOfWork.cs`, a `Ticket` és a `TicketHistoryEntry` konfigurációja.
  - Tesztek: Application (`TestData`, `TestUsers`, `RecordingLogger`, `DependencyInjectionTests`, a P-5A
    handler-tesztjei); Domain (`TestData`, `AgentTests`, `TicketAssignmentTests`, `TicketChangeTests`,
    `TicketStateMachineTests`); Infrastructure (fixture, `IntegrationTest`, builderek, `UnitOfWorkTests`,
    `TicketQueriesTests`); ArchitectureTests (`DependencyRuleTests`).
  - `Directory.Packages.props`, `Directory.Build.props`, `.editorconfig`; `Web/Components/Pages/Admin/TicketQueue.razor`
    (csak olvasva, nem módosult).

## Prompt
[P-6A] Admin use case-ek: sor, triázs, kiosztás, státuszok
Kontextus: AGENTS.md §5, §7 (konkurencia), §8, §10 (BR-03–BR-07, BR-10–BR-12), §12. Az 5. lépés elkészült.

Feladat (minden handler csak Admin szerepkörrel fut):
1. GetTicketQueue: szűrés státusz, kategória, prioritás, felelős és „kiosztatlan” szerint; rendezés prioritás, majd létrehozás szerint; lapozás (20).
2. ChangeImpactAndUrgency (triázs): a prioritás újraszámolódik (BR-11); lezárt jegyen hiba (BR-03).
3. RecategorizeTicket: csak New, Assigned, InProgress állapotban. Ha a felelős nem jogosult az új kategóriára, újrakiosztás a TicketAssignmentService-szel; jelölt hiányában Unassign → New (BR-07).
4. AssignTicketManually(ticketId, agentId): a BR-03 és BR-04 a domainben; átosztásnál az állapot nem változik. Mellé GetAssignableAgents(ticketId) query a dialógushoz: aktív, kompetens ügyintézők a terhelésükkel.
5. RetryAutoAssign: csak felelős nélküli New jegyre (BR-06).
6. Státuszváltások: StartWork, ResolveTicket, ReopenTicket, CloseTicket a §10 állapotgépe szerint.
7. A ConcurrencyConflict, InvalidTransition és BusinessRule hibák magyar, felhasználóbarát üzenettel érkeznek a UI-ba.
8. Tesztek:
   - Minden handlerre Reporter → Forbidden.
   - Lezárt jegy nem osztható ki és nem kategorizálható át (BR-03); inaktív ügyintéző nem kap jegyet (BR-04).
   - A BR-07 mindhárom ága; a RetryAutoAssign csak New jegyre működik.
   - Integrációs teszt az Infrastructure.Tests-ben: a CloseTicket és az AssignTicketManually ütközése ConcurrencyConflict-ot vagy BR-03 hibát ad, sosem kiosztott lezárt jegyet.

Ne csináld: UI (P-6B), törzsadat-kezelés (7. lépés).

Elfogadás: a build és minden teszt zöld; a BR-03-at domain- és Application-teszt is lefedi.

Zárás: AGENTS.md §1 és §14, napló: P-6A-admin-use-cases.

Jóváhagyás a terv után (eltéréssel):
Jóváhagyom a K1–K3 és K5–K8 javaslataival, egy módosítással:
- K4: ne a felelős változatlanságából következtess. A TicketAssignmentService.AutoAssignAsync visszatérése legyen Result<Guid?> (a kiválasztott ügyintéző azonosítója, null = nincs jogosult jelölt, BR-06). A RecategorizeTicket ennek alapján hívja az Unassign-t; a CreateTicket és a RetryAutoAssign is ezt használja. A meglévő tesztek ehhez igazodjanak, és legyen teszt arra, hogy jelölt hiányában az eredmény null.
Mehet a megvalósítás.

## Eredmény
- Létrehozott és módosított fájlok:
  - `src/ServiceDesk.Domain`:
    - `Agents/Agent.cs`: `IsEligibleFor(Guid categoryId)` = `IsActive && HasCompetency` (BR-04).
    - `Agents/AgentErrors.cs`: `NotFound` („Az ügyintéző nem található.”).
    - `Tickets/TicketErrors.cs`: `AutoAssignmentNotAllowed` (BusinessRule, „Automatikus kiosztás csak felelős nélküli,
      új jegyre futtatható.”).
  - `src/ServiceDesk.Application`:
    - `Tickets/TicketAssignmentService.cs`: `AutoAssignAsync` → `Result<Guid?>` (K4).
    - `Tickets/CreateTicket/CreateTicketHandler.cs`: a naplóba az `AutoAssignAsync` eredménye kerül.
    - `Common/Persistence/ITicketQueries.cs`: `GetTicketQueueAsync`, `GetAssignableAgentsAsync`.
    - új feature-mappák:
      - `Tickets/GetTicketQueue/`: `GetTicketQueueQuery` (`PageSize = 20`), `GetTicketQueueHandler`,
        `GetTicketQueueValidator`, `TicketQueueItemDto`;
      - `Tickets/ChangeImpactAndUrgency/`: `ChangeImpactAndUrgencyCommand`, `…Handler`, `…Validator`;
      - `Tickets/RecategorizeTicket/`: `RecategorizeTicketCommand`, `…Handler`, `…Validator`;
      - `Tickets/AssignTicketManually/`: `AssignTicketManuallyCommand`, `…Handler`, `…Validator`;
      - `Tickets/GetAssignableAgents/`: `GetAssignableAgentsQuery`, `…Handler`, `AssignableAgentsDto`,
        `AssignableAgentDto`;
      - `Tickets/RetryAutoAssign/`: `RetryAutoAssignCommand`, `RetryAutoAssignHandler`;
      - `Tickets/StartWork/`, `Tickets/ResolveTicket/`, `Tickets/ReopenTicket/`, `Tickets/CloseTicket/`:
        command és handler mindegyikhez.
  - `src/ServiceDesk.Infrastructure/Persistence/Queries/TicketQueries.cs`: a két új lekérdezés.
  - Tesztek:
    - `tests/ServiceDesk.Domain.Tests/Agents/AgentTests.cs` (+3): `IsEligibleFor`.
    - `tests/ServiceDesk.Application.Tests`:
      - `TestData.cs`: `CreateTicketInStatus` és `ArrangingAdminUserId` (a Domain.Tests mintájára);
      - `Tickets/TicketAssignmentServiceTests.cs`: a kiválasztott ügyintéző azonosítója, illetve jelölt hiányában
        `null` (K4);
      - új tesztek: `Tickets/TicketStatusHandlersTests.cs` (29), `GetTicketQueue/GetTicketQueueHandlerTests.cs` (6),
        `ChangeImpactAndUrgency/ChangeImpactAndUrgencyHandlerTests.cs` (7),
        `RecategorizeTicket/RecategorizeTicketHandlerTests.cs` (19),
        `AssignTicketManually/AssignTicketManuallyHandlerTests.cs` (14),
        `GetAssignableAgents/GetAssignableAgentsHandlerTests.cs` (7), `RetryAutoAssign/RetryAutoAssignHandlerTests.cs` (10).
    - `tests/ServiceDesk.Infrastructure.Tests`:
      - `ServiceDesk.Infrastructure.Tests.csproj`: `Microsoft.Extensions.TimeProvider.Testing` (a §3 listáján van;
        a verziót a `Directory.Packages.props` már tartalmazta);
      - `Persistence/TicketQueriesTests.cs` (+12): a jegysor sorrendje, szűrői és lapozása; a kiosztható ügyintézők;
      - új `UseCases/CloseAndAssignConflictTests.cs` (3), segédek: `UseCases/TestCurrentUser.cs`,
        `UseCases/BeforeSaveUnitOfWork.cs`.
  - Ez a napló. Migráció nem kellett, mert a modell nem változott. ADR nem készült (lásd lent).
- Döntések és indoklásuk:
  - **A felhasználó döntései (a terv jóváhagyásakor):**
    - **K1 – inaktív felelős átkategorizáláskor:** a BR-07 „jogosult” szava a BR-04 szerinti jogosultság (aktív és
      kompetens). Ezért az inaktív, de kompetens felelős átkategorizáláskor elveszti a jegyet, ha van más jelölt.
      Ha nincs, a jegy `New` lesz. A BR-12 csak azt mondja ki, hogy az inaktiválás maga nem mozgatja a jegyet.
    - **K2 – ugyanaz a kategória:** a domain hibái (lezárt vagy megoldott jegy, inaktív kategória) előbb jönnek.
      Egyébként siker, kiosztás és mentés nélkül.
    - **K3 – felelős nélküli `New` jegy átkategorizálása:** automatikus kiosztás (BR-05); jelölt hiányában `New`
      marad (BR-06).
    - **K4 – `AutoAssignAsync` → `Result<Guid?>` (módosítással):**
      - Az érték a kiválasztott ügyintéző azonosítója; `null`, ha nincs jogosult jelölt (BR-06). Ekkor a jegy
        változatlan.
      - A domain hibája (BR-03, BR-04) változatlanul visszajut.
      - A `RecategorizeTicket` a `null` értéknél hívja az `Unassign`-t, de csak akkor, ha a jegynek van felelőse.
        Így a `New` jegy `New` marad.
      - A `RetryAutoAssign` az értéket adja vissza; a `CreateTicket` a naplóba írja.
      - Az `Unassign` nem futhat a kiosztás előtt, mert az `InProgress` állapotot `New`-ra állítaná, átosztáskor
        viszont az állapot nem változik.
    - **K5 – `RetryAutoAssign`:** a „csak `New`” ellenőrzés a handlerben van (`// BR-06`). A domainben a `New`
      jegynek sosem lehet felelőse, így ez egyben a „felelős nélküli” feltétel. Lezárt jegyre is
      `AutoAssignmentNotAllowed` a válasz. Ha nincs jelölt, a válasz siker `null` értékkel, mentés nélkül.
    - **K6 – jegysor:**
      - Rendezés: prioritás (Critical → Planning), azon belül a legrégebbi létrehozás elöl, végül `Id`.
      - A prioritás névként van tárolva, ezért a sorrend egy `CASE`-re fordított feltételes kifejezés.
      - Szűrők: egyértékűek (`Status`, `CategoryId`, `Priority`, `AssigneeAgentId`, `UnassignedOnly`). A felelős és
        a kiosztatlan szűrő együtt `Validation.AssigneeAgentId`.
      - Szűrő nélkül minden jegy megjelenik, a lezártak is; az alapnézetet a P-6B állítja be.
      - A DTO tartalmazza a kategória nevét, a felelős nevét és a bejelentő e-mail-címét. A nevek ugyanannak az
        SQL-utasításnak az allekérdezései (nincs N+1).
    - **K7 – `GetAssignableAgents`:**
      - Lezárt jegyre `ClosedCannotBeAssigned` (BR-03).
      - A lista a jegy kategóriájára aktív és kompetens ügyintézőket tartalmazza: `Load`, nyitott jegyek száma,
        `IsCurrentAssignee`.
      - A `Load` az `AgentCandidate.Load`-dal számol, így ugyanaz a terhelés-definíció, mint az automatikus
        kiosztásé (BR-05).
      - Rendezés: terhelés szerint növekvő, egyezésnél név szerint (stabil rendezés a névsorrendű lekérdezés után).
      - Két lekérdezés: egy a jegyre, egy az ügyintézőkre a nyitott jegyeik prioritásával.
      - A port az `ITicketQueries` része, mert a §5 portlistája rögzített.
    - **K8 – ütközési teszt:**
      - Determinisztikus: a `BeforeSaveUnitOfWork` dekorátor a mentés előtt lefuttatja a versengő handlert egy
        külön scope-ban. Addigra a dekorált handler már betöltötte és módosította a jegyet.
      - Három eset:
        - a lezárás ment előbb → az átosztás `ConcurrencyConflict`-ot kap; a jegy lezárt, a felelős változatlan;
        - az átosztás ment előbb → a lezárás `ConcurrencyConflict`-ot kap; a jegy `Resolved`, az új felelőssel;
        - egymás után (lezárás, majd kiosztás) → `ClosedCannotBeAssigned`.
      - A lezárt esetekben a lezárás az utolsó előzménysor.
      - A valódi handlerek, repositoryk és `UnitOfWork` futnak LocalDB-n.
  - **Handler-sorrend (§5):** minden handler első lépése `RequireRole(Roles.Admin)`, ezt követi a validáció (ahol
    van), majd a betöltés, a domain-művelet, egyetlen `SaveChangesAsync` és az eredmény.
    - Validátor: csak ott készült, ahol a commandnak a jegyazonosítón kívül más mezője is van (`GetTicketQueue`,
      `ChangeImpactAndUrgency`, `RecategorizeTicket`, `AssignTicketManually`).
    - A csak `TicketId`-t tartalmazó commandok üres `Guid`-ja egyszerűen `NotFound`, ahogy a `GetTicketDetails`-ben.
  - **Visszatérési típusok:**
    - `Result`: a triázs, az átkategorizálás, a kézi kiosztás és a státuszváltások. A felület a sikeres művelet után
      újratölti a részleteket (`GetTicketDetails`).
    - `Result<Guid?>`: `RetryAutoAssign`.
    - DTO: a két lekérdezés.
  - **A 7. pont (magyar hibaüzenetek):**
    - A handlerek a domain, a validátor és a `UnitOfWork` hibáit változatlanul adják tovább.
    - A `ConcurrencyConflict`, az `InvalidTransition`, a `ClosedCannotBeAssigned`, a `ClosedCannotBeModified` és az
      új hibák mind magyar, felhasználónak szóló szövegek.
    - Több teszt a `Message` szövegét is ellenőrzi.
  - **Átkategorizálás (BR-07):**
    - A jelenlegi felelőst az `Agent.IsEligibleFor` ítéli meg. Jogosult felelősnél a jelöltlista le sem kérdeződik.
    - A kiosztás és az előzmények egyetlen mentésben kerülnek az adatbázisba.
    - Nem betölthető felelős programozási hiba (ügyintéző nem törölhető), ezért kivétel, ahogy a
      `TicketAssignmentService`-ben.
  - **Naplózás (§11):** minden sikeres művelet Information szintű, `[LoggerMessage]`-dzsel írt bejegyzést ad, csak
    azonosítókkal (jegy, ügyintéző, kategória, végrehajtó), illetve a triázsnál az új prioritással.
  - **ADR nem készült.** Új architekturális döntés nem született; a handlerek, a port és a lekérdezések a meglévő
    szerkezetbe illeszkednek (ADR-002).

## Ellenőrzés
- dotnet build: Build succeeded – 0 Warning(s), 0 Error(s) (`--no-incremental` újrafordítással).
- dotnet test: Passed – 514/514 zöld.
  - Projektenként: Domain.Tests 224 (+3), Application.Tests 154 (+92), Infrastructure.Tests 45 (+15),
    Web.Tests 77, ArchitectureTests 14.
  - Kiinduló állapot: 404/404.
  - `dotnet test -- --filter-trait "BR=BR-03"`: 59 teszt a Domain-, az Application- és az Infrastructure-rétegben.
- Javasolt kézi ellenőrzés (a felület a P-6B-ben készül, addig csak adatbázis-szinten):
  - A seed-adatokkal egy `bejelento.dora` által rögzített HARDWARE-jegy Kiss Annához kerül.
  - Ha a jegyet NETWORK-re kategorizálják át, Anna megtartja (HARDWARE- és NETWORK-kompetenciája is van).
  - ACCESS-re átkategorizálva Nagy Bélához kerül (BR-07). Ha Béla inaktív, a jegy `New` lesz, felelős nélkül;
    ekkor a `RetryAutoAssign` `null` értéket ad.

## Ismert korlátok
- Az `Agent` táblán nincs konkurenciatoken. Ha egy ügyintézőt egy kézi kiosztás betöltése és mentése között
  inaktiválnak, a jegy még megkaphatja. A jegy oldali ütközést a `RowVersion` és az előzmények egyedi indexe kezeli.
- Két egyszerre futó automatikus kiosztás (létrehozás, átkategorizálás, újrafuttatás) ugyanazt a legkevésbé terhelt
  ügyintézőt választhatja (ahogy a P-5A-ban); ez csak az elosztás egyenletességét érinti, BR-04-et nem.
- A jegysor szűrői egyértékűek. Szűrő nélkül a lezárt jegyek is megjelennek; az alapértelmezést a P-6B állítja be.
- A `GetTicketQueue` oldalszámának nincs felső korlátja (ahogy a `GetMyTickets`-nél).
- A kiosztható ügyintézők névsorrendje az adatbázis alapértelmezett rendezését követi, nem a `hu-HU` kultúrát.
- A lezárt jegyre kért státuszváltás a közös `InvalidTransition` üzenetet kapja, nem lezárásra utaló szöveget.

## Emberi javítás
Kézi forráskód-módosításra nem volt szükség. A K1–K8 tervezési döntéseket és a K4 szerinti módosítást (az AutoAssignAsync visszatérése Result<Guid?>, így jelölt hiányában tiszta null értéket ad a BR-06 és BR-07 ágakban) jóváhagytam. Mind az 514/514 teszt zöld.
## Commit
feat(admin): implement ticket queue, triage, assignment and lifecycle use cases
