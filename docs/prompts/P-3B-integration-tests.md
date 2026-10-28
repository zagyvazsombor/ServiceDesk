# P-3B – Integrációs tesztek LocalDB-n
- Dátum: 2026-10-28
- Eszköz és modell: Claude Code (asztali alkalmazás), Claude Opus 5.5 (`claude-opus-5-5`)
- Cél és érintett szabályok: a perzisztencia integrációs tesztjei a `ServiceDesk_Tests` LocalDB-adatbázison: egyszeri
  migráció assembly fixture-ben, tesztenkénti Respawn-reset, tesztadat-építők; repository-k, konkurencia, jelöltlekérdezés,
  seeder-idempotencia, Restrict FK. BR-04, BR-05, BR-10; AGENTS.md §1, §3, §7, §11, §12, §14
- Átadott kontextus: `AGENTS.md`, `CLAUDE.md`, `docs/prompts/P-3A-persistence.md`, a Domain entitásai (`Ticket`,
  `TicketHistoryEntry`, `Agent`, `AgentCompetency`, `Category`, `AgentCandidate`, `Result`), az Application portjai és
  `GeneralErrors`, az Infrastructure (`AppDbContext`, konfigurációk, repository-k, `UnitOfWork`, `DatabaseSeeder`,
  `DatabaseInitializer`, `DependencyInjection`), a tesztprojektek, `Directory.Packages.props`, `Directory.Build.props`,
  `.editorconfig`

## Prompt
[P-3B] Integrációs tesztek LocalDB-n
Kontextus: AGENTS.md §7, §12. A P-3A elkészült.

Feladat:
1. ServiceDesk.Infrastructure.Tests: assembly fixture, amely a ServiceDesk_Tests LocalDB-adatbázisra egyszer lefuttatja a migrációkat. Minden teszt előtt Respawn-reset az __EFMigrationsHistory kihagyásával; párhuzamosítás kikapcsolva.
2. Tesztadat-építők (builder) a tesztprojekten belül.
3. Tesztek:
   - Jegy mentése és visszatöltése a történettel együtt; ügyintéző a kompetenciáival együtt.
   - Konkurencia: két külön DbContext ugyanazt a jegyet módosítja → a második mentés GeneralErrors.ConcurrencyConflict.
   - GetEligibleCandidatesAsync: csak aktív, kompetens ügyintéző jön vissza; a nyitott jegyek prioritásai helyesek; Resolved és Closed jegy nem számít bele (BR-04, BR-05).
   - A seeder kétszeri futtatása után a rekordok száma nem változik.
   - Az FK Restrict működik: jeggyel rendelkező felhasználó nem törölhető.

Elfogadás: a tesztek LocalDB-n, Docker nélkül futnak, és kétszer egymás után futtatva is zöldek.

Zárás: AGENTS.md §1 és §14, napló: P-3B-integration-tests.

Jóváhagyás a terv után (kiegészítéssel):
Jóváhagyom a K1–K3 javaslataival, egy kiegészítéssel:
- Új teszt (UnitOfWorkTests): SaveChangesAsync_DuplicateCategoryCode_DoesNotReturnConcurrencyConflict. Két azonos kódú kategória mentése nem ConcurrencyConflict-ot ad. Ha piros, szűkítsd a UnitOfWork fordítását kizárólag a (TicketId, Sequence) index egyediség-sértésére, az index nevének ellenőrzésével; ez az egyetlen megengedett éles kódváltozás ebben a lépésben, és rögzítsd a naplóban.
- K1: ha a P-3A-ban bekerült InternalsVisibleTo, és nincs rá szükség, töröld.
- Ha a fixture-ben a UserManager feloldásához hiányzó szolgáltatás kell (pl. Data Protection), azt a fixture regisztrálja, ne az AddInfrastructure.
- Ellenőrizd, hogy az xunit.runner.json kimásolódik a kimeneti mappába.
Mehet a megvalósítás.

## Eredmény
- Létrehozott és módosított fájlok:
  - `Directory.Packages.props`: új `Respawn` 7.0.0 a „Tests” csoportban.
  - `tests/ServiceDesk.Infrastructure.Tests`:
    - módosítva: `ServiceDesk.Infrastructure.Tests.csproj` (`Respawn` hivatkozás), `xunit.runner.json`
      (`"parallelizeTestCollections": false`), `Persistence/AppDbContextModelTests.cs` (a közös kapcsolati string, K3);
    - új: `TestDatabase.cs`, `DatabaseFixture.cs` (az `[assembly: AssemblyFixture]` regisztrációval), `IntegrationTest.cs`;
    - új `Builders/`: `UserBuilder.cs`, `CategoryBuilder.cs`, `AgentBuilder.cs`, `TicketBuilder.cs`, `TestTime.cs`,
      `ResultExtensions.cs`;
    - új `Persistence/`: `TicketRepositoryTests.cs`, `AgentRepositoryTests.cs`, `UnitOfWorkTests.cs`,
      `DatabaseSeederTests.cs`, `UserForeignKeyTests.cs`.
  - Ez a napló.
  - Éles kód (`src/`) és migráció nem változott. ADR nem készült: a Respawn és a LocalDB a §3 és a §12 rögzített
    döntése.
- Döntések és indoklásuk:
  - **A felhasználó döntései (a terv jóváhagyásakor):**
    - **K1 – nincs `InternalsVisibleTo`.** A tesztek a publikus portokat (`ITicketRepository`, `IAgentRepository`,
      `ICategoryRepository`, `IUnitOfWork`) a DI-ból kérik, ugyanazzal az `AddInfrastructure`-rel, mint az alkalmazás.
      A belső `DatabaseSeeder` a publikus `InitializeDatabaseAsync`-en át fut; a Development-indítás is ezt hívja.
      A P-3A-ban nem került be `InternalsVisibleTo` (a repóban sehol nincs), így törölni sem kellett.
    - **K2 – tisztán `RowVersion`-alapú konkurencia-teszt.** Minden domainmetódus történetbejegyzést is ír, és az EF
      a bejegyzés INSERT-jét a jegy UPDATE-je elé sorolja (P-3A). A „két DbContext ugyanazt a jegyet módosítja”
      teszt (`Start` és `ChangeImpactAndUrgency`) ezért a `(TicketId, Sequence)` indexen bukik el. A
      `SaveChangesAsync_StaleRowVersion_ReturnsConcurrencyConflict` a második módosítást a change trackeren át végzi
      (`Entry(...).Property(t => t.Title).CurrentValue`), történet nélkül. Így csak a `RowVersion`-ellenőrzés futhat le.
      Ez a megkerülés csak a tesztben van, és indoklás áll mellette. Lezárja a P-3A ismert korlátját: a
      `DbUpdateConcurrencyException` ág most automatikusan tesztelt (lásd Ellenőrzés).
    - **K3 – közös kapcsolati string.** A `TestDatabase.ConnectionString` konstanst a fixture és az
      `AppDbContextModelTests` is használja. A meglévő tesztben csak a konstans helye változott.
    - **`SaveChangesAsync_DuplicateCategoryCode_DoesNotReturnConcurrencyConflict` (új teszt): első futásra zöld.**
      A második azonos kódú kategória mentése `DbUpdateException`-t dob, belső `SqlException`-nel (2601,
      `IX_Categories_Code`). A `UnitOfWork` csak akkor fordítja `ConcurrencyConflict`-ra, ha az üzenet a
      `TicketHistoryEntryConfiguration.SequenceIndexName` indexet nevezi meg. A P-3A-ban ez a szűkítés már
      megtörtént, ezért **éles kódváltozásra nem volt szükség**. A teszt a kivételt és a 2601-es hibaszámot is
      ellenőrzi: a más egyedi indexen bekövetkező ütközés váratlan hiba marad (§11), nem „frissítsd az oldalt” üzenet.
    - **A fixture szolgáltatásai:** a fixture csak `AddLogging()`-ot regisztrál a sablon Web-hostja helyett. Ezt a
      `UnitOfWork`, a seeder és a `UserManager` naplózója is igényli. Más nem hiányzott: a `UserManager` és a
      token-providerek feloldódtak. A Data Protectiont a meglévő `AddInfrastructure` `AddAuthentication()` hívása már
      regisztrálja. Az `AddInfrastructure` nem változott.
    - **`xunit.runner.json`:** a csproj `Content … CopyToOutputDirectory="PreserveNewest"` eleme kimásolja. A build
      után a `bin/Debug/net10.0/xunit.runner.json` a `"parallelizeTestCollections": false` beállítással szerepel.
  - **Fixture (`DatabaseFixture`, §12):**
    - xUnit v3 assembly fixture, így tesztfutásonként egyszer jön létre. A konstruktor felépíti a
      `ServiceCollection().AddLogging().AddInfrastructure(config)`-ot. A konfiguráció memóriában van: kapcsolati
      string és teszt-seedjelszó.
    - Az `InitializeAsync` egyszer futtatja a `MigrateAsync`-et; az adatbázist is ez hozza létre, ha még nincs.
      Ezután létrehozza a `Respawner`-t (`DbAdapter.SqlServer`, `TablesToIgnore = [HistoryRepository.DefaultTableName]`,
      vagyis `__EFMigrationsHistory`).
    - A `ResetAsync` saját `SqlConnection`-nel fut. Az Identity-táblákat is üríti, ezért minden teszt maga hozza
      létre a felhasználóit.
    - A seed-jelszó (`Teszt-Jelszo-2026`) csak tesztérték a fixture memóriabeli konfigurációjában. Megfelel a
      mostani Identity-szabályoknak és a §8 legalább 10 karakteres szabályának.
  - **`IntegrationTest` ősosztály:** `IAsyncLifetime`; az `InitializeAsync` minden teszt *előtt* resetel (a prompt
    szerint). Az utolsó teszt adatai a futás után az adatbázisban maradnak, a következő futás első tesztje törli őket.
    A `SaveAsync(params object[])` saját scope-ban menti az előkészített entitásokat, így a teszt friss DbContextből
    olvas vissza. Az aszinkron hívások a `TestContext.Current.CancellationToken`-t kapják (xUnit1051).
  - **Builderek (`internal sealed`, fluent):**
    - A kötelező kapcsolatok konstruktor-paraméterek: `AgentBuilder(user)`, `TicketBuilder(reporter, category)`.
      Így nem lehet FK nélküli entitást építeni.
    - `UserBuilder`: `ApplicationUser` egyedi `@servicedesk.test` címmel, jelszó és szerepkör nélkül; a domaintáblák
      csak az `Id`-jára hivatkoznak.
    - `TicketBuilder.InStatus(status, assignee, admin)`: a rendszer kiosztja a jegyet (`actorUserId = null`, BR-05),
      majd az Admin a fő úton viszi tovább. A lépések egy-egy perccel követik egymást (`TestTime.Now`-tól), így a
      történet időpontjai különböznek.
    - Sikertelen domain-művelet a builderben az előkészítés hibája: `InvalidOperationException` (`EnsureSuccess`).
  - **Tesztek (11 új):**
    - `TicketRepositoryTests` (BR-10):
      - visszatöltés minden mezővel és a teljes történettel; a sorszám, a végrehajtó (bejelentő, rendszer = `null`,
        Admin), az időpont, a művelet és a régi/új érték egyezik, `Sequence` szerint rendezve;
      - a betöltött jegyhez adott bejegyzés INSERT-ként mentődik (5. bejegyzés).
    - `AgentRepositoryTests`:
      - ügyintéző a kompetenciáival;
      - BR-04: csak az aktív, kompetens ügyintéző jön vissza; ha ilyen nincs, üres lista;
      - BR-05: Assigned High + InProgress Critical + egy másik kategóriás Assigned Planning számít bele (terhelés 10);
        a Resolved Medium, a Closed Low és a másik ügyintéző jegye nem; a `LastAssignedAt` is visszajön.
    - `UnitOfWorkTests`: két konkurencia-teszt (az első változás megmarad, a második nem) és a duplikált kategóriakód.
    - `DatabaseSeederTests`: két futás után is 2 szerepkör, 5 felhasználó, 5 szerepkör-hozzárendelés, 5 kategória,
      3 ügyintéző és 7 kompetencia (§7).
    - `UserForeignKeyTests`: jeggyel rendelkező felhasználó törlése `DbUpdateException`-t dob, belső `SqlException`-nel
      (547); a felhasználó megmarad.
    - Ahol van BR, ott `[Trait("BR", …)]` jelölés van.
  - **Respawn 7.0.0:** a §3 engedélyezett listáján szerepel. A legújabb stabil verzió (2025-11-30), netstandard2.1-es,
    és nincs függősége, így a központi tranzitív rögzítést sem érinti.

## Ellenőrzés
- dotnet build: Build succeeded – 0 Warning(s), 0 Error(s) (`--no-incremental` újrafordítással is).
- dotnet test: Passed – 237/237 zöld (Domain.Tests 213, ArchitectureTests 12, Infrastructure.Tests 12; az
  Application.Tests és a Web.Tests még üres).
  - Kétszer egymás után futtatva is 237/237. Az Infrastructure.Tests futásideje kb. 3–5 s, LocalDB-n, Docker nélkül.
  - `dotnet test --filter "BR=BR-04"`: 5 zöld (ebből 2 új integrációs teszt). Az Infrastructure.Tests-ben a
    `BR=BR-05` 1, a `BR=BR-10` 2 teszt.
  - Az Infrastructure.Tests első futása létrehozta és migrálta a `ServiceDesk_Tests` adatbázist; a második futáson
    a migráció már nem változtatott.
- Ideiglenes próba a K2-höz: a `StaleRowVersion` tesztben a második mentést átmenetileg a `UnitOfWork` helyett közvetlenül
  a `DbContext.SaveChangesAsync` végezte, és `DbUpdateConcurrencyException`-t várt. A teszt zöld lett, vagyis a teszt
  valóban a `RowVersion`-ágat futtatja. A módosítást visszaállítottam, utána újrafordítottam és újrafuttattam (237/237).
- Javasolt kézi ellenőrzés:
  - `dotnet test --project tests/ServiceDesk.Infrastructure.Tests` kétszer egymás után, egy másik gépen vagy egy új
    LocalDB-példányon is (az első futás létrehozza az adatbázist).
  - A `ServiceDesk_Tests` adatbázis átnézése (SSMS vagy VS SQL Server Object Explorer): a futás után csak az utolsó
    teszt adatai vannak benne, az `__EFMigrationsHistory` két sort tartalmaz.
  - A builderek és a `TicketBuilder` fő útja olvasásra.
- Ismert korlátok:
  - A tesztekhez helyi `MSSQLLocalDB` példány kell. A `ServiceDesk_Tests` adatbázis megmarad a futások között.
    Ha egy már alkalmazott migrációt kézzel átírnak (a §7 szerint csak indoklással), a tesztadatbázist kézzel kell
    törölni, mert a `MigrateAsync` a már alkalmazott migrációt nem futtatja újra.
  - A párhuzamosítás csak ebben a projektben van kikapcsolva. Más tesztprojekt nem használja a `ServiceDesk_Tests`
    adatbázist, ezért a projektek párhuzamos futása nem ütközik.
  - A Restrict FK-teszt csak a `Ticket.ReporterId` → `AspNetUsers` hivatkozást (és a vele létrejövő
    történetbejegyzés `ActorUserId`-ját) fedi le. Az `Agent.UserId` és a `CategorySuggestion.RequestedByUserId`
    ugyanígy van konfigurálva, de külön teszt nem készült rájuk.
  - A seeder a publikus `InitializeDatabaseAsync`-en át fut, amely előtte `MigrateAsync`-et is hív (itt már nincs
    mit alkalmaznia).
  - A duplikált kategóriakód a `UnitOfWork`-ből kivételként jön ki, és az `ErrorBoundary` kezelné. A felhasználóbarát
    hibaüzenethez a kategória-use case-nek mentés előtt ellenőriznie kell a kód egyediségét; ez a törzsadat-kezelés
    lépésének feladata.
  - A P-3A ismert korlátja változatlan: a sablon személyesadat-törlő oldala a Restrict FK miatt hibára futhat
    (P-9A). A `UserForeignKeyTests` ezt az adatbázis-oldali viselkedést rögzíti.

## Emberi javítás
TODO – a hallgató tölti ki

## Commit
TODO – a hallgató tölti ki
