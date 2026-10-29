# P-5B – Bejelentői felületek MudBlazorral
- Dátum: 2026-10-29
- Eszköz és modell: Claude Code (asztali alkalmazás), Claude Opus 5.5 (`claude-opus-5-5`)
- Cél és érintett szabályok:
  - `/tickets/new`, `/tickets/my`, `/tickets/{id:guid}` oldal MudBlazorral, `IUseCaseRunner`-en át; bUnit-tesztek.
  - A jóváhagyáskor kért bővítések:
    - felelős a saját jegyek listájában (K1);
    - budapesti idő a dátumokban;
    - betöltésenkénti `CancellationTokenSource` a részletek oldalon;
    - rögzített prioritás-chip színek;
    - kézi ellenőrzés a futó alkalmazásban.
  - Üzleti szabályok: BR-01, BR-02, BR-05, BR-06, BR-10, BR-11, BR-13.
  - AGENTS.md: §1, §4, §5 (Web), §6, §7, §8, §11, §12, §13, §14.
- Átadott kontextus:
  - `AGENTS.md`, `CLAUDE.md`, `docs/prompts/P-5A-reporter-use-cases.md`.
  - Web: `Program.cs`, `App.razor`, `Routes.razor`, `_Imports.razor`, `Layout/*` (`MainLayout`, `NavMenu`, `UserMenu`),
    `UseCases/*`, a meglévő oldalvázak (`NewTicket`, `MyTickets`, `TicketDetails`, `Admin/TicketQueue`, `Home`,
    `NotFound`), `appsettings.Development.json` (seed-jelszó a kézi ellenőrzéshez), `launchSettings.json`.
  - Application: `Tickets/*` (commandok, query-k, DTO-k, handlerek, validátor), `Common/Paging/PagedResult.cs`,
    `Common/Validation/ValidatorExtensions.cs`, `Common/Persistence/ITicketQueries.cs`.
  - Domain: `Common/*` (`Error`, `ErrorType`, `Result`), `Tickets/*` (`PriorityMatrix`, enumok, `Ticket`,
    `TicketErrors`, `TicketHistoryEntry`), `Categories/CategoryErrors.cs`.
  - Infrastructure: `Persistence/Queries/TicketQueries.cs`.
  - Tesztek:
    - Web.Tests: `UserMenuTests`, `MainLayoutTests`, `PageAuthorizationTests`, `TestUsers`, csproj;
    - Infrastructure.Tests: `TicketQueriesTests`, `TicketBuilder`, `AgentBuilder`;
    - Application.Tests: `GetMyTicketsHandlerTests`;
    - ArchitectureTests: `DependencyRuleTests`.
  - `Directory.Packages.props`, `Directory.Build.props`, `.editorconfig`.
  - A MudBlazor 9.11 és a bUnit 2.13 API-ját reflexióval ellenőriztem: `MudDataGrid.ServerData` aláírása,
    `MudSelect.SelectOption`, `cut.Render(...)`, `DisposeComponentsAsync`.

## Prompt
[P-5B] Bejelentői felületek MudBlazorral
Kontextus: AGENTS.md §5 (Web), §6, §11. A P-5A elkészült.

Feladat:
1. /tickets/new (Reporter): MudForm – cím, leírás (többsoros), hatás, sürgősség, kategória (csak aktív, MudSelect). A várható prioritást tájékoztatásként a domain PriorityMatrix-ával mutasd (saját képlet nem lehet). Siker után Snackbar és navigálás a jegy részleteire; a validációs hibák a mezőknél jelennek meg.
2. /tickets/my (Reporter): MudDataGrid – cím, státusz (színes MudChip, magyar felirattal), prioritás, kategória, felelős, létrehozás ideje; sorra kattintva részletek; lapozás.
3. /tickets/{id:guid} (Reporter és Admin): fejlécadatok, a leírás sima szövegként, MudTimeline az előzményekkel. NotFound eredménynél barátságos „A jegy nem található” állapot.
4. Minden use case-hívás az IUseCaseRunner-en át; a komponens IDisposable, a CancellationTokenSource-ot Dispose-ban megszakítja.
5. Web.Tests (bUnit): az új jegy űrlapja üres mezőkkel nem küld; sikeres Result után navigál; hibás Result esetén Snackbar-üzenetet mutat.

Elfogadás: a build és a tesztek zöldek; komponensben nincs üzleti logika és nincs Infrastructure-hivatkozás.

Zárás: AGENTS.md §1 és §14, napló: P-5B-reporter-ui.

Jóváhagyás a terv után (kiegészítésekkel):
Jóváhagyom a K1–K4 javaslataival, a következő kiegészítésekkel:
- Időzóna: a TicketLabels dátumformázása a tárolt UTC időt explicit módon budapesti időre váltja (TimeZoneInfo, "Europe/Budapest"), hu-HU formátummal. Teszt: egy ismert UTC-időpont a várt budapesti időként jelenik meg, nyári és téli időben is.
- TicketDetails: minden új betöltés megszakítja az előző, még futó betöltést (betöltésenként új CancellationTokenSource), hogy egy elavult válasz ne írhassa felül az újabbat.
- A prioritás-chipek színe is legyen rögzítve a TicketLabels-ben (pl. Kritikus: Error, Magas: Warning, Közepes: Info, Alacsony és Tervezhető: Default).
- A build és a tesztek után futtasd le a prompt-terv 5. lépésének kézi ellenőrzését a futó alkalmazásban (Dóra HARDWARE jegye Kiss Annához kerül; két azonos besorolású NETWORK jegy közül az első Tóth Csabához, a második Kiss Annához; Erik nem nyithatja meg Dóra jegyét), és az eredményt rögzítsd a naplóban.
Mehet a megvalósítás.

## Eredmény
- Létrehozott és módosított fájlok:
  - `src/ServiceDesk.Application/Tickets/GetMyTickets/MyTicketDto.cs`: új `string? AssigneeName` mező a
    `CategoryName` után (K1).
  - `src/ServiceDesk.Infrastructure/Persistence/Queries/TicketQueries.cs`: a `GetMyTicketsAsync` projekciója
    al-lekérdezéssel olvassa az ügyintéző nevét. Ugyanez a minta a `GetTicketDetailsAsync`-ben; egy SQL-utasítás,
    nincs N+1.
  - `src/ServiceDesk.Web`:
    - új `Components/Tickets/TicketLabels.cs`:
      - magyar feliratok (státusz, prioritás, hatás, sürgősség, előzményművelet);
      - chip-színek;
      - az előzményértékek fordítása;
      - budapesti idejű `hu-HU` dátumformázás.
    - új `Components/Tickets/TicketStatusChip.razor` és `TicketPriorityChip.razor`.
    - `Components/Pages/Tickets/NewTicket.razor`: `MudForm` a kategóriák betöltésével, kötelező mezőkkel,
      várható prioritással, Snackbarral, navigálással és mezőhibákkal.
    - `Components/Pages/Tickets/MyTickets.razor`: `MudDataGrid` szerveroldali lapozással, chipekkel, felelőssel,
      sorra kattintással és üres állapottal.
    - `Components/Pages/Tickets/TicketDetails.razor`: fejlécadatok, sima szöveges leírás, `MudTimeline`,
      NotFound-állapot, betöltésenkénti CTS. Az elavult „későbbi lépés” komment frissült.
  - Tesztek:
    - `tests/ServiceDesk.Web.Tests`:
      - `Pages/Tickets/NewTicketTests.cs` (5);
      - `Pages/Tickets/MyTicketsTests.cs` (3);
      - `Pages/Tickets/TicketDetailsTests.cs` (6);
      - `Components/Tickets/TicketLabelsTests.cs` (27 eset).
    - `tests/ServiceDesk.Infrastructure.Tests/Persistence/TicketQueriesTests.cs`: a várt DTO frissült, és van egy új
      teszt a kiosztott és kiosztatlan jegy felelősnevére (+1).
    - `tests/ServiceDesk.Application.Tests/Tickets/GetMyTickets/GetMyTicketsHandlerTests.cs`: a DTO-konstruktor
      hívása frissült.
  - `.claude/launch.json`: a kézi ellenőrzéshez a beépített böngésző előnézeti szerverét indítja
    (`dotnet run --project src/ServiceDesk.Web --launch-profile http`, 5123-as port). A fájl új és nem követett; a
    hallgató dönti el, hogy commitolja vagy kihagyja.
  - Ez a napló. Migráció nem kellett, mert csak a projekció változott, a modell nem. ADR nem készült (lásd lent).
- Döntések és indoklásuk:
  - **A felhasználó döntései (a terv jóváhagyásakor):**
    - **K1 – felelős a listában:** `MyTicketDto.AssigneeName`, al-lekérdezéssel. Kiosztatlan jegynél a felület
      „Nincs kiosztva” feliratot mutat (`TicketLabels.NoAssignee`), a listában és a részleteknél egyaránt.
    - **K2 – feliratok:**
      - Státusz: Új, Kiosztva, Folyamatban, Megoldva, Lezárva.
      - Prioritás: Kritikus, Magas, Közepes, Alacsony, Tervezhető.
      - Hatás és sürgősség: Magas, Közepes, Alacsony.
      - Előzményműveletek: Jegy létrehozva, Kiosztás, Átosztás, Felelős eltávolítva, Státuszváltás, Átkategorizálás,
        Hatás módosítása, Sürgősség módosítása.
      - Státuszszínek: Info, Primary, Warning, Success, Default.
    - **Prioritásszínek (kiegészítés):** Kritikus → Error, Magas → Warning, Közepes → Info, Alacsony és
      Tervezhető → Default. A `TicketLabels.PriorityColor` rögzíti, teszt ellenőrzi.
    - **K3 – validációs hiba:** a `FieldErrors` a mezők `Error`/`ErrorText` paraméterébe kerül, Snackbar nélkül
      (§11). Snackbar csak akkor jelenik meg, ha nincs mezőhiba (nem validációs hiba), vagy ha van az űrlapon nem
      szereplő mező. A mező hibája szerkesztéskor (`@bind-Value:after`) és új beküldéskor törlődik.
    - **K4 – kiegészítő tesztek:** a részletek, a lista és a feliratok tesztjei is elkészültek.
    - **Időzóna (kiegészítés):**
      - A `TicketLabels.FormatDateTime` `TimeZoneInfo.FindSystemTimeZoneById("Europe/Budapest")`-tal vált, és
        `hu-HU` `"g"` mintával formáz (pl. `2026. 07. 15. 12:30`).
      - A rövid időminta `H:mm`, ezért egyjegyű órák előtt nincs vezető nulla (`1:59`).
      - A teszt nyári és téli időt, valamint a 2026. márciusi átállás két oldalát is lefedi.
    - **Betöltésenkénti CTS (kiegészítés):**
      - A `TicketDetails.OnParametersSetAsync` minden futáskor megszakítja az előző betöltést, és új
        `CancellationTokenSource`-ot hoz létre.
      - A válasz után a betöltés ellenőrzi a saját tokenjét, így az elavult válasz eldobódik.
      - A forrás a betöltés végén `using`-gal felszabadul; a `_loadCts` mező csak a futó betöltést tartja, így sem
        egy újabb betöltés, sem a `Dispose` nem hív `Cancel`-t felszabadított forráson.
      - Ha a megszakított hívás `OperationCanceledException`-t dob, a Blazor a megszakított életciklus-taskot
        csendben elengedi, ezért az `ErrorBoundary` nem jelenik meg.
  - **Rétegek:**
    - A komponensek csak megjelenítenek, inputot gyűjtenek és a `Result`-ot kezelik.
    - A várható prioritás a `PriorityMatrix.Calculate` eredménye; a Pages névtér a Domaintől függhet (§4).
    - A kliensoldali `Required` csak kényelmi ellenőrzés: a hossz- és enumszabályokat a szerveroldali validátor
      ellenőrzi, a hibák a mezőknél jelennek meg.
    - Infrastructure- és EF-hivatkozás nincs; az architektúra-tesztek zöldek.
  - **A közös elemek helye:** `Components/Tickets/` (`ServiceDesk.Web.Components.Tickets`). Ezek nem oldalak, és a
    lista meg a részletek oldal is használja őket. A Web általános „nincs Infrastructure” architektúra-szabálya
    rájuk is vonatkozik.
  - **`MyTickets`:**
    - A `ServerData` a grid 0-tól számozott oldalát 1-től számozottra váltja.
    - Oldalméret: `GetMyTicketsQuery.PageSize`.
    - Rendezés és szűrés nincs, mert a sorrendet (legújabb elöl) a lekérdezés adja.
    - A grid saját tokenje és a komponens `_cts`-e össze van kapcsolva.
    - A lapozó szövege magyar (`1–3, összesen 3`).
  - **`TicketDetails`:**
    - A leírás `white-space: pre-wrap` mellett, Razor-kódolással, `MarkupString` nélkül jelenik meg. Teszt
      ellenőrzi, hogy a `<b>` és a `<script>` szövegként látszik.
    - Az előzmények időrendben jelennek meg; a szereplő `null` értéke „Rendszer”.
    - A NotFound-állapot szerepkör szerinti visszalépő gombot ad (`AuthorizeView`).
    - Más hibánál a felület Snackbart mutat.
  - **Tesztmegoldások:**
    - Az `ISnackbar` NSubstitute-helyettesítő, így az üzenet és a súlyosság pontosan ellenőrizhető.
    - A `MudSelect` értékét `SelectOption` állítja be.
    - Az elavult válasz tesztje `TaskCompletionSource`-szal tartja függőben az első betöltést.
  - **MudBlazor 9:** az `AutoGrow` paramétert a 9-es verzió megszüntette (MUD0002 analizátorhiba). Helyette
    `Sizing="InputSizing.Auto"` került a leírásmezőre.
  - **A kézi ellenőrzésből fakadó javítás:** keskeny nézetben a bejelentő hosszú e-mail-címe belefutott a mellette
    lévő dátumba. Az e-mail `overflow-wrap: anywhere` stílust kapott, a javítást a futó alkalmazásban is ellenőriztem.
  - **ADR nem készült.** A lépés a meglévő rétegszerkezeten (ADR-002) és UI-döntéseken (ADR-001) belül marad;
    architekturális döntés nem született.

## Ellenőrzés
- dotnet build: Build succeeded – 0 Warning(s), 0 Error(s) (`--no-incremental` újrafordítással).
- dotnet test: Passed – 404/404 zöld.
  - Projektenként: Domain.Tests 221, Application.Tests 62, Infrastructure.Tests 30 (+1), Web.Tests 77 (+41),
    ArchitectureTests 14.
  - Kiinduló állapot: 362/362.
- Kézi ellenőrzés a futó alkalmazásban (Development, `http://localhost:5123`, beépített böngésző, seed-felhasználók):
  - Kiinduló állapot: a `ServiceDesk` adatbázisban 0 jegy volt, mindhárom ügyintéző `LastAssignedAt` értéke `NULL`.
    Mindhárom jegy Közepes hatást és Közepes sürgősséget kapott (Közepes prioritás, súly 3).
  - Új jegy űrlap:
    - üres beküldéskor mind az öt mező a magyar kötelezőmező-üzenetet mutatta;
    - a 3 karakteres cím szerveroldali hibája („A cím hossza 5 és 200 karakter között legyen.”) a Cím mezőnél
      jelent meg;
    - a várható prioritás („Közepes”) és a kategória leírása megjelent;
    - sikeres beküldés után „A jegy rögzítve.” Snackbar jelent meg, és az oldal a részletekre navigált.
  - ✓ Dóra HARDWARE jegye („Nem indul a laptop”) Kiss Annához került (egyedül ő jogosult).
  - ✓ Az első NETWORK jegy („Nem érem el a VPN-t”) Tóth Csabához került: terhelése 0, Annáé 3.
  - ✓ A második NETWORK jegy („Lassú a wifi a tárgyalóban”) Kiss Annához került. A terhelés egyenlő (3–3), és Anna
    `LastAssignedAt`-je a régebbi (BR-05).
  - Adatbázis-szinten (`sqlcmd`, csak olvasás) ugyanez látszott:
    - Anna `LastAssignedAt` értéke 11:21:44 UTC, Csabáé 11:21:17 UTC;
    - Nagy Béla `NULL`.
  - Saját jegyeim:
    - a három jegy legújabb elöl jelent meg;
    - a státusz („Kiosztva”) és a prioritás („Közepes”) chipként, a felelős névvel látszott;
    - az idő budapesti (`2026. 10. 29. 12:21`);
    - a lapozó szövege „1–3, összesen 3”;
    - a sorra kattintás a részletekre vitt.
  - Részletek: fejlécadatok és idővonal magyar feliratokkal („Jegy létrehozva – Új”, „Kiosztás – Kiss Anna –
    Rendszer”, „Státuszváltás – Új → Kiosztva”).
  - ✓ Erik Dóra jegyére a „A jegy nem található.” állapotot és a „Vissza a jegyeimhez” gombot kapta.
    - A szervernaplóban Warning jelent meg: `Reporter 213e75cc-… requested ticket 01a18763-…, which is not among
      their tickets`.
    - Erik saját listája üres volt („Még nincs jegyed.”).
    - A szervernaplóban hiba nem volt.
- Javasolt kézi ellenőrzés:
  - Admin (pl. `admin.anna`) megnyitja Dóra egyik jegyét: a részletek megjelennek. Nem létező azonosítóra a
    „Vissza a jegysorhoz” gomb jelenik meg.
  - Két részletoldal közti gyors váltásnál (pl. böngészővissza) mindig az utoljára kért jegy marad látható.

## Ismert korlátok
- A kézi ellenőrzés három jegye a fejlesztői `ServiceDesk` adatbázisban maradt. A P-5A naplójában és ebben a
  lépésben leírt kiosztási elvárások üres jegytáblát feltételeznek; ismétlés előtt az adatbázist újra kell
  létrehozni (pl. `dotnet ef database drop`, majd indítás).
- A `TicketLabels` az `Europe/Budapest` IANA-azonosítót használja. Windows alatt ehhez ICU kell, ami a .NET 10-ben
  alapértelmezés. Ha a `TimeZoneInfo` nem találja az időzónát, a típus inicializálása kivételt dob.
- Ha a kiválasztott kategória a betöltés és a beküldés között inaktívvá válik, a felület Snackbarban jelzi
  („Inaktív kategória nem választható.”), de a kategórialistát nem tölti újra.
- A MudBlazor beépített, nem testre szabott szövegei (pl. a lapozógombok akadálymentes feliratai) nincsenek
  magyarítva; ezt nem ellenőriztem.
- A saját jegyek listája nem rendezhető és nem szűrhető; a sorrend mindig „legújabb elöl”.

## Emberi javítás
Kézi kódmódosításra nem volt szükség. A tervet és a jóváhagyott kiegészítéseket (felelős megjelenítése a saját jegyek listájában, budapesti időzóna-kezelés, betöltésenkénti CancellationTokenSource megszakítás, fix prioritás-chipszínek) jóváhagytam. A beépített böngészős kézi ellenőrzést és a BR-05 terhelésalapú kiosztási logikát (Anna/Csaba elosztás, Erik elutasítása Dóra jegyénél) leellenőriztem. Mind a 404/404 teszt hibátlanul zöld.
## Commit
feat(web): implement reporter ticket ui with mudblazor and bunit tests
