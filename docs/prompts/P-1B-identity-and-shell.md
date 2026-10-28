# P-1B – Identity az Infrastructure-be, UI-keret
- Dátum: 2026-10-28
- Eszköz és modell: Claude Code (asztali alkalmazás), Claude Opus 5.5 (`claude-opus-5-5`)
- Cél és érintett szabályok: Identity és DbContext áthelyezése az Infrastructure-be, új migráció, DI-belépési pontok,
  fejlesztői migráció indításkor, UI-keret (layout, ErrorBoundary, hu-HU, menü); AGENTS.md §1, §4, §5, §6, §7, §11, §13, §14
- Átadott kontextus: `AGENTS.md`, `CLAUDE.md`, `docs/prompts/P-1A-solution-skeleton.md`

## Prompt
[P-1B] Identity az Infrastructure-be, UI-keret
Kontextus: AGENTS.md §4, §5 (Infrastructure, Web), §6, §7. A P-1A elkészült, a build zöld.

Feladat:
1. Helyezd át az ApplicationUser-t az Infrastructure/Identity alá, a sablon DbContextjét pedig Infrastructure/Persistence/AppDbContext.cs néven (AppDbContext : IdentityDbContext<ApplicationUser>). A Web csak a Program.cs-ben és a Components/Account alatt hivatkozhat rájuk.
2. Töröld a sablon migrációit, és hozz létre új InitialIdentity migrációt a §7 parancsával. Kapcsolati string: §7.
3. Hozd létre az AddApplication() és AddInfrastructure(IServiceCollection, IConfiguration) belépési pontot. Az Identity- és DbContext-regisztráció az AddInfrastructure-be kerül; a Program.cs ezeket hívja.
4. Development környezetben indításkor fusson a Database.MigrateAsync(); a seeder a 3. lépésben jön.
5. App.razor: a PageRenderMode interaktív ága new InteractiveServerRenderMode(prerender: false) legyen (§6).
6. MainLayout: MudLayout AppBarral és Drawerrel; a providerek egyszer; a @Body ErrorBoundary-ben, magyar hibaüzenettel és Újrapróbálás gombbal. Kultúra: hu-HU.
7. NavMenu: egyelőre csak Kezdőlap; a szerepkör-függő menü a 4. lépésben készül.

Elfogadás:
- Build, tesztek és architektúra-tesztek zöldek.
- Indításkor létrejön a ServiceDesk adatbázis; a regisztráció és a bejelentkezés működik.
- A bejelentkező és a regisztráló oldal statikus SSR-ben fut, MudBlazor-stílussal.

Zárás: AGENTS.md §1 és §14, napló: P-1B-identity-and-shell.

Kiegészítő utasítás (a Register-hiba jelentése után):
Javítsd most a Register.razor hibát (return; az átirányítás után), és frissítsd a P-1B naplót.

## Eredmény
- Létrehozott és módosított fájlok:
  - `src/ServiceDesk.Infrastructure`:
    - új: `Identity/ApplicationUser.cs`, `Persistence/AppDbContext.cs`, `Persistence/DatabaseInitializer.cs`,
      `DependencyInjection.cs`;
    - generált: `Persistence/Migrations/20261028105514_InitialIdentity.cs`, `…InitialIdentity.Designer.cs`,
      `AppDbContextModelSnapshot.cs`;
    - `ServiceDesk.Infrastructure.csproj`: `FrameworkReference Microsoft.AspNetCore.App`,
      `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.SqlServer`.
  - `src/ServiceDesk.Application`: új `DependencyInjection.cs`; a csproj kapja a
    `Microsoft.Extensions.DependencyInjection.Abstractions` hivatkozást.
  - `src/ServiceDesk.Web`:
    - törölve: `Data/` (sablon DbContext, `ApplicationUser`, a `CreateIdentitySchema` migráció és snapshot),
      `Components/Pages/Auth.razor`;
    - `Program.cs`, `ServiceDesk.Web.csproj` (a két Identity/EF csomag kikerült), `appsettings.json`,
      `appsettings.Development.json`;
    - `Components/App.razor` (`lang="hu"`), `Components/Layout/MainLayout.razor`, `Components/Layout/NavMenu.razor`,
      `Components/Pages/Home.razor`;
    - `Components/Account` (31 fájl): a `ServiceDesk.Web.Data` → `ServiceDesk.Infrastructure.Identity` using csere.
      A `Pages/Register.razor`-ban ezen felül egy `return;` került a kódba (a kiegészítő utasításra, lásd lent).
  - Gyökér: `Directory.Packages.props`. Új `Application` csoport (`Microsoft.Extensions.DependencyInjection.Abstractions`
    10.0.14), az Infrastructure csoport „átmeneti” megjegyzése törölve.
  - `tests/ServiceDesk.Infrastructure.Tests`: `Persistence/AppDbContextModelTests.cs`, Shouldly-hivatkozás a csproj-ban.
  - `docs/adr/ADR-002-clean-architecture.md`, `docs/adr/ADR-003-sql-server-localdb.md`: az „átmenetileg…”
    következmény-mondatok a megvalósult állapotra frissítve. Ez a napló.
- Döntések és indoklásuk:
  - **A felhasználó döntései (a terv jóváhagyásakor):**
    - A §8 Identity-beállításai későbbi lépésben jönnek. Most a sablon értékei maradnak:
      `RequireConfirmedAccount = true`, `IdentitySchemaVersions.Version3`, alapértelmezett jelszószabály,
      `lockoutOnFailure: false`. A kódban komment jelzi.
    - A menüben a Kezdőlap mellett megmaradnak a fiók-linkek magyarul (Regisztráció, Bejelentkezés; bejelentkezve
      a felhasználónév és a Kijelentkezés). Az „Auth Required” menüpont és az `Auth.razor` mintaoldal törlődött.
    - Az `AddApplication()` már most reflexióval scoped-ként regisztrál minden nem absztrakt, nem generikus
      `*Handler` osztályt (§5). A validátorok regisztrációja a FluentValidation-nel jön, az első validátorral együtt.
  - **5. pont:** az `App.razor` interaktív ága a P-1A óta `new InteractiveServerRenderMode(prerender: false)`
    statikus mezőben. Most csak a `lang="en"` → `lang="hu"` csere történt.
  - **Az `AddInfrastructure` tartalma:**
    - `AddDbContext<AppDbContext>(UseSqlServer)`;
    - `AddAuthentication(...).AddIdentityCookies()`;
    - `AddIdentityCore<ApplicationUser>` + EF-tár + SignInManager + token providerek.
    - A Web-típusok regisztrációja a `Program.cs`-ben maradt (`IdentityRedirectManager`, a revalidáló
      `AuthenticationStateProvider`, a no-op `IEmailSender`), mert az Infrastructure nem hivatkozhat a Webre.
      Az `AddDatabaseDeveloperPageExceptionFilter()` is ott maradt, mert Web-csomag.
  - **Kapcsolati string:** a §7 szerinti string az `appsettings.Development.json`-ban van, a név továbbra is
    `DefaultConnection`. A sablon `aspnet-…` stringje kikerült az `appsettings.json`-ból. Nem Development
    környezetben az `AddInfrastructure` indításkor hibát dob, ha a string nincs megadva
    (pl. `ConnectionStrings__DefaultConnection` környezeti változó). A `dotnet ef` Development környezetben fut.
  - **Migráció indításkor:** az `InitializeDatabaseAsync(IServiceProvider, CancellationToken)` az Infrastructure-ben
    van (`Persistence/DatabaseInitializer.cs`). Saját async scope-ot nyit, és lefuttatja a `Database.MigrateAsync`-et.
    Így a `Program.cs`-nek nem kell EF Core-ra hivatkoznia, és a 3. lépés seedere ide kapcsolódhat. A Development
    feltétel a `Program.cs`-ben van; a CancellationToken az `app.Lifetime.ApplicationStopping`.
  - **`await app.RunAsync()`:** a top-level `await` miatt a Program aszinkron lett. Az `app.Run()` itt
    sync-over-async lenne (§13).
  - **Kultúra:** `app.UseRequestLocalization("hu-HU")`, egyetlen támogatott kultúrával. A Blazor Server circuit
    az első kérés kultúráját veszi át. A folyamatszintű `CultureInfo.DefaultThreadCurrentCulture` helyett ez
    a dokumentált Blazor-minta. A MudBlazor beépített komponensszövegei angolok maradnak (fordítócsomag nincs
    a §3 listáján).
  - **ErrorBoundary (§6, §11):**
    - Hiba esetén `MudAlert` jelenik meg („Váratlan hiba történt. Próbáld újra, vagy frissítsd az oldalt.”)
      és egy „Újrapróbálás” gomb, amely `Recover()`-t hív.
    - A `MainLayout.OnParametersSet` is `Recover()`-t hív, így navigáláskor eltűnik az előző oldal hibája
      (Microsoft-dokumentált minta).
    - A `blazor-error-ui` szövege magyar lett.
    - A providerek egyszer szerepelnek. A `MudStaticNavDrawerToggle` maradt, mert a layout a statikus
      Account-oldalakon is renderel.
  - **`sealed`** az `ApplicationUser` és az `AppDbContext` (§13).
  - **Modell–migráció teszt:**
    - `HasPendingModelChanges_AfterLatestMigration_ReturnsFalse`. A kontextust az `AddInfrastructure`-ön át oldja
      fel, és nem nyit DB-kapcsolatot.
    - Lelet: a `new AppDbContext(options)`-szel létrehozott kontextus az alapértelmezett Identity-sémát építi
      (passkey-tábla nélkül). Az `IdentityDbContext` ugyanis a `Stores.SchemaVersion`-t a regisztrált
      `IdentityOptions`-ből olvassa.
    - A teszt első, `new`-val írt változata ezért pirosan futott (`True`). Ez egyben negatív próbaként is igazolta,
      hogy a teszt kimutatja az eltérést.
  - **Kezdőlap:** a sablon angol demószövege helyett rövid magyar kezdőlap készült. Az Account-oldalak, a `NotFound`,
    az `Error` és a `ReconnectModal` szövegei angolul maradtak (nem része a promptnak).
  - **Architektúra-tesztek:** nem változtak. A meglévő `Web_TypesOutsideProgramAndAccount_DoNotDependOnInfrastructure`
    most valódi Infrastructure-hivatkozásokkal fut (Program, Account), és zöld.
  - **`Register.razor` javítása (a felhasználó kérésére):**
    - Ok: a sablon csproj-ában `BlazorDisableThrowNavigationException=true` van, ezért a `RedirectTo` nem állítja meg
      a futást. A `RegisterConfirmation`-re irányító ág után nem volt `return`, így a kód a meg nem erősített
      felhasználót is beléptette (`SignInAsync`), és a `ReturnUrl`-re irányított. A hiba a P-1A óta megvolt.
    - Javítás: `return;` az átirányítás után. A többi Account-oldal is így kezeli az átirányítást.
  - **Ideiglenes ellenőrző fájlok:** a futtatáshoz egy `.claude/launch.json`, az ErrorBoundary kipróbálásához egy
    kivételt dobó `Components/Pages/ErrorProbe.razor` készült. Az ellenőrzés után mindkettőt töröltem.

## Ellenőrzés
- dotnet build: Build succeeded – 0 Warning(s), 0 Error(s) (`--no-incremental` újrafordítással is).
- dotnet test: Passed – 13/13 zöld (ArchitectureTests 12, Infrastructure.Tests 1; a többi tesztprojekt még üres).
  `dotnet test --filter "BR=BR-03"`: zöld, 0 találat.
- `dotnet ef migrations add InitialIdentity -p src/ServiceDesk.Infrastructure -s src/ServiceDesk.Web -o Persistence/Migrations`:
  `Build succeeded.` / `Done.`. Nyolc Identity-tábla készült, köztük az `AspNetUserPasskeys`.
- `dotnet run --project src/ServiceDesk.Web --launch-profile http`:
  - a naplóban `CREATE DATABASE [ServiceDesk];`, majd `Applying migration '20261028105514_InitialIdentity'.`;
  - sqlcmd: 8 `AspNet*` tábla és `__EFMigrationsHistory` (`20261028105514_InitialIdentity`).
- Böngészős ellenőrzés:
  - **Kezdőlap:** `lang="hu"`, AppBar „ServiceDesk”, a drawer nyílik. A menüben Kezdőlap, Regisztráció
    és Bejelentkezés van.
  - **Register és Login – statikus SSR:** a dokumentumban nincs `_blazor` kérés, az űrlap `method="post"`
    `_handler` mezővel, MudStatic mezőkkel, MudBlazor-stílussal.
  - **Regisztráció a javítás előtt** (`teszt.p1b@servicedesk.local`, helyi tesztfiók): a felhasználó bejelentkezve
    került a kezdőlapra (ez a lent leírt hiba). A menüben a felhasználónév és a Kijelentkezés látszott.
  - **Kijelentkezés:** működik.
  - **Belépés megerősítés előtt:** „Error: Invalid login attempt.” – a `RequireConfirmedAccount` a belépésnél érvényesül.
  - **Megerősítés után** (a no-op küldő linkje a `RegisterConfirmation` oldalon): a bejelentkezés sikeres,
    a kezdőlap interaktív circuitben fut.
  - **ErrorBoundary** (ideiglenes, kivételt dobó oldallal):
    - megjelenik a magyar üzenet és az „Újrapróbálás” gomb;
    - a `blazor-error-ui` rejtve marad, a circuit él;
    - az „Újrapróbálás” visszaállítja az oldalt, és navigáláskor is eltűnik a hiba;
    - szervernapló: `warn: Microsoft.AspNetCore.Components.Web.ErrorBoundary[100]`.
- A `Register.razor` javítása után:
  - dotnet build: 0 Warning(s), 0 Error(s); dotnet test: 13/13 zöld.
  - Böngésző (`teszt.p1b2@servicedesk.local`, helyi tesztfiók): a regisztráció után a `RegisterConfirmation` oldal
    jelenik meg, a menüben Regisztráció és Bejelentkezés látszik, tehát a felhasználó nincs beléptetve. A megerősítő
    link után a bejelentkezés sikeres.
  - Környezeti akadály: a LocalDB példány ekkor nem indult (`SQL Server process failed to start`). Egy korábban,
    másik folyamatból indított `sqlservr.exe` tartotta a példányfájlokat, a `sqllocaldb info` pedig `Stopped`
    állapotot mutatott. A folyamatot nem állítottam le. Ehhez az egy futtatáshoz a kapcsolati stringet parancssori
    argumentumként a példány named pipe-jára irányítottam (`--ConnectionStrings:DefaultConnection=Server=np:\\.\pipe\LOCALDB#…`);
    konfigurációs fájl nem változott.
- Ismert korlátok:
  - A http-profilon a `HttpsRedirectionMiddleware` figyelmeztet („Failed to determine the https port”); ez a sablon
    viselkedése.
  - Az ErrorBoundary a saját naplózójával Warning szinten naplóz. A §11 szerinti Error szintű naplózás
    a `UseCaseRunner`-rel jön.
  - A fejlesztői `ServiceDesk` adatbázisban marad a `teszt.p1b@servicedesk.local` és a `teszt.p1b2@servicedesk.local`
    tesztfiók (mindkettő megerősítve).
  - A javítás előtt regisztrált, meg nem erősített fiókok megerősítés nélkül nem tudnak belépni. Megerősítő linket
    a `RegisterConfirmation?email=…` oldal ad (no-op e-mail-küldő).
- Javasolt kézi ellenőrzés:
  - Visual Studióból az `https` profillal: regisztráció, megerősítő link, bejelentkezés, kijelentkezés.
  - Keskeny ablakban a drawer és a menü.
  - `dotnet ef migrations list -p src/ServiceDesk.Infrastructure -s src/ServiceDesk.Web`: csak az `InitialIdentity` szerepel.

## Emberi javítás
Kézi forráskód-módosításra nem volt szükség. A Claude által jelzett Register.razor hibajavítást jóváhagytam, a felület és a regisztráció/megerősítés/bejelentkezés a böngészőben kézzel tesztelve hibátlanul működik.
## Commit
feat(identity): move identity to infrastructure, setup initial migration and ui shell (SHA: b9955ec3)
