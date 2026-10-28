# P-3A – Perzisztencia: EF Core-konfiguráció, repository-k, seed
- Dátum: 2026-10-28
- Eszköz és modell: Claude Code (asztali alkalmazás), Claude Opus 5.5 (`claude-opus-5-5`)
- Cél és érintett szabályok: Application-portok és -hibák, `CategorySuggestion` entitás, `AppDbContext` és
  EF-konfigurációk, repository-k és UnitOfWork, `AddServiceDeskDomain` migráció, szerepkör-konstansok, idempotens
  seeder; a jóváhagyáskor kért `TicketHistoryEntry.Sequence`. BR-04, BR-05, BR-06 (jelöltlekérdezés), BR-08, BR-09,
  BR-10, BR-11, BR-13; AGENTS.md §1, §5, §7, §8, §9, §10, §11, §13, §14
- Átadott kontextus: `AGENTS.md`, `CLAUDE.md`, `docs/prompts/P-1B-identity-and-shell.md`,
  `docs/prompts/P-2A-domain-ticket.md`, `docs/prompts/P-2B-assignment-strategy.md`, `docs/adr/ADR-004-rich-domain-model.md`,
  a teljes Domain-kód, az Infrastructure (`AppDbContext`, `DatabaseInitializer`, `DependencyInjection`), a `Program.cs`
  és a tesztprojektek

## Prompt
[P-3A] Perzisztencia: EF Core-konfiguráció, repository-k, seed
Kontextus: AGENTS.md §5 (Application-portok, Infrastructure), §7, §9, §10. A P-2A és a P-2B elkészült.

Feladat:
1. Application-portok (csak interfészek):
   - ITicketRepository: GetByIdAsync (az aggregátum teljes betöltésével), Add.
   - IAgentRepository: GetByIdAsync, GetEligibleCandidatesAsync(categoryId) → AgentCandidate-lista a BR-04 szűrésével és a nyitott jegyek prioritásaival.
   - ICategoryRepository, ICategorySuggestionRepository, IUnitOfWork (SaveChangesAsync → Result).
   - AuthErrors, GeneralErrors, AiErrors az Application-ben.
2. CategorySuggestion entitás a Domainben, a §9 mezőivel.
3. AppDbContext és IEntityTypeConfiguration osztályok a §7 szerint: enum stringként, hosszkorlátok, indexek, FK az AspNetUsers-re navigáció nélkül (Restrict), Ticket.RowVersion shadow property. A TicketHistoryEntry és az AgentCompetency a szülő aggregátumhoz kötve. A Priority oszlopként tárolódik a szűrés miatt, de értékét csak a domain számolja (BR-11).
4. Repository-k és UnitOfWork; DbUpdateConcurrencyException → GeneralErrors.ConcurrencyConflict.
5. Migráció: AddServiceDeskDomain (§7 parancs).
6. Roles.cs (Reporter, Admin) az Application/Common/Security alá.
7. DatabaseSeeder a §7 seed-táblázata szerint, idempotensen; Development-indításkor a MigrateAsync után fut.

Ne csináld: use case-ek, UI, Identity-beállítások (4. lépés).

Elfogadás:
- A migráció lefut; indítás után a seed-felhasználókkal be lehet lépni.
- Két egymás utáni indítás sem duplikál adatot.
- A build és a tesztek zöldek.

Zárás: AGENTS.md §1 és §14, napló: P-3A-persistence.

Jóváhagyás a terv után (eltéréssel):
Jóváhagyom a K1–K4 javaslataival, a következő módosításokkal:
- Hatókör: az Infrastructure.Tests fixture-je, a Respawn és az integrációs tesztek a P-3B feladatai, most ne készüljenek el. A CategorySuggestionTests domaintesztek maradnak.
- Új követelmény: a TicketHistoryEntry kapjon int Sequence mezőt, amelyet a Ticket oszt ki jegyen belül 1-től növekvően, minden bejegyzésnél. Mapping: egyedi index (TicketId, Sequence); a lekérdezések ez szerint rendeznek. Domainteszt: egy műveleten belül több bejegyzés sorrendje helyes. Ez a mostani AddServiceDeskDomain migrációba kerüljön.
- Pontosítás: a CategorySuggestion.Create akkor dob kivételt, ha a confidence NaN vagy a [0, 1] tartományon KÍVÜL esik.
- K1: a naplóban rögzítsd, hogy a döntéshozó azonos a kérővel, és ezt a P-8B use case biztosítja.
- Ismert korlát a naplóba: a sablon személyesadat-törlő oldala Restrict FK miatt hibázhat; a kezelése a P-9A feladata.
Mehet a megvalósítás.

## Eredmény
- Létrehozott és módosított fájlok:
  - `src/ServiceDesk.Domain`:
    - új: `CategorySuggestions/CategorySuggestion.cs`, `CategorySuggestions/CategorySuggestionDecision.cs`;
    - módosítva: `Tickets/TicketHistoryEntry.cs` (`Sequence`), `Tickets/Ticket.cs` (a sorszám kiosztása az `AddHistory`-ban).
  - `src/ServiceDesk.Application` (új fájlok, a csproj nem változott):
    - `Common/Security/Roles.cs`;
    - `Common/Errors/`: `AuthErrors.cs`, `GeneralErrors.cs`, `AiErrors.cs`;
    - `Common/Persistence/`: `ITicketRepository.cs`, `IAgentRepository.cs`, `ICategoryRepository.cs`,
      `ICategorySuggestionRepository.cs`, `IUnitOfWork.cs`.
  - `src/ServiceDesk.Infrastructure` (a csproj nem változott):
    - módosítva: `Persistence/AppDbContext.cs`, `Persistence/DatabaseInitializer.cs`, `DependencyInjection.cs`;
    - új: `Persistence/Configurations/` (`ColumnLengths.cs` és hat `IEntityTypeConfiguration`: Ticket,
      TicketHistoryEntry, Category, Agent, AgentCompetency, CategorySuggestion), `Persistence/Repositories/` (négy
      repository), `Persistence/UnitOfWork.cs`, `Persistence/Seed/DatabaseSeeder.cs`, `Persistence/Seed/SeedOptions.cs`;
    - generált: `Persistence/Migrations/20261028183546_AddServiceDeskDomain.cs`, `…AddServiceDeskDomain.Designer.cs`,
      `AppDbContextModelSnapshot.cs`.
  - `src/ServiceDesk.Web/appsettings.Development.json`: `Seed:DefaultPassword`. A `Program.cs` nem változott.
  - `tests/ServiceDesk.Domain.Tests`: új `CategorySuggestions/CategorySuggestionTests.cs`; a `Tickets/TicketHistoryTests.cs`
    öt sorszám-teszttel bővült.
  - Ez a napló. ADR nem készült.
- Döntések és indoklásuk:
  - **A felhasználó döntései (a terv jóváhagyásakor):**
    - **K1 – a döntéshozó azonos a kérővel.** A `CategorySuggestion` a §9 mezőit kapta, külön `DecidedByUserId` nélkül.
      A javaslatról ugyanaz a Bejelentő dönt a létrehozó űrlapon, aki kérte, ezért a `RequestedByUserId` egyben
      a döntéshozó is (BR-09). Ezt a P-8B use case biztosítja.
    - **K2 – a döntés metódusai később jönnek.** Az `Accept`/`Override`, valamint a `TicketId` és a `DecidedAt`
      beállítása a BR-09 use case-szel együtt készül. Most csak a `Create` van, és a `Decision` `Pending`.
    - **K3:** az `ICategoryRepository` és az `ICategorySuggestionRepository` csak `GetByIdAsync`-et és `Add`-et kapott;
      a további lekérdezések a saját use case-ükkel jönnek.
    - **K4:** `AddRoles<IdentityRole>()` az `AddInfrastructure`-ben. A seeder `RoleManager` nélkül nem tud
      szerepkört létrehozni. Ez nem a §8 Identity-beállítása, és migrációt sem igényel (az `AspNetRoles` tábla
      megvolt). Mellékhatás: a bejelentkezési cookie szerepkör-claimet is kap.
    - **Hatókör:** az Infrastructure.Tests fixture-je, a Respawn és az integrációs tesztek a P-3B-ben készülnek. A
      `Directory.Packages.props` és a tesztprojektek csproj-ai ezért nem változtak, `InternalsVisibleTo` sem kellett.
    - **`TicketHistoryEntry.Sequence` (új követelmény):**
      - `int Sequence`, privát setterrel. A `Ticket.AddHistory` osztja ki `_history.Count + 1` értékkel. Bejegyzés nem
        törlődik, és írás előtt a teljes történet betöltődik, így a sorozat 1-től hézag nélkül nő.
      - A belső `TicketHistoryEntry.Create` 1-nél kisebb sorszámra `ArgumentOutOfRangeException`-t dob.
      - Mapping: egyedi index a `(TicketId, Sequence)` oszlopokon, `IX_TicketHistoryEntries_TicketId_Sequence` néven.
        A `TicketRepository.GetByIdAsync` `Include(t => t.History.OrderBy(e => e.Sequence))`-szel tölt.
      - Az `AddServiceDeskDomain` migráció része.
    - **`confidence`:** a `CategorySuggestion.Create` `ArgumentOutOfRangeException`-t dob, ha az érték NaN vagy
      a [0, 1] tartományon kívül esik. A tervben tévesen „közé esik” szerepelt; a felhasználó pontosította.
  - **Eltérés a prompt 4. pontjától – a történet-sorszám ütközése is `ConcurrencyConflict`:**
    - A repón kívüli próba (lásd Ellenőrzés) megmutatta, mi történik, ha két scope ugyanazt a jegyet módosítja. Az EF
      a `TicketHistoryEntries` INSERT-et a `Tickets` UPDATE elé sorolja. A második mentés ezért az egyedi
      `(TicketId, Sequence)` indexen bukik el, sima `DbUpdateException`-nel, és nem jut el a `RowVersion` ellenőrzéséig.
    - Minden jegyváltozás történetbejegyzést is ír, így a §7 szerinti „A jegyet időközben módosították…” üzenet
      e nélkül gyakorlatilag soha nem jelenne meg; helyette az `ErrorBoundary` általános hibája jönne.
    - A `UnitOfWork` ezért a `DbUpdateConcurrencyException` mellett azt a `DbUpdateException`-t is
      `GeneralErrors.ConcurrencyConflict`-ra fordítja, amelynek belső `SqlException`-je 2601-es (egyedi index sérülése),
      és az üzenete ezt az indexet nevezi meg. Az indexnév a konfigurációban konstans (`SequenceIndexName`).
      Mindkét ág Warning szinten naplóz (§11).
  - **Seed-jelszó `IOptions<SeedOptions>`-on át, nem `IConfiguration`-ből:** a seeder első változata az
    `IConfiguration`-t DI-ból kérte. Ezt a Web-host regisztrálja, egy sima `ServiceCollection` viszont nem; a próbában
    így elbukott. Az `AddInfrastructure` amúgy is megkapja a konfigurációt, ezért ott köti a `Seed` szekciót
    (`services.Configure<SeedOptions>`). A P-3B fixture-jének így nem kell külön `IConfiguration`-t regisztrálnia.
    Hiányzó jelszó esetén a seeder `InvalidOperationException`-t dob. Ez konfigurációs hiba, és csak Development
    indításkor fordulhat elő.
  - **Leképezés (§7):**
    - **Táblák:** `Tickets`, `TicketHistoryEntries`, `Categories`, `Agents`, `AgentCompetencies`, `CategorySuggestions`.
    - **Enumok:** stringként, `HasMaxLength(30)`: `Impact`, `Urgency`, `Priority`, `Status`, `Action`, `Decision`.
    - **Hosszkorlátok:** a domain konstansaiból jönnek, így a §10 korlátai egy helyen vannak.
      - A felhasználó-azonosító oszlopok 450 hosszúak, mint az `AspNetUsers.Id`.
      - A §10 nem ad korlátot a `CategorySuggestion` két mezőjére; saját döntésem: `Model` 100, `PromptVersion` 20.
    - **Kulcsok:** minden Guid kulcs `ValueGeneratedNever()`. Enélkül az EF a betöltött jegyhez adott új
      történetbejegyzést meglévőnek tekintené, és UPDATE-et küldene. A próba igazolta, hogy így INSERT készül.
    - **Aggregátumhatárok:** `HasMany` + `Include` (a §7 szövege szerint), nem `OwnsMany`.
      - Ticket → History: shadow `TicketId` FK, kaszkád törlés.
      - Agent → Competencies: összetett kulcs `(AgentId, CategoryId)`, kaszkád törlés.
      - Mindkét gyűjtemény backing fielden át (`_history`, `_competencies`) töltődik.
    - **FK az `AspNetUsers`-re, navigáció nélkül, `Restrict`-tel:** `Ticket.ReporterId`, `Agent.UserId`,
      `TicketHistoryEntry.ActorUserId` (nullable) és `CategorySuggestion.RequestedByUserId`.
    - **További FK-k, `Restrict`-tel:** `Ticket.CategoryId`, `Ticket.AssigneeAgentId`, `AgentCompetency.CategoryId`
      (BR-13: a kategória nem törölhető), `CategorySuggestion.SuggestedCategoryId` és `CategorySuggestion.TicketId`.
    - **Indexek:**
      - egyedi: `Category.Code`, `Agent.UserId`, `(TicketId, Sequence)`;
      - nem egyedi: `Ticket.ReporterId`, `Ticket.AssigneeAgentId`, `Ticket.Status`;
      - a többi FK-indexet az EF konvenció hozza létre.
    - **Konkurencia és prioritás:** a `Ticket.RowVersion` shadow `byte[]`, `IsRowVersion()`. A `Priority` sima oszlop
      privát setterrel; csak a domain állítja (`// BR-11` a konfigurációban).
  - **Repository-k:**
    - `internal sealed`, primary constructorral; a regisztráció scoped, az `AddInfrastructure`-ben.
    - A `GetByIdAsync` követő (tracking) lekérdezés, a gyerek-gyűjteménnyel együtt.
    - A `GetEligibleCandidatesAsync` egyetlen `AsNoTracking` lekérdezés:
      - aktív, a kategóriára kompetens ügyintézők (BR-04);
      - mindegyikhez a nyitott (`Assigned`, `InProgress`) jegyeinek prioritás-listája (BR-05);
      - a terhelést és a választást a domain végzi (P-2B), üres lista esetén a BR-06.
    - A `TicketRepository.GetByIdAsync` nem szűr Bejelentőre: írási útvonalon az Admin műveletei használják. A BR-02
      szűrése az olvasó lekérdezésekbe (`ITicketQueries`) kerül.
  - **Seeder (§7):**
    - A „ha hiányzik, létrehozza” elven működik, a meglévő adatot nem írja felül.
      - Szerepkörök és felhasználók: `RoleManager` / `UserManager`. A meglévő seed-felhasználó szerepkörét pótolja,
        a jelszavát nem állítja vissza.
      - Kategóriák: kód szerint. Ügyintézők: `UserId` szerint, a §7 kompetenciáival.
      - A domain-adatok egyetlen `SaveChangesAsync`-szel mentődnek.
    - A seed-felhasználók `EmailConfirmed = true` értéket kapnak. A `RequireConfirmedAccount = true` a 4. lépésig
      marad, és enélkül nem tudnának belépni.
    - **Seed-jelszó:** a §7 szerint az `appsettings.Development.json`-ban van, és a mostani Identity-alapszabálynak,
      valamint a §8 legalább 10 karakteres szabályának is megfelel. Csak fejlesztői demó.
    - Hibás seed-adat (sikertelen `IdentityResult` vagy domain-`Result`) programozási hiba, ezért
      `InvalidOperationException`.
    - A napló csak darabszámokat ír (Information), e-mail-címet és jelszót nem.
    - A kategórialeírások rövid magyar szövegek; a későbbi LLM-prompt is ezeket használja.
  - **Hibák:**
    - `Auth.Forbidden` (Forbidden): „Nincs jogosultságod ehhez a művelethez.”
    - `General.ConcurrencyConflict` (Conflict): a §7 szövege.
    - `Ai.NoSuggestion` (Unavailable): „Most nem sikerült kategóriát javasolni, válassz kézzel.”
  - **`CategorySuggestion.Create`:** `Category`-t kap, a `Ticket.Create` mintájára (P-2A K1).
    - Inaktív kategóriára `CategoryErrors.Inactive` a válasz (`// BR-08, BR-13`).
    - A többi bemenetet az AI-adapter már validálja (§9), ezért ott a szabálysértés programozási hiba, és kivételt dob:
      üres azonosító, modellnév vagy prompt-verzió; `null` indoklás; hosszkorlát túllépése; `confidence` a fenti szerint.
    - Az indoklást nem trimeli.
  - **ADR nem készült:** a leképezést a §5 és a §7 előre meghatározza. A sorszám és az ütközés-leképezés ebben
    a naplóban van indokolva.

## Ellenőrzés
- dotnet build: Build succeeded – 0 Warning(s), 0 Error(s) (`--no-incremental` újrafordítással is).
- dotnet test: Passed – 226/226 zöld (Domain.Tests 213, ArchitectureTests 12, Infrastructure.Tests 1; az Application.Tests
  és a Web.Tests még üres).
  - Új domaintesztek (25):
    - `CategorySuggestionTests` 20: érvényes létrehozás, inaktív kategória, `confidence` a 0 és az 1 határon, valamint
      öt tartományon kívüli érték (−0,01, 1,01, NaN, ±∞), indoklás a korlátnál és fölötte, hiányzó vagy túl hosszú
      modellnév és prompt-verzió, hiányzó kérő.
    - `TicketHistoryTests` +5: az első bejegyzés sorszáma 1; az `AssignTo` és a `ChangeImpactAndUrgency` egy
      műveleten belüli bejegyzései sorrendben számozódnak; a fő úton a sorozat 1-től hézag nélküli; sikertelen
      művelet után sincs hézag.
  - Az `AppDbContextModelTests` (modell és migráció egyezése) zöld, az explicit indexnév után is.
  - `dotnet test --filter "BR=BR-10"`: zöld, 16 teszt.
- Indítás (Development, `dotnet run --project src/ServiceDesk.Web --launch-profile http`):
  - 1. indítás: „Applying migration '20261028183546_AddServiceDeskDomain'”, majd „Database seed finished; created
    2 roles, 5 users, 5 categories, 3 agents.”
  - 2. és 3. indítás: „No migrations were applied. The database is already up to date.”, majd „created 0 roles,
    0 users, 0 categories, 0 agents.”
  - Adatbázis a 2. indítás után: 2 szerepkör, 5 seed-felhasználó, 5 szerepkör-hozzárendelés, 5 kategória, 3 ügyintéző,
    7 kompetencia.
    - Kiss Anna: HARDWARE, NETWORK; Nagy Béla: ACCESS, SOFTWARE; Tóth Csaba: NETWORK, OTHER, SOFTWARE; mind Admin.
    - Dóra és Erik Reporter. Minden kategória aktív.
  - Belépés a beépített böngészőben: `admin.anna@servicedesk.local` és `bejelento.dora@servicedesk.local` sikeres;
    a menüben a felhasználónév és a Kijelentkezés látszik.
- Repón kívüli próba: konzolprogram a scratchpad mappában, a `ServiceDesk_Probe` adatbázison, amelyet a futás végén
  törölt; a repóba nem került.
  - A seeder kétszer futott, utána 5/3/7/5 sor (kategória/ügyintéző/kompetencia/felhasználó).
  - Jegy mentése, kiosztása és újratöltése után a történet `1:Created, 2:Assigned, 3:StatusChanged`; az új
    bejegyzés INSERT-ként mentődik.
  - `GetEligibleCandidatesAsync`:
    - HARDWARE kiosztás előtt: csak Kiss Anna, üres listával. Kiosztás után: `[High]`, terhelés 4.
    - NETWORK: Anna és Tóth Csaba.
  - Két scope ugyanazon a jegyen (`Resolve`/`Resolve`, illetve `ChangeImpactAndUrgency` kétszer):
    - a javítás előtt a második mentés `DbUpdateException`-t dobott (egyedi index);
    - a javítás után mindkét esetben `General.ConcurrencyConflict` az eredmény.
- Javasolt kézi ellenőrzés:
  - Belépés a többi seed-felhasználóval (`admin.bela`, `admin.csaba`, `bejelento.erik`).
  - A táblák, indexek és FK-k átnézése (SSMS vagy VS SQL Server Object Explorer), különösen az
    `IX_TicketHistoryEntries_TicketId_Sequence` és a `Restrict` törlési szabályok.
  - A kategórialeírások szövegezése a `DatabaseSeeder`-ben, mert ezeket a későbbi LLM-prompt is használja.
- Ismert korlátok:
  - A sablon személyesadat-törlő oldala (`Account/Manage/DeletePersonalData`) hibára futhat, ha a felhasználóra
    `Restrict` FK hivatkozik: jegy, ügyintézői profil, történetbejegyzés vagy javaslat. A kezelése a P-9A feladata.
  - Integrációs tesztek még nincsenek a repository-kra, a UnitOfWork-re és a seederre. A fenti próba kézi és egyszeri;
    az automatizált változat a P-3B-ben készül.
  - A tisztán `RowVersion`-alapú ág (`DbUpdateConcurrencyException`) a próbában nem futott le. Minden jegyváltozás
    történetbejegyzést is ír, és az EF ezt a beszúrást sorolta előre. Az ág a §5 szerint megmaradt arra az esetre,
    ha a jegy UPDATE-je fut előbb.
  - A sorszám-ütközés felismerése SQL Server-specifikus: a 2601-es hibaszám és az indexnév szerepel az üzenetben.
  - Az `Agent.LastAssignedAt`-en nincs konkurenciavédelem: két egyidejű kiosztásnál az utolsó írás nyer. Ez csak
    a BR-05 holtverseny-feloldását érinti.
  - A seeder a meglévő seed-sorokat nem frissíti: ha egy kategórialeírás a kódban változik, a meglévő adatbázisban
    a régi marad.
  - A fejlesztői adatbázis korábbi, regisztrációval létrehozott tesztfelhasználói (P-1B) szerepkör nélkül maradtak.
    A regisztrációkor kiosztott `Reporter` szerepkör a 4. lépés feladata.

## Emberi javítás
Kódjavításra nem volt szükség
## Commit
feat(persistence): configure ef core entities, repositories, migrations and database seeder
