# P-1A – Solution-váz
- Dátum: 2026-10-28
- Eszköz és modell: Claude Code (asztali alkalmazás), Claude Opus 5.5 (`claude-opus-5-5`)
- Cél és érintett szabályok: solution- és projektváz, build- és csomagkonfiguráció, architektúra-tesztek;
  AGENTS.md §1, §3, §4, §6, §12, §13, §14
- Átadott kontextus: `AGENTS.md`, `CLAUDE.md`

## Prompt
[P-1A] Solution-váz
Kontextus: az AGENTS.md kötelező, különösen a §3, §4, §6 és §13. Ez az 1/9. lépés első promptja.

Feladat:
1. Hozd létre a ServiceDesk solutiont a repó gyökerében, a src/ és tests/ projektekkel az AGENTS.md §4 szerint.
2. Web projekt: dotnet new mudblazor -o src/ServiceDesk.Web -int Server -au Individual -ai -uld
3. Domain, Application, Infrastructure: classlib. Tesztprojektek: dotnet new xunit3.
4. Projektreferenciák pontosan a §4 függőségi szabályai szerint.
5. Directory.Build.props és .editorconfig a §13 szerint. Central Package Management: minden verzió a Directory.Packages.props-ba kerül, a csproj-okban nem marad Version attribútum.
6. dotnet-ef helyi eszközként (.config/dotnet-tools.json).
7. Töröld a sablon mintaoldalait (Counter, Weather) és a menüpontjaikat.
8. ArchitectureTests: NetArchTest.Rules-szal teszteld a §4 összes függőségi szabályát.
9. Hozd létre a docs/adr, docs/prompts, docs/ai mappákat, és írd meg az ADR-001 (Blazor Web App, Interactive Server, MudBlazor, egy alkalmazás), ADR-002 (Clean Architecture négy projekttel) és ADR-003 (SQL Server LocalDB, Docker nélkül) fájlt.

Ha a sablonkód figyelmeztetést ad, ne nyomd el globálisan: javítsd, vagy célzott, indokolt szabállyal kezeld, és írd a naplóba.
Ne csináld: Identity áthelyezése, domain kód, új oldalak – ezek későbbi promptok.

Elfogadás:
- dotnet build: 0 hiba, 0 figyelmeztetés; dotnet test: zöld.
- A dotnet run --project src/ServiceDesk.Web elindul, a főoldal MudBlazor-layouttal jelenik meg.

Zárás: AGENTS.md §1 és §14, napló: P-1A-solution-skeleton.

## Eredmény
- Létrehozott és módosított fájlok:
  - Gyökér: `ServiceDesk.slnx` (`/src/`, `/tests/` solution-mappák), `Directory.Build.props`,
    `Directory.Packages.props`, `.editorconfig`, `.gitignore` (`dotnet new gitignore`), `global.json`,
    `.config/dotnet-tools.json` (dotnet-ef 10.0.14).
  - `src/ServiceDesk.Domain`, `src/ServiceDesk.Application`, `src/ServiceDesk.Infrastructure`: csak csproj
    (a sablon `Class1.cs`-e törölve, domain kód nincs). A Domain és az Application csproj-ban
    `AnalysisMode=Recommended`.
  - `src/ServiceDesk.Web`: a `dotnet new mudblazor -o src/ServiceDesk.Web -int Server -au Individual -ai -uld`
    kimenete, a következő módosításokkal:
    - törölve: `Components/Pages/Counter.razor`, `Components/Pages/Weather.razor` és a két menüpont a `NavMenu.razor`-ban;
    - `ServiceDesk.Web.csproj`: Version attribútumok nélkül, a props-ban már szereplő tulajdonságok nélkül,
      projektreferenciákkal;
    - `Components/App.razor`: statikus `InteractiveServerWithoutPrerender = new InteractiveServerRenderMode(prerender: false)`
      mező (§6);
    - `Program.cs`: `AddInteractiveServerComponents(options => options.DetailedErrors = builder.Environment.IsDevelopment())` (§6).
  - `tests/ServiceDesk.{Domain,Application,Infrastructure,Web}.Tests`: `dotnet new xunit3 -f net10.0`.
    A `UnitTest1.cs` törölve, a csproj CPM-re és `xunit.v3`-ra állítva, projektreferenciával.
  - `tests/ServiceDesk.ArchitectureTests`: `DependencyRuleTests.cs`, `SolutionAssemblies.cs`,
    `TestResultAssertions.cs` (NetArchTest.Rules + Shouldly).
  - `docs/adr/ADR-001-blazor-web-app-interactive-server.md`, `docs/adr/ADR-002-clean-architecture.md`,
    `docs/adr/ADR-003-sql-server-localdb.md`, `docs/ai/.gitkeep`, ez a napló.
- Projektreferenciák (§4): Application → Domain; Infrastructure → Application, Domain;
  Web → Application, Infrastructure. Tesztek: X.Tests → X; ArchitectureTests → mind a négy forrásprojekt.
- Döntések és indoklásuk:
  - **Tesztfuttató: Microsoft Testing Platform (MTP), csak `xunit.v3`** (a felhasználó döntése). A xunit.v3 4.x
    MTP v2-re épül. .NET 10 SDK-n a VSTest-módú `dotnet test` hibával leáll
    („Testing with VSTest target is no longer supported…”). A VSTest-hez a listán nem szereplő
    `xunit.v3.mtp-off` csomag kellett volna. Ezért a `global.json` `test.runner = Microsoft.Testing.Platform`
    (ezt a sablon is létrehozza), és a `Microsoft.NET.Test.Sdk` meg a `xunit.runner.visualstudio` nem kell.
    A `dotnet test --filter "BR=BR-03"` MTP alatt is működik (kipróbálva).
  - **`--ignore-exit-code 8` a teszt-projektekben** (`Directory.Build.props`, csak a `*Tests` projektekre).
    Az MTP 8-as kóddal (piros) zár, ha egy projektben nem fut teszt. Ez most a még üres tesztprojekteket,
    később pedig minden szűrt futást érintene (`--filter "BR=BR-03"` esetén a BR-teszt nélküli projekteket).
    Ez a VSTest korábbi viselkedését adja vissza. Ára, hogy egy véletlenül üressé vált tesztprojekt nem bukik el.
  - **NU1902/NU1903 – tranzitív pin** (a felhasználó döntése). A `Microsoft.EntityFrameworkCore.SqlServer`
    → `Microsoft.Data.SqlClient` 6.1.7 a sérülékeny `System.Security.Cryptography.Pkcs` 9.0.11-et húzta be
    (GHSA-2fmv-rv2c-59x7, GHSA-846f-f764-76mx). `TreatWarningsAsErrors` mellett ez restore-hiba.
    Megoldás: `CentralPackageTransitivePinningEnabled=true` és
    `PackageVersion System.Security.Cryptography.Pkcs 10.0.14`. Ez nem új közvetlen hivatkozás, csak a meglévő
    tranzitív függőség javított verzióra emelése. Ellenőrizve: a feloldott verzió 10.0.14, NU-kód nincs.
  - **§6 igazítások** (a felhasználó döntése): csak a `prerender: false` és a `DetailedErrors`. Az
    `ErrorBoundary` (§11), a magyar UI-szövegek és a hu-HU kultúra későbbi lépés.
  - **Sablon-figyelmeztetések kezelése.** Szigorú beállítással a sablon a `Data/Migrations/…CreateIdentitySchema.cs`-re
    IDE0005-öt és IDE0161-et adott. Az EF-migráció generált kód, de nincs `<auto-generated>` fejléce. Célzott
    szabály: `.editorconfig` → `[**/Migrations/*.cs] generated_code = true`. Ez a későbbi Infrastructure-migrációkra
    is érvényes. Globális elnyomás nincs.
  - **`GenerateDocumentationFile=true` + `NoWarn CS1591`.** Az IDE0005 („nincs nem használt using”, §13) csak
    akkor jelez build közben, ha készül XML-dokumentáció. A CS1591 (hiányzó XML-komment) elnyomása ennek
    a mellékhatásnak a célzott kezelése: XML-komment nem követelmény.
  - **`.editorconfig` (§13):** file-scoped namespace (IDE0161), nem használt using (IDE0005) és a using
    a namespace-en kívül (IDE0065) figyelmeztetésként. Ezen felül `CA1852` (sealed) figyelmeztetésként,
    a primary constructor javaslatként.
  - **Csomagverziók** (`Directory.Packages.props`): a sablon `10.*` / `9.*` / `4.*` lebegő verzióit a feloldott
    pontos verziókra rögzítettem: MudBlazor 9.11.0, Extensions.MudBlazor.StaticInput 4.1.0, EF Core és
    Identity 10.0.14. Tesztek: xunit.v3 4.0.2, NetArchTest.Rules 1.3.2, Shouldly 4.3.0. Csak a ténylegesen
    használt csomagok szerepelnek.
  - **Átmeneti eltérés a §3-tól:** a `Microsoft.AspNetCore.Identity.EntityFrameworkCore` és a
    `Microsoft.EntityFrameworkCore.SqlServer` most a Web projektben van, mert a sablon Identity-kódja
    (`Data/`) ott maradt. Az Identity áthelyezése a prompt szerint későbbi lépés.
  - **Architektúra-tesztek (12 eset):**
    - Domain: nem függ a többi projekttől (NetArchTest). Csak a .NET megosztott keretrendszer assemblyjeire
      hivatkozik (assembly-referencia ellenőrzés). A NetArchTest `OnlyHaveDependenciesOn` szabálya
      a fordító által generált típusokat (`<PrivateImplementationDetails>`, `<>z__ReadOnlyArray`1`) is
      szabálysértésnek veszi (scratchpadben kipróbálva), ezért a „nincs külső csomag” szabályt az assembly
      hivatkozásain ellenőrzöm.
    - Application: nincs EF Core, ASP.NET Core, MudBlazor, Microsoft.Extensions.AI, Infrastructure és Web függőség
      (`[Theory]`).
    - Infrastructure: nem függ a Webtől.
    - Web: Infrastructure-típust csak a `Program` és a `Components/Account` használhat. A sablon
      `IdentityComponentsEndpointRouteBuilderExtensions` osztálya az Account mappában van, de
      `Microsoft.AspNetCore.Routing` névtérben, ezért név szerint kivétel.
    - `ServiceDesk.Web.Components.Pages`: nem függ az Infrastructure-tól és az EF Core-tól.
    - A szűrőket a lefordított Web assemblyn negatív próbával ellenőriztem. A Pages-szűrő az Auth, Error, Home
      és NotFound típust választja ki, és kimutatja a MudBlazor-függést. A lambdákban és async
      állapotgépekben lévő függés a szülő típusnál jelenik meg.
  - A .NET 10 `dotnet new tool-manifest` a gyökérbe tette a `dotnet-tools.json`-t. A §3/§4 szerint áthelyeztem
    a `.config/` mappába (a `dotnet tool restore` megtalálja).
  - Az `Auth.razor` mintaoldal megmaradt, mert a prompt csak a Counter és a Weather törlését kérte.

## Ellenőrzés
- dotnet build: Build succeeded – 0 Warning(s), 0 Error(s) (`--no-incremental` újrafordítással is).
- dotnet test: Passed – 12/12 zöld (ArchitectureTests 12; a másik négy tesztprojekt még üres).
  `dotnet test --filter "BR=BR-03"`: zöld, 0 találat.
- `dotnet tool restore`: dotnet-ef 10.0.14 visszaállítva.
- `dotnet run --project src/ServiceDesk.Web`: elindul (`Now listening on: http://localhost:5123`). A főoldal
  MudBlazor-layouttal (AppBar, drawer, MudText/MudAlert) jelent meg. A menüben csak Home, Auth Required,
  Register és Login van. A böngészőkonzolban nincs hiba. A prerender ki van kapcsolva, ezért a megjelenő tartalom
  azt is igazolja, hogy az interaktív circuit felépült.
- Javasolt kézi ellenőrzés:
  - Visual Studióban a Test Explorer felismeri-e a tesztprojekteket (MTP-mód).
  - Széles ablakban a drawer és a menü megjelenése.
  - Az `Auth Required` menüpont a Login oldalra irányít-e.
  - Ismert korlát: a NetArchTest a nem használt projekt- vagy csomaghivatkozást nem látja (nem kerül
    a lefordított kódba), és az attribútumban lévő `typeof(...)` argumentumot sem számolja függésnek.
    Ezeket a csproj-ok review-ja fedi le.

## Emberi javítás
TODO – a hallgató tölti ki

## Commit
TODO – a hallgató tölti ki
