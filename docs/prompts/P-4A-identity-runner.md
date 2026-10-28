# P-4A – Identity-beállítások, ICurrentUser, UseCaseRunner
- Dátum: 2026-10-28
- Eszköz és modell: Claude Code (asztali alkalmazás), Claude Opus 5.5 (`claude-opus-5-5`)
- Cél és érintett szabályok:
  - Identity-beállítások: e-mail-megerősítés nélkül, 10 karakteres jelszó, 5 próbálkozás / 15 perc zárolás.
  - Reporter szerepkör regisztrációkor.
  - `ICurrentUser`, `AddApplication` validátor-regisztrációval, `IUseCaseRunner`/`UseCaseRunner`/`CurrentUser`.
  - Szerepkörrel védett helyettesítő oldalak, szerepkör szerinti menü, AppBar a felhasználóval és kijelentkezéssel.
  - Szabályok: AGENTS.md §1, §3, §5 (Application, Web), §6, §8, §11, §12, §13, §14.
- Átadott kontextus:
  - `AGENTS.md`, `CLAUDE.md`, `docs/prompts/P-1B-identity-and-shell.md`, `docs/prompts/P-3A-persistence.md`,
    `docs/prompts/P-3B-integration-tests.md`, `docs/adr/ADR-004-rich-domain-model.md`.
  - Infrastructure és Application: `DependencyInjection.cs` (mindkettő), `DatabaseSeeder.cs`, `Roles.cs`,
    `AuthErrors.cs`.
  - Web: `Program.cs`, `App.razor`, `Routes.razor`, `_Imports.razor`, `MainLayout.razor`, `NavMenu.razor`,
    `Home.razor`, valamint a `Components/Account` oldalai (`Login`, `Register`, `ChangePassword`, `SetPassword`,
    `ResetPassword`, `RedirectToLogin`, `IdentityComponentsEndpointRouteBuilderExtensions`).
  - A tesztprojektek (`DatabaseFixture`, `IntegrationTest`, `DependencyRuleTests`), `Directory.Packages.props`,
    `Directory.Build.props`, `.editorconfig`.

## Prompt
[P-4A] Identity-beállítások, ICurrentUser, UseCaseRunner
Kontextus: AGENTS.md §5 (Application, Web), §6, §8. A 3. lépés elkészült.

Feladat:
1. Identity-beállítások az AddInfrastructure-ben a §8 szerint: RequireConfirmedAccount = false, jelszó legalább 10 karakter, lockout 5 próbálkozás után 15 percre.
2. Login.razor: PasswordSignInAsync(…, lockoutOnFailure: true). Register.razor: a sikeres CreateAsync után AddToRoleAsync(user, Roles.Reporter). Más Account-oldalt ne módosíts.
3. Application: ICurrentUser (UserId, IsAuthenticated, IsInRole); az AddApplication() reflexióval regisztrálja a *Handler osztályokat és a validátorokat.
4. Web/UseCases: IUseCaseRunner, UseCaseRunner és CurrentUser pontosan az AGENTS.md §5 Web szakasza szerint.
5. Útvonalak helyettesítő oldalakkal, [Authorize(Roles = …)] védelemmel: /tickets/new és /tickets/my (Reporter), /tickets/{id:guid} (bármely bejelentkezett), /admin/queue, /admin/categories, /admin/agents (Admin).
6. NavMenu szerepkör szerint, <AuthorizeView>-val. Bejelentő: Új jegy, Saját jegyeim. Admin: Jegysor, Kategóriák, Ügyintézők.
7. AppBar: a bejelentkezett felhasználó e-mail-címe és Kijelentkezés gomb a sablon logout-végpontjával.

Elfogadás:
- Új regisztráló Reporter szerepkört kap; 5 hibás jelszó után a fiók 15 percre zárolódik.
- Bejelentőként az Admin-oldalak URL-je közvetlenül beírva sem nyílik meg.
- A build és a tesztek zöldek.

Zárás: AGENTS.md §1 és §14, napló: P-4A-identity-runner.

Válasz a tervre:
Módosítanék

Jóváhagyás a terv után (kiegészítéssel):
Jóváhagyom a K1–K5 javaslataival, egy kiegészítéssel:
- Kivételt adok az Account-oldalak tiltása alól: a ChangePassword, SetPassword és ResetPassword oldalon az űrlap MinimumLength értéke 6 helyett 10 legyen. Ezeken az oldalakon más nem változhat. Rögzítsd a naplóban, és töröld az ismert korlátok közül.
- Az ismert korlátok közé vedd fel: a szerepkörök ma csak a Development-seederben jönnek létre; a P-9A-ban válnak külön, minden környezetben futó lépéssé.
Mehet a megvalósítás.

## Eredmény
- Létrehozott és módosított fájlok:
  - `Directory.Packages.props`: új csomagok.
    - „Application” csoport: `FluentValidation` és `FluentValidation.DependencyInjectionExtensions`, mindkettő 12.2.0.
    - „Tests” csoport: `bunit` 2.13.3 és `NSubstitute` 6.2.0.
    - Mind szerepel a §3 engedélyezett listáján.
  - `src/ServiceDesk.Infrastructure`:
    - `DependencyInjection.cs`: a §8 Identity-beállításai; a „later step” komment törölve.
    - `Persistence/Seed/DatabaseSeeder.cs`: csak az `EmailConfirmed` melletti komment szövege változott (K4).
  - `src/ServiceDesk.Application`:
    - `ServiceDesk.Application.csproj`: a két FluentValidation-hivatkozás.
    - `DependencyInjection.cs`: `AddValidatorsFromAssembly(…, ServiceLifetime.Scoped)`; a handler-reflexió változatlan.
    - új: `Common/Security/ICurrentUser.cs`.
  - `src/ServiceDesk.Web`:
    - új `UseCases/`: `IUseCaseRunner.cs`, `UseCaseRunner.cs`, `CurrentUser.cs`,
      `UseCaseServiceCollectionExtensions.cs` (`AddUseCaseRunner()`).
    - `Program.cs`: `AddUseCaseRunner()` hívás és a hozzá tartozó `using`.
    - `Components/Account/Pages/Login.razor`: `lockoutOnFailure: true`; a sablonkomment helyén §8-hivatkozás.
    - `Components/Account/Pages/Register.razor`:
      - `AddToRoleAsync(user, Roles.Reporter)` a sikeres `CreateAsync` után;
      - sikertelen hozzárendelésnél a felhasználó törlődik, és hibaüzenetet kap (K3);
      - `MinimumLength = 10` (K3).
    - `Components/Account/Pages/ResetPassword.razor`, `Manage/ChangePassword.razor`, `Manage/SetPassword.razor`:
      kizárólag a jelszómező `MinimumLength` értéke: 6 → 10 (a jóváhagyáskor adott kivétel alapján).
    - `Components/_Imports.razor`: `Microsoft.AspNetCore.Authorization` és `ServiceDesk.Application.Common.Security`.
    - `Components/Routes.razor`: a `NotAuthorized` ág bejelentkezett felhasználónál magyar üzenetet mutat (K1).
    - `Components/Layout/NavMenu.razor`: szerepkör szerinti menü; a felhasználói rész átkerült az AppBarba (K2).
    - új: `Components/Layout/UserMenu.razor`.
    - `Components/Layout/MainLayout.razor`: a funkció nélküli `MoreVert` gomb helyén a `<UserMenu />` (K2).
    - új helyettesítő oldalak:
      - `Components/Pages/Tickets/`: `NewTicket.razor`, `MyTickets.razor`, `TicketDetails.razor`;
      - `Components/Pages/Admin/`: `TicketQueue.razor`, `Categories.razor`, `Agents.razor`.
  - Tesztek:
    - `tests/ServiceDesk.Infrastructure.Tests/Identity/IdentityConfigurationTests.cs` (új, 5 teszt).
    - `tests/ServiceDesk.Application.Tests`:
      - `ServiceDesk.Application.Tests.csproj`: `Shouldly`;
      - új: `DependencyInjectionTests.cs` (2 teszt).
    - `tests/ServiceDesk.Web.Tests`:
      - `ServiceDesk.Web.Tests.csproj`: `bunit`, `NSubstitute`, `Shouldly`;
      - új: `TestUsers.cs`;
      - új: `UseCases/CurrentUserTests.cs` (3), `UseCases/UseCaseRunnerTests.cs` (5), `UseCases/RecordingLogger.cs`;
      - új: `Layout/NavMenuTests.cs` (3), `Layout/UserMenuTests.cs` (2);
      - új: `Pages/PageAuthorizationTests.cs` (6 sor).
  - `docs/adr/ADR-005-identity-without-email-confirmation.md` (új) és ez a napló.
  - Migráció nem kellett, mert a modell nem változott. Az architektúra-tesztek nem változtak.
- Döntések és indoklásuk:
  - **A felhasználó döntései (a terv jóváhagyásakor):**
    - **K1 – jogosulatlan, de bejelentkezett felhasználó.**
      - Ha egy bejelentkezett felhasználó circuiten belül (linkről, `NavigationManager`-rel) navigál olyan oldalra,
        amelyhez nincs szerepköre, a `Routes.razor` magyar MudAlertet mutat: „Nincs jogosultságod az oldal
        megtekintéséhez.” A login oldal nem jelenik meg.
      - A névtelen felhasználó továbbra is a `RedirectToLogin`-ra kerül.
      - Közvetlenül beírt URL-nél a kérés még a Blazor előtt az ASP.NET Core authorization middleware-be fut
        (az oldal `[Authorize]` metaadata alapján). Ez a cookie-séma `Account/AccessDenied` oldalára irányít át,
        lásd Ellenőrzés.
    - **K2 – a felhasználói rész az AppBarban.**
      - Az e-mail-cím (link az `Account/Manage` oldalra) és a Kijelentkezés gomb egy új `UserMenu.razor`
        komponensbe került. Ezt a `MainLayout` az AppBarban, a funkció nélküli `MoreVert` gomb helyén jeleníti meg.
      - A NavMenuből ugyanezek kikerültek, így nincs duplikáció. A NavMenu `NavigationManager`- és
        `IDisposable`-kódja a `UserMenu`-be költözött.
      - A kijelentkezés a sablon `POST Account/Logout` végpontját használja `AntiforgeryToken`-nel.
        A `ReturnUrl` a sablon szerint az aktuális oldal.
      - Az e-mail a `ClaimTypes.Email` claimből jön. Az Identity claims-factory ezt kiadja; tartalék az
        `Identity.Name`, amely a regisztrációnál és a seednél szintén az e-mail-cím.
    - **K3 – Register.razor.**
      - Ha az `AddToRoleAsync` hibázik (például hiányzik a szerepkör), a frissen létrehozott felhasználó törlődik,
        és az űrlap a hibát mutatja. Így nem marad szerepkör nélküli fiók.
      - A szerepkör a `SignInAsync` előtt kerül fel, ezért a cookie már a szerepkörrel jön létre.
      - Az űrlap `MinimumLength` értéke 6 helyett 10.
    - **Kivétel az Account-tiltás alól (kiegészítés):** a `ChangePassword`, `SetPassword` és `ResetPassword`
      oldalon csak a jelszómező `StringLength` attribútumának `MinimumLength` értéke változott 6-ról 10-re.
      A `LoginWith2fa` és az `EnableAuthenticator` 6–7 jegyű kódmezői nem jelszómezők, változatlanok.
      Az ismert korlát ezzel megszűnt.
    - **K4 – seeder-komment:** a „the sign-in requires a confirmed account” állítás a módosítás után már nem igaz.
      Csak a komment szövege változott, a kód nem.
    - **K5 – kézi ellenőrzés a fejlesztői adatbázison:** lásd Ellenőrzés.
  - **Identity-beállítások:**
    - Beállított értékek: `RequireConfirmedAccount = false`, `RequiredLength = 10`, `MaxFailedAccessAttempts = 5`,
      `DefaultLockoutTimeSpan = 15 perc`; az `AllowedForNewUsers = true` explicit (ez az alapérték is).
    - A többi jelszószabály a sablon alapértéke marad. A seed-jelszó és a teszt-seed jelszó ezeknek megfelel.
    - Az ADR-005 rögzíti, mert a §8 kifejezetten ADR-t ír elő a megerősítés kikapcsolásához.
  - **`ICurrentUser`** az `Application/Common/Security` mappában, a `Roles` mellett van.
  - **`AddApplication`:** a validátorokat a FluentValidation `AddValidatorsFromAssembly` regisztrálja, assembly-
    szkenneléssel (reflexió), scoped élettartammal. Így a validátor ugyanabban a use case-scope-ban él, mint a
    handler.
  - **`CurrentUser`:**
    - A `UserId` csak hitelesített identitásnál kap értéket (`ClaimTypes.NameIdentifier`, az Identity alapértelmezett
      user-Id claimje).
    - Az `IsAuthenticated` ebből származik (`UserId is not null`). Így a handler nem kaphat „hitelesített, de
      azonosító nélküli” felhasználót.
    - Az `Initialize` publikus, mert a tesztek `InternalsVisibleTo` nélkül hívják (P-3B, K1).
  - **`UseCaseRunner`:**
    - A §5 öt lépését követi. A circuit-scope-ban regisztrált runner a circuit `AuthenticationStateProvider`-ét kapja.
    - A use case-enként nyitott `AsyncServiceScope`-ból oldja fel a `CurrentUser`-t (inicializálja) és a handlert.
    - Váratlan kivételnél Error-szintű strukturált naplóbejegyzést ír (`{Handler}` sablonparaméter), majd továbbdob.
      A feloldási hibák (például hiányzó regisztráció) is ide futnak.
    - Kivétel: ha a hívó a saját tokenjével megszakítja a futást (`OperationCanceledException` és
      `ct.IsCancellationRequested`), nem ír Error-naplót, mert ez a komponens `Dispose`-ánál várt helyzet;
      továbbdobja.
  - **`AddUseCaseRunner()`:**
    - A regisztráció egy bővítő metódusban van, hogy a tesztek ugyanazt használják, mint a `Program.cs`.
    - A `CurrentUser` scoped, és az `ICurrentUser` ugyanarra a példányra mutat.
    - Az `IUseCaseRunner` scoped, így Blazor Server alatt circuitenként jön létre.
  - **Helyettesítő oldalak:**
    - Mindegyik `PageTitle`-t, címsort és tájékoztató MudAlertet tartalmaz, logika és runner-hívás nélkül.
    - A `TicketDetails` csak az útvonal `Guid` paraméterét mutatja.
    - Az útvonalsablon `{Id:guid}`, mert a paraméter az `Id` property (az útvonalparaméter neve nem
      kis- és nagybetű-érzékeny, így ez a promptbeli `{id:guid}`-dal egyenértékű).
    - A `/tickets/{Id:guid}` csak `[Authorize]`-t kap. A BR-02 erőforrásszintű ellenőrzése a későbbi use case dolga.
  - **NavMenu:** három `AuthorizeView`:
    - `Roles="@Roles.Reporter"`: Új jegy, Saját jegyeim;
    - `Roles="@Roles.Admin"`: Jegysor, Kategóriák, Ügyintézők;
    - `NotAuthorized`: Regisztráció, Bejelentkezés.
  - **Tesztek:**
    - Az `IdentityConfigurationTests` a valódi `UserManager`-t és `SignInManager`-t a fixture DI-konténeréből kéri
      (ugyanaz az `AddInfrastructure`). A `SignInManager` feloldásához nem kellett további regisztráció, így a
      tervezett `UserManager.AccessFailedAsync` tartalékra nem volt szükség.
    - A zárolást a `CheckPasswordSignInAsync(…, lockoutOnFailure: true)` teszteli. Ez a `PasswordSignInAsync`
      zárolási része, `HttpContext` nélkül.
    - A `LockoutEnd` értékét a `TimeProvider.System` két mérése közé eső +15 perces sávban ellenőrzi.
    - Az Application `DependencyInjectionTests` konvenciótesztek: az Application assembly minden `*Handler`
      osztályára és `IValidator<T>` implementációjára ellenőrzik a scoped regisztrációt. Jelenleg sem handler,
      sem validátor nincs, ezért ma üresen teljesülnek. Az első use case-től kezdve védenek.
    - A Web-tesztek bUnit `BunitContext`-tel futnak (`AddMudServices`, laza JS-interop, `AddAuthorization`).
    - A `UseCaseRunnerTests` a runnert egy „circuit-scope”-ból oldja fel, ugyanazzal az `AddUseCaseRunner()`-rel.
      Az `AuthenticationStateProvider` NSubstitute-helyettesítő.
    - A naplózást egy kis `RecordingLogger<T>` rögzíti, mert a `Microsoft.Extensions.Diagnostics.Testing` nincs
      az engedélyezett listán.
    - A `PageAuthorizationTests` reflexióval ellenőrzi az oldalak `[Route]` és `[Authorize]` attribútumát.
      A tényleges kikényszerítést az `AuthorizeRouteView` és a middleware végzi; ezt a kézi ellenőrzés mutatta meg.
  - **Ideiglenes indítókonfiguráció:**
    - A böngészős ellenőrzéshez `.claude/launch.json` készült (`dotnet run --launch-profile http`, 5123-as port).
    - Az ellenőrzés után törölve lett, a munkafába nem került.

## Ellenőrzés
- dotnet build: Build succeeded – 0 Warning(s), 0 Error(s) (`--no-incremental` újrafordítással is).
- dotnet test: Passed – 263/263 zöld.
  - Projektenként: Domain.Tests 213, ArchitectureTests 12, Infrastructure.Tests 17 (+5),
    Application.Tests 2 (+2), Web.Tests 19 (+19).
  - Kiinduló állapot: 237/237. A teljes futás háromszor, az Infrastructure.Tests egymás után többször is zöld volt.
- Kézi ellenőrzés a beépített böngészőben (`http://localhost:5123`, Development, fejlesztői `ServiceDesk` adatbázis):
  - A böngészőben egy korábbi munkamenetből `bejelento.dora` volt bejelentkezve.
    - Az AppBar az e-mail-címet és a Kijelentkezés gombot mutatta. A menüben csak az Új jegy és a Saját jegyeim
      látszott.
    - A `/tickets/my` megnyílt.
  - Bejelentőként a közvetlenül beírt `/admin/queue` és `/admin/agents` URL nem nyílt meg. Átirányítás történt az
    `Account/AccessDenied?ReturnUrl=…` oldalra („You do not have access to this resource.”).
  - Circuiten belüli navigációnál (`Blazor.navigateTo('/admin/categories')`) a magyar „Nincs jogosultságod az oldal
    megtekintéséhez.” üzenet jelent meg (K1).
  - Az AppBar Kijelentkezés gombja kijelentkeztetett: a menüben újra a Regisztráció és a Bejelentkezés látszott.
  - Regisztráció helyi tesztfiókkal (`teszt.p4a@servicedesk.local`):
    - 9 karakteres jelszóval az űrlap „The Password must be at least 10 …” hibát adott.
    - Érvényes jelszóval a felhasználó azonnal, megerősítés nélkül bejelentkezett, és a menüben a Bejelentő
      linkjei jelentek meg.
    - Az adatbázisban a fiók `Reporter` szerepkört kapott, és `EmailConfirmed = 0`.
  - Zárolás ugyanezzel a fiókkal:
    - 4 hibás jelszó után „Error: Invalid login attempt.”, `AccessFailedCount = 4`, `LockoutEnd = NULL`.
    - Az 5. hibás jelszó után átirányítás az `Account/Lockout` oldalra; a `LockoutEnd` kb. 15 perccel későbbi
      (9 másodperccel később mérve 891 s volt hátra).
    - A helyes jelszó is a Lockout oldalra vitt.
  - `admin.anna` (seed):
    - a menüben csak a Jegysor, a Kategóriák és az Ügyintézők látszott;
    - az `/admin/queue` megnyílt;
    - a `/tickets/new` az `Account/AccessDenied` oldalra irányított.
  - Szervernapló: hiba nincs. Warning: a két „User account locked out.” bejegyzés (várt), valamint a http-profil
    miatti „Failed to determine the https port for redirect.”.
- Javasolt kézi ellenőrzés:
  - Az AppBar megjelenése keskeny ablakban (hosszú e-mail-cím és a Kijelentkezés gomb egymás mellett).
  - Regisztráció és belépés a https-profillal (`https://localhost:7133`).
  - A `ChangePassword` oldalon 9 karakteres új jelszó (az űrlap a 10 karakteres szabályt mutatja).
- Ismert korlátok:
  - A szerepkörök ma csak a Development-seederben jönnek létre. A P-9A-ban válnak külön, minden környezetben futó
    lépéssé. Addig más környezetben a regisztráció a hiányzó `Reporter` szerepkör miatt hibát ad (K3: a felhasználó
    ilyenkor nem jön létre).
  - Közvetlen URL-nél a jogosulatlan bejelentkezett felhasználó a sablon angol `AccessDenied` oldalát látja, a
    magyar üzenet csak circuiten belüli navigációnál jelenik meg. Az Account-oldalak a P-1B döntése szerint angolok.
  - Az `ExternalLogin.razor` szerepkör nélkül hozna létre felhasználót. Külső bejelentkezési szolgáltató nincs
    beállítva, így ez az ág most nem érhető el; az oldalt a prompt nem engedte módosítani.
  - A fejlesztői adatbázisban a P-1B két tesztfiókja (`teszt.p1b`, `teszt.p1b2`) szerepkör nélküli, mert a
    szerepkör-hozzárendelés előtt jöttek létre. Nem módosítottam őket. A `teszt.p4a` tesztfiók megmaradt, és a
    teszt után 15 percig zárolva volt.
  - Az Application `DependencyInjectionTests` ma üresen teljesül (nincs még handler és validátor).

## Emberi javítás
Kézi kódmódosításra nem volt szükség. A build és a tesztek zöldek.
## Commit
feat(identity): configure identity settings, icurrentuser, usecaserunner and role-based navigation
