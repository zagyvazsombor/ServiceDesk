# P-6B – Admin felületek: jegysor és jegykezelés
- Dátum: 2026-10-29
- Eszköz és modell: Claude Code (asztali alkalmazás), Claude Opus 5.5 (`claude-opus-5-5`)
- Cél és érintett szabályok:
  - `/admin/queue` jegysor; Admin-panel a `/tickets/{id}` oldalon (triázs, átkategorizálás, kézi kiosztás, státuszgombok,
    automatikus kiosztás újra); konkurenciaütközés kezelése; bUnit-tesztek.
  - A jóváhagyáskor kért döntések: K1 (a), K2 (a), K3 (b); az AGENTS.md tesztparancsainak frissítése.
  - Üzleti szabályok: BR-03, BR-04, BR-06, BR-07, BR-11, BR-12, BR-13.
  - AGENTS.md: §1, §5 (Web), §6, §7 (konkurencia), §8, §10, §11, §12, §13, §14, §16.
- Átadott kontextus: `AGENTS.md`, `CLAUDE.md`, `docs/prompts/P-6A-admin-use-cases.md`, `P-5B-reporter-ui.md`; a P-6A
  handlerei, commandjai és DTO-i; `Ticket.cs`, `TicketErrors.cs`, `PriorityMatrix.cs`; `TicketQueries.cs`; a Web oldalai,
  `TicketLabels`, chipek, `MainLayout`, `NavMenu`; a Web.Tests, Application.Tests és Infrastructure.Tests érintett tesztjei;
  a MudBlazor 9.11 és bUnit 2.13 XML-dokumentációja (API-ellenőrzés).

## Prompt
[P-6B] Admin felületek: jegysor és jegykezelés
Kontextus: AGENTS.md §5 (Web), §6, §11. A P-6A elkészült.

Feladat:
1. /admin/queue: MudDataGrid szerveroldali lapozással, a P-6A szűrőivel; „Csak kiosztatlan” kapcsoló; prioritás és státusz színkódolt MudChip-pel.
2. /tickets/{id:guid} Admin-panel (csak Adminnak, <AuthorizeView>):
   - Triázs: hatás és sürgősség módosítása, a prioritás előnézetével.
   - Átkategorizálás és kézi kiosztás MudDialog-ban; a listában a GetAssignableAgents jelöltjei, a végső ellenőrzés a use case-é.
   - Státuszgombok csak a megengedett átmenetekhez; lezárás megerősítő dialógussal.
   - Kiosztatlan New jegynél „Automatikus kiosztás újra” gomb.
3. Konkurenciaütközésnél Snackbar az üzenettel és „Frissítés” művelettel, amely újratölti a jegyet.
4. Web.Tests (bUnit): lezárt jegynél nem jelennek meg a kiosztó és státuszgombok; ütközéses Result esetén Snackbar jelenik meg.

Elfogadás: a build és a tesztek zöldek; a gombok elrejtése mellett a use case-ek önállóan is elutasítják a tiltott műveletet.

Zárás: AGENTS.md §1 és §14, napló: P-6B-admin-ui.

Jóváhagyás a terv után (eltéréssel):
Jóváhagyom, a következő döntésekkel:
- K1: (a) – GetTicketQueueFilters, csak Adminnak, az inaktív kategóriák és ügyintézők „(inaktív)” jelöléssel.
- K2: (a) – Domain/Tickets/TicketWorkflow; a Ticket metódusai és a RetryAutoAssignHandler is ezt hívják, a Web csak olvassa. A meglévő állapotgép-tesztek változatlanul zöldek maradjanak.
- K3: (b) – IncludeClosed mező (alapértéke false) a GetTicketQueueQuery-ben és „Lezártak is” kapcsoló a felületen; teszt arra, hogy alapértelmezésben lezárt jegy nem jelenik meg.
- A többi döntést elfogadom.
- A paraméter nélküli dotnet test viselkedése miatt az AGENTS.md §1 (5. pont) és §16 parancsait frissítsd a működő dotnet test --solution ServiceDesk.slnx alakra, és rögzítsd a naplóban.
Mehet a megvalósítás.

## Eredmény
- Létrehozott és módosított fájlok:
  - Domain:
    - új `Tickets/TicketWorkflow.cs`: `IsTerminal`, `CanStart`, `CanResolve`, `CanReopen`, `CanClose`, `CanUnassign`,
      `CanAssign`, `CanChangeImpactAndUrgency`, `CanRecategorize`, `CanRetryAutoAssign`;
    - `Tickets/Ticket.cs`: az őrfeltételek ezeket hívják (a hibakódok és a sorrend változatlan).
  - Application:
    - `Tickets/RetryAutoAssign/RetryAutoAssignHandler.cs`: `TicketWorkflow.CanRetryAutoAssign`;
    - `Tickets/GetTicketQueue/GetTicketQueueQuery.cs`: `IncludeClosed = false`; `GetTicketQueueValidator.cs`: `Closed`
      státuszszűrő csak `IncludeClosed`-dal (`Validation.Status`);
    - új `Tickets/GetTicketQueueFilters/` (query, handler, `TicketQueueFiltersDto`, `QueueFilterOptionDto`);
      `Common/Persistence/ITicketQueries.cs`: `GetTicketQueueFiltersAsync`.
  - Infrastructure: `Persistence/Queries/TicketQueries.cs`: a lezártak kizárása alapértelmezésben; a szűrőlisták két
    lekérdezéssel (`AsNoTracking`, név szerint, az inaktívakkal).
  - Web:
    - `Components/Pages/Admin/TicketQueue.razor`: szűrők (státusz, kategória, prioritás, felelős; „Csak kiosztatlan”,
      „Lezártak is”), `MudDataGrid` szerveroldali lapozással, chipekkel, sorra kattintással;
    - `Components/Pages/Tickets/TicketDetails.razor`: `LoadAsync` kiemelve; `<AuthorizeView Roles="Admin">` +
      `TicketAdminPanel`;
    - új `Components/Tickets/TicketAdminPanel.razor`, `AssignTicketDialog.razor`, `RecategorizeTicketDialog.razor`;
    - `Components/Tickets/TicketLabels.cs`: `NameWithActiveState` („(inaktív)”).
  - `AGENTS.md` §1 (5. pont) és §16: `dotnet test --solution ServiceDesk.slnx` (a trait-szűrővel együtt).
  - Tesztek:
    - Domain.Tests: új `Tickets/TicketWorkflowTests.cs` (10 eset);
    - Application.Tests: `GetTicketQueueHandlerTests` (+2), új `GetTicketQueueFilters/GetTicketQueueFiltersHandlerTests` (3);
    - Infrastructure.Tests: `TicketQueriesTests` (+4): alapértelmezésben nincs lezárt jegy, `IncludeClosed`, `Closed`
      státusz, szűrőlisták; a meglévő kategória-/felelős-szűrő esetek `IncludeClosed: true`-val őrzik a szándékukat;
    - Web.Tests: új `Pages/Tickets/TicketDetailsAdminTests` (17), `Pages/Admin/TicketQueueTests` (8),
      `Components/Tickets/AssignTicketDialogTests` (3), `RecategorizeTicketDialogTests` (3), `TicketLabelsTests` (+2).
  - Migráció nem kellett (a modell nem változott). ADR nem készült (nincs architekturális döntés).
- Döntések és indoklásuk:
  - **K1–K3** a jóváhagyás szerint. A `Closed` státusz csak bekapcsolt „Lezártak is” mellett választható; a kapcsoló
    kikapcsolása törli a `Closed` szűrőt. A „Csak kiosztatlan” törli és letiltja a felelős-szűrőt (a validátor a kettőt
    együtt nem engedi).
  - **Szűrőváltás:** a grid `@key`-t kap, így új példányként az első oldalról tölt (bUnit-teszt ellenőrzi).
  - **Dialógusok:** a panel előbb lekéri a jelölteket (`GetAssignableAgents`) / aktív kategóriákat, hibánál Snackbar és
    nincs dialógus; a dialógus csak a választást adja vissza, a commandot a panel futtatja. A jelenlegi felelős látszik,
    de nem választható; üres listánál figyelmeztetés (BR-06).
  - **Ütközés:** `GeneralErrors.ConcurrencyConflict` → Warning Snackbar „Frissítés” akcióval (`RequireInteraction`,
    `CloseAfterNavigation`), amely a `OnChanged`-en át újratölti a jegyet. Más hiba: Error Snackbar; triázs
    validációs hibája a mezőknél. Siker: Success Snackbar + újratöltés.
  - **Elfogadás 2. fele:** a use case-ek önálló elutasítását a P-6A tesztjei fedik; a Web oldalon teszt van arra, hogy
    az elutasító `Result` (pl. `InvalidTransition`) Snackbart ad.
  - **`dotnet test`:** a paraméter nélküli futás minden tesztprojektre „Zero tests ran”-t (kilépési kód 5) adott,
    projektenként és `--solution`-nel a tesztek futnak; a felhasználó döntése szerint az AGENTS.md a `--solution` alakot írja elő.

## Ellenőrzés
- dotnet build: Build succeeded – 0 Warning(s), 0 Error(s) (`--no-incremental`).
- dotnet test (`--solution ServiceDesk.slnx`): Passed – 566/566 zöld (kiinduló állapot: 514/514).
  - Domain.Tests 234 (+10), Application.Tests 159 (+5), Infrastructure.Tests 49 (+4), Web.Tests 110 (+33), ArchitectureTests 14.
  - `--filter "BR=BR-03"`: 82 teszt.
- Kézi ellenőrzés a futó alkalmazásban (Development, `http://localhost:5123`, `admin.anna`):
  - Jegysor: a 3 meglévő jegy chipekkel, magyar feliratokkal, budapesti idővel.
  - „Nem indul a laptop” (Kiosztva): csak Besorolás mentése, Munka megkezdése, Átosztás, Átkategorizálás látszott.
  - Triázs: Hatás → Magas; az előnézet „Magas”, mentés után Snackbar, a jegy prioritása Magas.
  - Átosztás dialógus: HARDWARE-re csak Kiss Anna jogosult, „terhelés: 7, nyitott jegy: 2 (jelenlegi felelős)”, letiltva.
  - Átkategorizálás Egyéb kategóriára: a jegy Tóth Csabához került, állapota Kiosztva maradt (BR-07); az előzmények
    rögzítették az átkategorizálást és az átosztást.
  - Munka megkezdése → Megoldás: Megoldva állapotban Újranyitás, Lezárás, Átosztás látszott.
  - Lezárás: a megerősítés „Mégse” után nem történt semmi; „Lezárás” után Snackbar, és csak a „A jegy lezárva…” sáv maradt.
- Javasolt kézi ellenőrzés: a „Lezártak is” és „Csak kiosztatlan” kapcsoló a jegysoron; „Automatikus kiosztás újra”
  egy felelős nélküli új jegyen. Az ütközés kézzel nehezen idézhető elő (minden use case frissen tölt), ezt bUnit-teszt fedi.

## Ismert korlátok
- A kézi ellenőrzés módosította a fejlesztői adatbázist: a „Nem indul a laptop” jegy lezárt, Egyéb kategóriájú, Tóth Csabáé.
- Elavult oldalon kattintva a use case többnyire `InvalidTransition`-t ad (nem ütközést); ilyenkor csak Error Snackbar
  jelenik meg „Frissítés” akció nélkül.
- A jegysor szűrői nem kerülnek az URL-be. Az AGENTS.md §15 és a CLAUDE.md még a rövid `dotnet test` nevet használja.
- Az Átosztás dialógus megerősítő gombja „Kiosztás” feliratú.

## Emberi javítás
Kézi kódmódosításra nem volt szükség. A build figyelmeztetés nélkül lefutott, mind az 566 teszt zöld.
## Commit
feat(admin): implement admin ticket queue and management ui with mudblazor
