# P-4B – Application-szintű autorizáció és a futtató tesztjei
- Dátum: 2026-10-28
- Eszköz és modell: Claude Code (asztali alkalmazás), Claude Opus 5.5 (`claude-opus-5-5`)
- Cél és érintett szabályok:
  - Ellenőrző segédmetódusok az `ICurrentUser`-re (`RequireAuthenticated`, `RequireRole`), `AuthErrors.Forbidden`
    eredménnyel; minden handler ezzel kezd.
  - Első use case: `GetCurrentUserProfile`; az AppBar ebből mutatja a nevet.
  - Tesztek: Application (segédmetódusok, handler), Web (`UseCaseRunner`), ArchitectureTests (`Components.Pages`).
  - Szabályok: AGENTS.md §1, §4, §5 (Application, Infrastructure, Web), §6, §7, §8, §11, §12, §13, §14.
- Átadott kontextus:
  - `AGENTS.md`, `CLAUDE.md`, `docs/prompts/P-4A-identity-runner.md`, `docs/prompts/P-3B-integration-tests.md`.
  - Application: `ICurrentUser.cs`, `Roles.cs`, `AuthErrors.cs`, `GeneralErrors.cs`, a `Common/Persistence` portjai,
    `DependencyInjection.cs`; Domain: `Result.cs`, `ResultOfT.cs`, `Error.cs`, `ErrorType.cs`, `Agent.cs`.
  - Infrastructure: `DependencyInjection.cs`, `AppDbContext.cs`, `AgentRepository.cs`, `AgentConfiguration.cs`,
    `ApplicationUser.cs`, `DatabaseSeeder.cs`.
  - Web: `UseCaseRunner.cs`, `CurrentUser.cs`, `UseCaseServiceCollectionExtensions.cs`, `MainLayout.razor`,
    `UserMenu.razor`, `NavMenu.razor`, `Routes.razor`, `App.razor`, `_Imports.razor`, `Program.cs`.
  - A tesztprojektek (`UseCaseRunnerTests`, `RecordingLogger`, `UserMenuTests`, `DependencyRuleTests`,
    `DatabaseFixture`, `IntegrationTest`, builderek), `Directory.Packages.props`, `Directory.Build.props`, `.editorconfig`.

## Prompt
[P-4B] Application-szintű autorizáció és a futtató tesztjei
Kontextus: AGENTS.md §5, §8, §12. A P-4A elkészült.

Feladat:
1. Application/Common/Security: rövid ellenőrző segédmetódusok az ICurrentUser-re (pl. RequireAuthenticated, RequireRole), amelyek AuthErrors.Forbidden eredményt adnak. Minden handler ezzel kezd (§5 handler-sorrend).
2. Első valódi use case: GetCurrentUserProfile query (UserId, e-mail, szerepkörök; Admin esetén az Agent-profil neve és aktív állapota). Az AppBar ebből mutatja a nevet.
3. Tesztek:
   - Application.Tests: a segédmetódusok és a GetCurrentUserProfile handler; nem bejelentkezett felhasználó → Forbidden; Reporter és Admin helyes adatot kap.
   - Web.Tests: a UseCaseRunner handlere a saját scope-jában a circuit felhasználóját látja; két egymás utáni hívás két különböző scope-ot kap; a kivétel naplózva és továbbdobva.
   - ArchitectureTests: a Components/Pages névtér nem hivatkozik a CurrentUser implementációra és az AppDbContextre.

Elfogadás: a tesztek zöldek; Admin esetén az AppBar az ügyintéző nevét mutatja (pl. Kiss Anna).

Zárás: AGENTS.md §1 és §14, napló: P-4B-authorization.

Jóváhagyás a terv után (eltérésekkel):
Jóváhagyom, a következő eltérésekkel:
- K1: igen, IUserQueries. Az AGENTS.md §5 portlistájába vedd fel az IUserQueries-t, és rögzítsd a naplóban, hogy a szabályrendszer bővült.
- K2: a megtagadott hozzáférést a UseCaseRunner naplózza központilag, Warning szinten (handler neve, UserId, hibakód), nem a segédmetódus. A segédmetódusok ILogger nélkül maradnak. Ha a Result<T> nem a Result-ból származik, vezess be egy minimális közös interfészt, amelyből a runner kiolvassa a hibát. Teszt: Forbidden eredménynél Warning-napló keletkezik, sikernél nem.
- K3, K4, K6: a javaslat szerint.
- K5: a MainLayout-ban a UserMenu kapjon saját ErrorBoundary-t, amelynek tartalék tartalma a claimből vett e-mail-cím. Teszt: kivételt dobó runner mellett az AppBar az e-mail-címet mutatja, és a hiba nem terjed tovább.
Mehet a megvalósítás.

## Eredmény
- Létrehozott és módosított fájlok:
  - `AGENTS.md`: a §5 portlistája bővült az `IUserQueries`-szel (K1). **A szabályrendszer ezzel a lépéssel bővült**;
    más szabály nem változott.
  - `src/ServiceDesk.Application`:
    - új `Common/Security/CurrentUserAuthorization.cs`: `RequireAuthenticated()` és `RequireRole(role)`.
    - új `Common/Persistence/IUserQueries.cs`: `GetProfileAsync(userId, ct)` → `CurrentUserProfileDto?`.
    - új `Users/GetCurrentUserProfile/`: `GetCurrentUserProfileQuery`, `GetCurrentUserProfileHandler`,
      `CurrentUserProfileDto`, `AgentProfileDto`.
  - `src/ServiceDesk.Infrastructure`:
    - új `Persistence/Queries/UserQueries.cs`;
    - `DependencyInjection.cs`: `AddScoped<IUserQueries, UserQueries>()`.
  - `src/ServiceDesk.Web`:
    - `UseCases/UseCaseRunner.cs`: a `Forbidden` típusú eredmény Warning-naplója (K2).
    - `Components/Layout/UserMenu.razor`: a profil lekérése a runneren át; Adminnál az ügyintéző neve.
    - `Components/Layout/MainLayout.razor`: a `UserMenu` saját `ErrorBoundary`-t kapott (K5).
  - Tesztek:
    - `tests/ServiceDesk.Application.Tests`: `ServiceDesk.Application.Tests.csproj` (`NSubstitute`); új `TestUsers.cs`,
      `Common/Security/CurrentUserAuthorizationTests.cs` (9), `Users/GetCurrentUserProfile/GetCurrentUserProfileHandlerTests.cs` (5).
    - `tests/ServiceDesk.Web.Tests`:
      - `UseCases/UseCaseRunnerTests.cs` (+6), új `UseCases/GetCurrentUserProfileRunnerTests.cs` (2);
      - `UseCases/RecordingLogger.cs` (a sablonértékeket is rögzíti), új `UseCases/LogEntry.cs`;
      - `Layout/UserMenuTests.cs` (2 → 4), új `Layout/MainLayoutTests.cs` (2).
    - `tests/ServiceDesk.Infrastructure.Tests`: új `Persistence/UserQueriesTests.cs` (4, K4).
    - `tests/ServiceDesk.ArchitectureTests/DependencyRuleTests.cs`: új `[Theory]` (2 sor).
  - Ez a napló. Migráció nem kellett, mert a modell nem változott. Új NuGet-csomag nem került be (az `NSubstitute`
    már a központi listán volt).
- Döntések és indoklásuk:
  - **A felhasználó döntései (a terv jóváhagyásakor):**
    - **K1 – `IUserQueries` olvasási port.**
      - Az `Application/Common/Persistence` mappában van, az `ITicketQueries` mintájára. Az implementáció
        (`UserQueries`) egyetlen `AsNoTracking()` lekérdezés, `Select`-tel közvetlenül a DTO-ba (§7): e-mail
        (`AspNetUsers`), szerepkörök (`AspNetUserRoles` + `AspNetRoles`, ábécérendben), Agent-profil (`Agents`).
      - Az Agent-profilt `FirstOrDefault` adja; legfeljebb egy van, mert az `Agent.UserId` egyedi.
      - Az elvetett alternatíva (`IAgentRepository.GetByUserIdAsync` és `ICurrentUser.Email`) íráshoz készült
        repositoryn át olvasott volna, és a P-4A interfészét is módosította volna.
    - **K2 – a megtagadott hozzáférést a `UseCaseRunner` naplózza.**
      - A `Result<T>` a `Result`-ból származik, ezért **nem kellett közös interfész**: a runner
        `result is Result { IsFailure: true } failure` mintával olvassa ki a hibát.
      - Warning csak `ErrorType.Forbidden` esetén keletkezik, sablonnal:
        `Use case {Handler} denied access to user {UserId} with {ErrorCode}`. Névtelen felhasználónál a `UserId` null.
        A napló e-mail-címet nem tartalmaz, csak az Identity-azonosítót (§8).
      - A segédmetódusok `ILogger` nélküliek; az Application nem kapott naplózócsomagot.
    - **K3 – `Result<string>`:** siker esetén a segédmetódus értéke a bejelentkezett felhasználó Id-ja, így a handler
      `UserId!` nélkül jut hozzá. Az „azonosítva, de Id nélkül” ellentmondásos állapot is `Forbidden`.
    - **K4 – Infrastructure-teszt:** a `UserQueriesTests` a valódi lekérdezést ellenőrzi LocalDB-n. Itt dől el,
      hogy a Reporter és az Admin a valóságban helyes adatot kap. A handler-teszt a helyettesített porttal
      ezt nem tudja ellenőrizni.
    - **K5 – saját `ErrorBoundary` a `UserMenu` körül.**
      - A `MainLayout` AppBarjában van. Tartalék tartalma `AuthorizeView`-ban a claimből vett e-mail-cím
        (`ClaimTypes.Email`, tartalék: `Identity.Name`), `MudText`-ként.
      - Az `ErrorContent` paramétere `Context="exception"`, hogy ne ütközzön az `AuthorizeView` `context`-jével.
      - Ez az `ErrorBoundary` navigáláskor nem áll helyre (nincs `@ref`). A tartalék a következő újratöltésig marad.
      - Sikertelen `Result` (kivétel nélkül) esetén a `UserMenu` maga marad a claimből vett e-mail-címnél,
        snackbar nélkül.
    - **K6 – `RequireAuthenticated`:**
      - A saját profilt minden bejelentkezett felhasználó olvashatja, szerepkör nélkül is.
      - A §12 „rossz szerepkör” és „idegen jegy” esete itt nem értelmezhető. A query-nek nincs saját Id-ja,
        mindig a bejelentkezett felhasználó Id-jával kérdez. Ezt a teszt ellenőrzi.
      - Ha a felhasználót érvényes cookie mellett törölték, `AuthErrors.Forbidden` a válasz.
  - **Handler-sorrend (§5):**
    - (1) `RequireAuthenticated`;
    - (2) validáció nem kell, mert a query üres record, nincs bemenete;
    - (3) betöltés a porton át;
    - (6) DTO;
    - (4) domainművelet és (5) mentés nincs.
  - **`UserMenu`:**
    - A profilt az `OnInitializedAsync`-ben kéri, egyszer komponenspéldányonként. Interaktív módban ez
      circuitenként egyszer, a statikus SSR Account-oldalakon kérésenként egyszer történik.
    - Névtelen látogatónál nem hívja a use case-t, különben minden névtelen oldalbetöltés „denied access” Warningot
      írna.
    - Az `Admin`-név / e-mail választás csak megjelenítés. A `CancellationTokenSource`-ot a `Dispose` megszakítja.
    - A `currentUrl` mező neve nem változott (sablonból jött); az új mezők `_` előtagot kaptak.
  - **Web-tesztek:**
    - A P-4A tesztjei már lefedték a három kért esetet, ezért bővítettem őket:
      - a handler `ICurrentUser`-e a use case-scope példánya, a circuit-scope `CurrentUser`-e anonim marad;
      - felhasználóváltás két hívás között;
      - kivételnél a scope lezárul;
      - a kivétel naplóbejegyzése a `{Handler}` értéket is tartalmazza.
    - K2-tesztek:
      - `Result<T>` Forbidden → Warning a három értékkel;
      - siker → nincs napló;
      - nem Forbidden hiba (NotFound) → nincs napló.
    - A `GetCurrentUserProfileRunnerTests` a valódi `AddApplication()`-nel futtatja a valódi handlert (helyettesített
      `IUserQueries`). Admin esetén a circuit felhasználójának Id-jával kérdez. Névtelen felhasználónál `Forbidden` és
      Warning a `GetCurrentUserProfileHandler` névvel.
    - A `MainLayoutTests` a teljes layoutot rendereli. A `MudStaticNavDrawerToggle` miatt kell az
      `AddBunitPersistentComponentState()` és a `Renderer.SetRendererInfo(new RendererInfo("Server", true))`.
      Az utóbbi a szolgáltatások regisztrálása után kerül beállításra.
    - Ha a kivétel kijutna az `ErrorBoundary`-ből, a bUnit a renderelésnél hibát dobna. A teszt azt is ellenőrzi,
      hogy az oldaltartalom megjelenik, és az oldal `ErrorBoundary`-jének hibaüzenete (`.mud-alert`) nincs ott.
  - **ArchitectureTests:** `WebPages_AllTypes_DoNotDependOnForbiddenType` `[Theory]`, `typeof(CurrentUser)` és
    `typeof(AppDbContext)` teljes nevével. Az `AppDbContext`-et a meglévő Infrastructure-névtérszabály is lefedi;
    a prompt kérésére külön sor ellenőrzi.
  - **ADR nem készült.** Az új port és a központi Forbidden-napló a meglévő rétegszerkezetbe illeszkedik (ADR-002).
    A szabálybővítést az AGENTS.md és ez a napló rögzíti.
  - **Ideiglenes indítókonfiguráció:** a böngészős ellenőrzéshez `.claude/launch.json` készült
    (`dotnet run --launch-profile http`, 5123-as port). Az ellenőrzés után törölve lett, a munkafába nem került.

## Ellenőrzés
- dotnet build: Build succeeded – 0 Warning(s), 0 Error(s) (`--no-incremental` újrafordítással).
- dotnet test: Passed – 295/295 zöld.
  - Projektenként: Domain.Tests 213, ArchitectureTests 14 (+2), Infrastructure.Tests 21 (+4),
    Application.Tests 16 (+14), Web.Tests 31 (+12).
  - Kiinduló állapot: 263/263.
- Negatív próba az architektúra-szabályra:
  - Átmenetileg `@inject CurrentUser` és `@inject AppDbContext` került a `Pages/Home.razor`-ba. Erre 4 teszt lett
    piros: az új `[Theory]` mindkét sora és a két meglévő Infrastructure-szabály, „Violating types: …Pages.Home”.
  - A fájlt visszaállítottam; a `git diff` üres.
- Kézi ellenőrzés a beépített böngészőben (`http://localhost:5123`, Development, fejlesztői `ServiceDesk` adatbázis):
  - `admin.anna` (egy korábbi munkamenetből bejelentkezve):
    - az AppBarban „KISS ANNA” látszott (a `MudButton` nagybetűs), a Kezdőlapon és a statikus SSR
      `Account/Manage` oldalon is;
    - a menüben csak az Admin-linkek voltak.
  - Kijelentkezés után belépés `bejelento.dora` seed-fiókkal: az AppBar az e-mail-címet mutatta, a menüben az Új jegy
    és a Saját jegyeim látszott.
  - Szervernapló: hiba nincs. „Denied access” Warning nem keletkezett; a névtelen Login oldal nem hívta a use case-t.
    Egyetlen Warning a http-profil miatti „Failed to determine the https port for redirect.”.
- Javasolt kézi ellenőrzés:
  - `admin.bela` és `admin.csaba` belépése: „Nagy Béla”, illetve „Tóth Csaba” az AppBarban.
  - Futó alkalmazás mellett a LocalDB leállítása (`sqllocaldb stop MSSQLLocalDB`), majd újratöltés.
    - Az AppBarban az e-mail-cím jelenik meg, a menü és a Kezdőlap működik.
    - A naplóban a runner Error-bejegyzése látszik.
    - Utána `sqllocaldb start MSSQLLocalDB`.
  - Az AppBar keskeny ablakban, hosszú névvel.

## Ismert korlátok
- A név komponenspéldányonként egyszer töltődik. Ha egy ügyintéző neve vagy aktív állapota később változik
  (ügyintéző-kezelés), az AppBar csak új circuitben (újratöltés, újbóli belépés) frissül.
- Az `ErrorBoundary` tartalékában csak az e-mail-cím van, Kijelentkezés gomb nincs (a jóváhagyott K5 szerint).
  Kijelentkezni újratöltés után, vagy az `Account/Manage` oldalon át lehet.
- A statikus SSR Account-oldalak minden kérésnél lekérdezik a profilt az AppBarhoz (egy kis lekérdezés oldalanként).
- A profil szerepkörei az adatbázisból jönnek, az autorizáció viszont a cookie claimjeiből. A kettő legfeljebb a
  revalidációs időközig (30 perc) térhet el. A szerepkörök ma nem jelennek meg a felületen.
- A runner csak az `ErrorType.Forbidden` eredményt naplózza megtagadott hozzáférésként. A BR-02 szerinti idegen jegyre
  adott `NotFound` nem különböztethető meg a valódi „nincs ilyen” esettől, ezért nem kerül Warningként a naplóba.
- A `UserMenu` váratlan kivételét a runner Error-szinten naplózza, és a keretrendszer `ErrorBoundary`-naplózója is
  rögzítheti, így ugyanaz a hiba kétszer is megjelenhet a naplóban.
- A P-4A ismert korlátja („az Application `DependencyInjectionTests` üresen teljesül”) megszűnt: a konvenciótesztek
  most a `GetCurrentUserProfileHandler` scoped regisztrációját ellenőrzik.

## Emberi javítás
Kézi forráskód-módosításra nem volt szükség.
## Commit
feat(auth): add application-level authorization, user profile query and runner tests