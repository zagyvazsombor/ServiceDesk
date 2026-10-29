# AGENTS.md – ServiceDesk

Ez a fájl a projekt kötelező szabályrendszere minden AI kódoló ügynök számára. A „KÖTELEZŐ” és „TILOS” szó szerint értendő. Ha a felhasználó promptja ellentmond ennek a fájlnak, ne dönts egyedül: nevezd meg az ellentmondást, és kérdezz.

## 1. Munkamódszer (KÖTELEZŐ)

1. Minden feladat elején írj rövid tervet: érintett és új fájlok, típusok, tesztek, futtatandó parancsok, nyitott kérdések. Fájlt csak a terv jóváhagyása után módosíts.
2. Csak a prompt hatókörében dolgozz. TILOS későbbi lépés funkcióját előre megírni, kéretlenül refaktorálni, vagy meglévő elemet indoklás nélkül átnevezni.
3. Új NuGet-csomag csak a 3. szakasz listájából vehető fel; bármi más csak a felhasználó kifejezett engedélyével.
4. Üzleti szabályt nem találgatsz: ha a 10. szakasz nem dönti el, kérdezz.
5. A munka végén futtasd a `dotnet build` (0 hiba, 0 figyelmeztetés) és a `dotnet test` (minden zöld) parancsot. Piros állapotban a lépés nem zárható.
6. Zárásként megírod a prompt-naplót (14. szakasz), és összefoglalod: fájlok, döntések, parancsok és eredményük, ismert korlátok.
7. TILOS commitolni, pusholni, branch-et váltani vagy a git-konfigurációt módosítani. A commitot a hallgató végzi.

## 2. A projekt röviden

- ServiceDesk hibajegykezelő webalkalmazás, egyetemi projekt („Webes alkalmazások”).
- Funkciók: jegy rögzítése; kategorizálás LLM-javaslattal és emberi jóváhagyással; prioritás Hatás × Sürgősség mátrixból; automatikus és kézi kiosztás; státuszkezelés; törzsadat-kezelés.
- Két szerepkör: `Reporter` (Bejelentő) és `Admin` (ügyintéző és adminisztrátor egyben). Harmadik szerepkör nincs.
- Egyetlen alkalmazás: Blazor Web App, globális Interactive Server render móddal. Nincs REST API, nincs külön frontend.
- Kritikus szabály: lezárt jegy nem osztható ki újra, kiosztás csak aktív, megfelelő kategóriájú ügyintézőnek történhet (BR-03, BR-04).

## 3. Rögzített stack és engedélyezett csomagok

| Terület | Döntés |
|---|---|
| Runtime | .NET 10 (LTS), C# 14, `net10.0` |
| UI | Blazor Web App, globális Interactive Server, prerender nélkül; MudBlazor 9.x |
| Statikus Identity-oldalak | `Extensions.MudBlazor.StaticInput` (a MudBlazor sablon szerint) |
| Hitelesítés | ASP.NET Core Identity (Individual accounts), cookie |
| Adatbázis | SQL Server LocalDB (`(localdb)\MSSQLLocalDB`), Docker nélkül |
| ORM | EF Core 10, `Microsoft.EntityFrameworkCore.SqlServer`, code-first migrációk |
| Validáció | FluentValidation az Application rétegben |
| AI | `Microsoft.Extensions.AI` (`IChatClient`); alapértelmezés: lokális Ollama (`OllamaSharp`); alternatíva: OpenAI-kompatibilis végpont (`Microsoft.Extensions.AI.OpenAI`) |
| Idő | `TimeProvider`, tesztben `FakeTimeProvider` |
| Teszt | xUnit v3, Shouldly, NSubstitute, bUnit, Respawn, NetArchTest.Rules |
| Csomagverziók | Central Package Management (`Directory.Packages.props`) |

Engedélyezett csomagok (ezen kívül semmi):

- Web: `MudBlazor`, `Extensions.MudBlazor.StaticInput`, `Microsoft.AspNetCore.Diagnostics.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Tools`
- Application: `FluentValidation`, `FluentValidation.DependencyInjectionExtensions`, `Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.Extensions.Logging.Abstractions`
- Infrastructure: `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.Extensions.AI`, `OllamaSharp`, `Microsoft.Extensions.AI.OpenAI`, valamint `<FrameworkReference Include="Microsoft.AspNetCore.App" />`
- Tesztek: `xunit.v3`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, `Shouldly`, `NSubstitute`, `bunit`, `Respawn`, `NetArchTest.Rules`, `Microsoft.Extensions.TimeProvider.Testing`, `Microsoft.AspNetCore.Mvc.Testing`

Eszköz: `dotnet-ef` helyi eszközként (`.config/dotnet-tools.json`, `dotnet tool restore`).

## 4. Solution-struktúra és függőségek

```
ServiceDesk/
├── src/
│   ├── ServiceDesk.Domain/          entitások, enumok, domain service-ek, Result/Error
│   ├── ServiceDesk.Application/     use case-ek feature-mappákban, portok, DTO-k, validátorok
│   ├── ServiceDesk.Infrastructure/  AppDbContext, EF-konfigurációk, migrációk, repository-k, seeder, AI-adapter
│   └── ServiceDesk.Web/             Blazor Web App: komponensek, Identity UI, UseCaseRunner, Program.cs
├── tests/
│   ├── ServiceDesk.Domain.Tests/
│   ├── ServiceDesk.Application.Tests/
│   ├── ServiceDesk.Infrastructure.Tests/   LocalDB integrációs tesztek
│   ├── ServiceDesk.Web.Tests/              bUnit és HTTP-szintű tesztek
│   └── ServiceDesk.ArchitectureTests/
├── docs/
│   ├── adr/       ADR-<nnn>-<slug>.md
│   ├── prompts/   P-<lépés><betű>-<slug>.md
│   └── ai/        a futásidejű LLM-prompt verziói
├── AGENTS.md · CLAUDE.md · README.md
└── Directory.Build.props · Directory.Packages.props · .editorconfig · .config/dotnet-tools.json
```

Függőségi szabályok (KÖTELEZŐ, az architektúra-tesztek ellenőrzik):

- `Domain` semmilyen projektre és külső csomagra nem hivatkozik.
- `Application` → `Domain`. TILOS: EF Core, ASP.NET Core, MudBlazor, `Microsoft.Extensions.AI`, `Infrastructure`, `Web`.
- `Infrastructure` → `Application`, `Domain`.
- `Web` → `Application`, `Infrastructure`. Infrastructure-típust csak a `Program.cs` (DI-összerakás) és a sablon `Components/Account` mappája használhat (`ApplicationUser`).
- A `ServiceDesk.Web.Components.Pages` névtér nem függhet `ServiceDesk.Infrastructure`-tól és EF Core-tól.

## 5. Réteg-szabályok

### Domain

- Gazdag modell: privát setterek, állapotváltás csak metóduson át, érvényes létrehozás factory metódussal (pl. `Ticket.Create`). EF Core-hoz `private` paraméter nélküli konstruktor megengedett.
- Üzleti szabály megsértése `Result` / `Result<T>` + `Error` visszatéréssel jelez. Kivételt csak programozási hiba dob (pl. `null` argumentum).
- Nincs I/O és nincs időlekérdezés: az időpont `DateTimeOffset now`, a végrehajtó `string? actorUserId` paraméter (`null` = rendszer).
- Azonosítók: entitás `Guid` (`Guid.CreateVersion7()` a factoryban); felhasználó `string` (az Identity `IdentityUser.Id`-ja).
- `Result`, `Error`, `ErrorType` a `Domain/Common` mappában; hibakódok központi statikus osztályokban: `TicketErrors`, `AgentErrors`, `CategoryErrors`.

### Application

- Feature-mappák, pl. `Tickets/CreateTicket/{CreateTicketCommand, CreateTicketHandler, CreateTicketValidator}.cs`; lekérdezésnél `…Query`, `…Handler`, `…Dto`.
- Handler: `public sealed class XHandler` egyetlen `Task<Result<T>> HandleAsync(XCommand command, CancellationToken ct)` (vagy `Task<Result>`) metódussal. Mediator nincs. Az `AddApplication()` reflexióval scoped-ként regisztrál minden `*Handler` osztályt, és a validátorokat is.
- Handler-sorrend: (1) autorizáció (8. szakasz), (2) validáció, (3) betöltés, (4) domain-művelet, (5) `IUnitOfWork.SaveChangesAsync`, (6) DTO visszaadása.
- Portok: `ITicketRepository`, `IAgentRepository`, `ICategoryRepository`, `ICategorySuggestionRepository`, `IUnitOfWork`, `ITicketQueries`, `IUserQueries`, `ICategorySuggester`, `ISuggestionRateLimiter`, `ICurrentUser`.
- Alkalmazásszintű hibák: `AuthErrors`, `GeneralErrors`, `AiErrors`.
- Entitás nem hagyja el az Application réteget: a Web csak `record` DTO-t kap.
- A közös kiosztási logika `TicketAssignmentService` (Application-szolgáltatás, nem handler).
- Szerepkör-konstansok: `Application/Common/Security/Roles.cs` (`Reporter`, `Admin`).

### Infrastructure

- Egyetlen `AppDbContext : IdentityDbContext<ApplicationUser>` a `Persistence/` mappában. Identity- és domaintáblák egy adatbázisban, egy migrációs történettel (`Persistence/Migrations`).
- Konfiguráció `IEntityTypeConfiguration<T>` osztályokban (`Persistence/Configurations`); a Domainben nincs EF-attribútum.
- Aggregátumonként egy repository. Az `IUnitOfWork` az `AppDbContext.SaveChangesAsync`-ot hívja, és a `DbUpdateConcurrencyException`-t `GeneralErrors.ConcurrencyConflict` eredménnyé alakítja.
- Mappák: `Identity/ApplicationUser.cs`, `Persistence/Seed/DatabaseSeeder.cs`, `Ai/`.
- Belépési pont: `DependencyInjection.AddInfrastructure(IServiceCollection, IConfiguration)`.

### Web

- Oldal-komponens use case-t csak az `IUseCaseRunner`-en át hív. TILOS DbContextet, repositoryt vagy `UserManager`-t injektálni oldal-komponensbe (kivétel: a sablon `Components/Account` mappája).
- Komponensben nincs üzleti logika: csak megjelenítés, input-gyűjtés és a `Result` kezelése.
- `IUseCaseRunner`, `UseCaseRunner` és `CurrentUser : ICurrentUser` a `Web/UseCases/` mappában.

```csharp
public interface IUseCaseRunner
{
    Task<TResult> RunAsync<THandler, TResult>(
        Func<THandler, CancellationToken, Task<TResult>> useCase,
        CancellationToken ct = default) where THandler : notnull;
}

// Hívás egy komponensből:
// var result = await Runner.RunAsync(
//     (CreateTicketHandler h, CancellationToken ct) => h.HandleAsync(command, ct), _cts.Token);
```

A `UseCaseRunner` működése (KÖTELEZŐ):

1. Lekéri az aktuális `AuthenticationState`-et a circuit `AuthenticationStateProvider`-éből.
2. Új aszinkron DI-scope-ot nyit (`IServiceScopeFactory.CreateAsyncScope()`).
3. A scope `CurrentUser`-ét inicializálja a `ClaimsPrincipal`-lal.
4. A handlert a scope-ból oldja fel, és lefuttatja.
5. Váratlan kivételt naplóz, majd továbbdob; az `ErrorBoundary` kezeli.

Így egy use case = egy scope = egy DbContext.

## 6. Blazor (.NET 10, Interactive Server)

KÖTELEZŐ minták:

- A Web projekt a MudBlazor.Templates `mudblazor` sablonjából készül: `dotnet new mudblazor -o src/ServiceDesk.Web -int Server -au Individual -ai -uld`. A sablon szerkezetét (`Components/App.razor`, `Routes.razor`, `Layout/`, `Account/`, `ReconnectModal`, `NotFound`) megtartjuk; módosítani csak a dokumentált pontokon lehet.
- `Program.cs`:
  - `AddRazorComponents().AddInteractiveServerComponents(o => o.DetailedErrors = builder.Environment.IsDevelopment())`
  - `AddCascadingAuthenticationState()`, `UseAntiforgery()`, `MapStaticAssets()`
  - `MapRazorComponents<App>().AddInteractiveServerRenderMode()`, `MapAdditionalIdentityEndpoints()`
- Globális interaktivitás az `App.razor`-ban a sablon `PageRenderMode` logikájával (`HttpContext.AcceptsInteractiveRouting()`), azzal a kivétellel, hogy a státuszkód-újrafuttatott (`IStatusCodeReExecuteFeature`) kérések statikusan renderelődnek. Indok: ezekhez a válaszokhoz a framework nem ír megőrzött komponensállapotot, így a belőlük induló circuitnek nem volna antiforgery-tokenje (ADR-006). A hibakezelő oldalt a framework eleve statikusan rendereli; ezt teszt ellenőrzi. Interaktív esetben a visszaadott érték `new InteractiveServerRenderMode(prerender: false)` (statikus mezőben).
- A `Components/Account` oldalai `[ExcludeFromInteractiveRouting]` miatt statikus SSR-ben futnak; ezt nem változtatjuk meg.
- Oldalakra nem kerül `@rendermode` direktíva.
- Statikus fájlok `@Assets["…"]`-szel, pl. `@Assets["_framework/blazor.web.js"]`, `@Assets["_content/MudBlazor/MudBlazor.min.css"]`.
- A MudBlazor-providerek (`MudThemeProvider`, `MudPopoverProvider`, `MudDialogProvider`, `MudSnackbarProvider`) egyszer, a `MainLayout`-ban vannak; a `@Body` egy `ErrorBoundary`-n belül.
- Űrlapok:
  - Statikus SSR (Account) oldalon csak `MudStatic…` komponens, `EditForm` + `[SupplyParameterFromForm]` + `FormName`.
  - Interaktív oldalon `MudForm` vagy `EditForm` modell-bindinggal, `FormName` nélkül.
- Felhasználó interaktív komponensben: `[CascadingParameter] Task<AuthenticationState>` vagy `AuthenticationStateProvider`; use case-ben `ICurrentUser`.
- Életciklus:
  - Adatbetöltés `OnInitializedAsync` / `OnParametersSetAsync`-ben, a runneren át.
  - JS-interop csak `OnAfterRenderAsync(firstRender)`-ben.
  - Hosszú művelethez `CancellationTokenSource`, amelyet a komponens `Dispose`-ban megszakít.
- Oldalvédelem: `@attribute [Authorize]` vagy `[Authorize(Roles = Roles.Admin)]`; menüben `<AuthorizeView Roles="…">`. Ez csak UI-védelem (8. szakasz).
- Navigáció: `NavigationManager.NavigateTo`; `forceLoad: true` csak Identity-oldalra lépéskor és kijelentkezéskor.
- UI-szövegek magyarul, dátum- és számformátum `hu-HU` kultúrával.

TILOS (régi, .NET 6–7 korszakbeli vagy ütköző minták):

- `Pages/_Host.cshtml`, `_Layout.cshtml`, `MapFallbackToPage("/_Host")`, `Startup.cs`
- `AddServerSideBlazor()`, `MapBlazorHub()`, `blazor.server.js`, `<component type="typeof(App)" render-mode="ServerPrerendered" />`
- `<CascadingAuthenticationState>` komponens a router körül (helyette `AddCascadingAuthenticationState()`)
- Router az `App.razor`-ban: az `App.razor` a HTML-dokumentum, a router a `Routes.razor`-ban van
- `IHttpContextAccessor` vagy `HttpContext` interaktív komponensben
- Oldalankénti `@rendermode`; Interactive WebAssembly vagy Auto mód
- Saját `AuthenticationStateProvider`, saját jelszó- vagy cookie-kezelés
- Bootstrap vagy más UI-könyvtár a MudBlazor mellett
- `MarkupString` felhasználói vagy LLM-eredetű szövegre

## 7. Adatelérés (EF Core + LocalDB)

- Kapcsolati string (`appsettings.Development.json`): `Server=(localdb)\\MSSQLLocalDB;Database=ServiceDesk;Trusted_Connection=True;MultipleActiveResultSets=true`. A tesztek a `ServiceDesk_Tests` adatbázist használják.
- A DbContext scoped. Blazor Server alatt a `UseCaseRunner` ad neki use case-enkénti scope-ot. TILOS DbContextet komponensbe injektálni vagy circuit-szinten tartani.
- Konkurencia: a `Ticket` táblán `RowVersion` shadow property (`byte[]`, `IsRowVersion()`). Ütközéskor `GeneralErrors.ConcurrencyConflict` („A jegyet időközben módosították, frissítsd az oldalt.”).
- Enumok stringként (`HasConversion<string>()`, `HasMaxLength(30)`); minden string mezőn `HasMaxLength`, a 10. szakasz mezőkorlátaival egyezően.
- Indexek: `Category.Code` egyedi, `Agent.UserId` egyedi; további indexek: `Ticket.ReporterId`, `Ticket.AssigneeAgentId`, `Ticket.Status`.
- Felhasználó-hivatkozás FK-val (`Ticket.ReporterId`, `Agent.UserId` → `AspNetUsers.Id`), navigációs property nélkül, `DeleteBehavior.Restrict`-tel.
- Olvasás: `AsNoTracking()` + `Select` közvetlenül DTO-ba; N+1 lekérdezés TILOS. Íráskor az aggregátum teljes betöltése, `Include`-dal a gyerek-gyűjteményekre.
- Migráció: `dotnet ef migrations add <Név> -p src/ServiceDesk.Infrastructure -s src/ServiceDesk.Web -o Persistence/Migrations`. Kézi szerkesztés csak indoklással. `EnsureCreated()` TILOS.
- Indításkor, csak Development környezetben: `Database.MigrateAsync()`, majd az idempotens `DatabaseSeeder` (szerepkörök; felhasználók `UserManager`/`RoleManager`-rel; kategóriák; ügyintézői profilok; kompetenciák).

Seed-adatok (KÖTELEZŐ, a tesztek és a bemutató erre épül):

| Felhasználó | Szerepkör | Ügyintézői profil |
|---|---|---|
| `admin.anna@servicedesk.local` | Admin | Kiss Anna – HARDWARE, NETWORK, aktív |
| `admin.bela@servicedesk.local` | Admin | Nagy Béla – SOFTWARE, ACCESS, aktív |
| `admin.csaba@servicedesk.local` | Admin | Tóth Csaba – NETWORK, SOFTWARE, OTHER, aktív |
| `bejelento.dora@servicedesk.local` | Reporter | – |
| `bejelento.erik@servicedesk.local` | Reporter | – |

- Kategóriák (mind aktív, rövid magyar leírással; az LLM-prompt is ezt használja): `HARDWARE` Hardver, `SOFTWARE` Szoftver, `NETWORK` Hálózat, `ACCESS` Jogosultság és hozzáférés, `OTHER` Egyéb.
- Seed-jelszó: `Seed:DefaultPassword` az `appsettings.Development.json`-ban; meg kell felelnie a 8. szakasz jelszószabályának. Csak fejlesztői demó; élesben a seeder nem fut.

## 8. Biztonság és jogosultságkezelés

- Identity: a sablon mechanizmusát nem írjuk felül, csak konfiguráljuk.
  - `RequireConfirmedAccount = false` (nincs e-mail-infrastruktúra; ADR rögzíti).
  - Jelszó legalább 10 karakter; lockout 5 hibás próbálkozás után 15 percre.
  - A `Login.razor`-ban `PasswordSignInAsync(…, lockoutOnFailure: true)`.
- Regisztráció: a `Register.razor`-ban a sikeres `CreateAsync` után `AddToRoleAsync(user, Roles.Reporter)`. A szerepkör sosem jön a klienstől; `Admin` csak seedből jön létre.
- Háromszintű védelem, mindhárom KÖTELEZŐ:
  1. UI: `[Authorize]` / `[Authorize(Roles = …)]` az oldalakon, `<AuthorizeView>` a menüben. Ez csak kényelmi réteg.
  2. Use case: minden handler első lépése az `ICurrentUser` alapján ellenőrzi a bejelentkezést és a szerepkört; hiba esetén `AuthErrors.Forbidden`.
  3. Erőforrás: a `Reporter` csak saját jegyet érhet el (BR-02). A szűrés a lekérdezés része (`ReporterId == currentUser.UserId`), nem utólagos; idegen jegyre `TicketErrors.NotFound`.
- `ICurrentUser` (`UserId`, `IsAuthenticated`, `IsInRole`) a `UseCaseRunner`-ben töltődik a circuit hitelesítési állapotából. TILOS `IHttpContextAccessor`-ból olvasni.
- Over-posting ellen a command csak a szerepkörnek megengedett mezőket tartalmazza. Például a `CreateTicketCommand`-ban nincs `ReporterId`, `Status`, `AssigneeAgentId` és `Priority`.
- `app.UseAntiforgery()` marad a statikus SSR űrlapok miatt; az interaktív események a SignalR-kapcsolaton futnak.
- Az AI-javaslat use case felhasználónként korlátozott: `ISuggestionRateLimiter`, `PartitionedRateLimiter`, 5 kérés/perc. A belépést brute force ellen az Identity lockout védi.
- Titok (API-kulcs) csak User Secrets-ben vagy környezeti változóban lehet. TILOS `appsettings*.json`-ban vagy a repóban.
- Naplóba nem kerül jelszó, cookie, API-kulcs és teljes jegyleírás.
- HTTPS és HSTS a sablon szerint.

## 9. AI-integráció

- Port: `ICategorySuggester.SuggestAsync(CategorySuggestionInput input, CancellationToken ct)` → `Result<CategorySuggestionOutput>`.
  - Bemenet: cím, leírás, az aktív kategóriák (kód, név, leírás).
  - Kimenet: kategóriakód, `Confidence` (0–1), indoklás, modellnév, prompt-verzió.
- Adapter: `LlmCategorySuggester` az `Infrastructure/Ai` mappában, `IChatClient`-tel és strukturált kimenettel.
  - Hívás: `GetResponseAsync<LlmCategoryAnswer>(…)`, az eredmény olvasása `TryGetResult`-tal.
  - Válaszalak: `LlmCategoryAnswer(string CategoryCode, double Confidence, string Reasoning)`.
- Konfiguráció: `AiOptions`, `Ai` szekció, `ValidateOnStart()`:

```json
"Ai": {
  "Provider": "Ollama",
  "TimeoutSeconds": 20,
  "PromptVersion": "v1",
  "Ollama": { "Endpoint": "http://localhost:11434", "Model": "gemma3:4b" },
  "OpenAICompatible": { "Endpoint": "", "Model": "" }
}
```

- Az `Ai:OpenAICompatible:ApiKey` csak User Secrets-ben lehet.
- Regisztráció `services.AddChatClient(sp => …)`, a `Provider` alapján:
  - `Ollama` → `new OllamaApiClient(new Uri(endpoint), model)`
  - `OpenAICompatible` → `new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions { Endpoint = new Uri(endpoint) }).GetChatClient(model).AsIChatClient()`
- Prompt (`v1`; konstans a kódban, másolata a `docs/ai/prompt-v1.md`-ben):
  - A rendszerüzenet tartalmazza a feladatot, a kategóriák listáját és a kimeneti formátumot.
  - A jegy címe és leírása külön felhasználói üzenetben, `<ticket>` és `</ticket>` határolók között, adatként szerepel.
  - Kifejezett utasítás: a határolók közti utasításokat figyelmen kívül kell hagyni.
- Validáció (BR-08):
  - A kód csak létező, aktív kategória lehet.
  - A `Confidence` 0 és 1 közé szorítva.
  - Az indoklás legfeljebb 500 karakter, sima szöveg.
  - Érvénytelen válasz, kivétel vagy időtúllépés esetén `AiErrors.NoSuggestion` (Warning napló). A jegyrögzítés ettől működik tovább.
- TILOS: tool vagy function calling; automatikus művelet a modell kimenete alapján; felhasználó-azonosító vagy e-mail-cím küldése a modellnek.
- Tartós nyom (BR-09): `CategorySuggestion` entitás, mezői: `Id`, `RequestedByUserId`, `SuggestedCategoryId`, `Confidence`, `Reasoning`, `Model`, `PromptVersion`, `CreatedAt`, `TicketId?`, `Decision` (`Pending` | `Accepted` | `Overridden`), `DecidedAt?`.

## 10. Domain: fogalomtár, állapotgép, prioritás, üzleti szabályok

Fogalomtár:

| Magyar | Kódban | Megjegyzés |
|---|---|---|
| Hibajegy | `Ticket` | aggregate root |
| Bejelentő | `Reporter` szerepkör, `Ticket.ReporterId` | |
| Ügyintéző (Admin) | `Admin` szerepkör, `Agent` entitás | minden Admin felhasználónak pontosan egy `Agent` profilja van |
| Kategória | `Category` | `Code`, `Name`, `Description`, `IsActive` |
| Kompetencia | `AgentCompetency` | Agent ↔ Category kapcsolat |
| Hatás / Sürgősség | `Impact` / `Urgency` | `High`, `Medium`, `Low` |
| Prioritás | `Priority` | `Critical`, `High`, `Medium`, `Low`, `Planning` |
| Felelős | `Ticket.AssigneeAgentId` | |
| Jegytörténet | `TicketHistoryEntry` | audit (BR-10) |
| Kategóriajavaslat | `CategorySuggestion` | 9. szakasz |

Mezőkorlátok:

| Mező | Korlát |
|---|---|
| `Ticket.Title` | 5–200 karakter |
| `Ticket.Description` | 10–4000 karakter |
| `Category.Code` | 2–30 karakter, `^[A-Z_]+$` |
| `Category.Name` | 2–100 karakter |
| `Category.Description` | legfeljebb 500 karakter |
| `Agent.DisplayName` | 2–100 karakter |
| `TicketHistoryEntry` régi és új érték | legfeljebb 200 karakter |

Állapotgép (`TicketStatus`):

| Művelet | Átmenet | Ki végzi |
|---|---|---|
| Kiosztás | `New` → `Assigned`; átosztásnál az állapot nem változik | rendszer (BR-05) vagy Admin |
| Munka megkezdése | `Assigned` → `InProgress` | Admin |
| Megoldás | `InProgress` → `Resolved` | Admin |
| Újranyitás | `Resolved` → `InProgress` | Admin |
| Lezárás | `Resolved` → `Closed` | Admin |
| Felelős elvesztése | `Assigned` vagy `InProgress` → `New` | rendszer, csak BR-07 esetén |

A `Closed` terminális (BR-03). Minden más átmenet `TicketErrors.InvalidTransition`.

Prioritásmátrix (BR-11):

| Hatás \ Sürgősség | High | Medium | Low |
|---|---|---|---|
| High | Critical | High | Medium |
| Medium | High | Medium | Low |
| Low | Medium | Low | Planning |

Terhelési súly: Critical 5, High 4, Medium 3, Low 2, Planning 1.

Üzleti szabályok:

- **BR-01** Jegyet csak `Reporter` hozhat létre. Kötelező mezők: cím, leírás, hatás, sürgősség, aktív kategória. A `ReporterId` a létrehozó, és később nem változik.
- **BR-02** `Reporter` csak a saját jegyeit listázhatja és nyithatja meg; idegen jegyre a válasz `NotFound`. Az `Admin` minden jegyet lát.
- **BR-03** `Closed` jegy terminális: nem osztható ki, nem osztható át, és semmilyen adata nem módosítható.
- **BR-04** Kiosztás csak `IsActive` ügyintézőnek történhet, akinek a kompetenciái között szerepel a jegy kategóriája. Kategória nélküli jegy nem osztható ki.
- **BR-05** Automatikus kiosztás történik a jegy létrehozásakor és átkategorizáláskor.
  - A jogosult (BR-04) ügyintézők közül a legkisebb terhelésű kapja. Terhelés: a nyitott (`Assigned`, `InProgress`) jegyeinek prioritás-súlyösszege.
  - Egyezésnél az kapja, akinek a `LastAssignedAt`-je a legrégebbi (`null` számít a legrégebbinek); végül az `Agent.Id` dönt (determinisztikus sorrend).
- **BR-06** Ha nincs jogosult ügyintéző, a jegy `New` állapotban, felelős nélkül marad; ez nem hiba. Admin később kézzel kioszthatja, vagy újrafuttathatja az automatikus kiosztást.
- **BR-07** Átkategorizálást csak Admin végezhet, `New`, `Assigned` vagy `InProgress` állapotban.
  - Ha a jelenlegi felelős jogosult az új kategóriára, a kiosztás marad.
  - Ha nem jogosult, a jegy újra kiosztódik (BR-05).
  - Ha nincs jogosult ügyintéző, a felelős törlődik, és a jegy `New` állapotba kerül.
- **BR-08** Az LLM csak létező, aktív kategóriát javasolhat. Érvénytelen, hiányzó vagy késő válasz esetén nincs javaslat, és a jegy rögzítése ettől nem bukik el.
- **BR-09** Kategória csak emberi döntéssel kerül a jegyre: a Bejelentő a létrehozó űrlapon elfogadja vagy felülbírálja a javaslatot, illetve kézzel választ.
  - A javaslat, a döntés (`Accepted` vagy `Overridden`), a döntéshozó és az időpont mentésre kerül.
  - Admin később átkategorizálhat (BR-07).
- **BR-10** Létrehozás, kiosztás, átosztás, státuszváltás, átkategorizálás, valamint hatás- vagy sürgősség-módosítás esetén `TicketHistoryEntry` keletkezik: ki, mikor, mit, régi → új érték.
- **BR-11** A prioritás mindig a Hatás × Sürgősség mátrixból számított érték, közvetlenül nem állítható. A Bejelentő létrehozáskor megadja a hatást és a sürgősséget; Admin triázskor módosíthatja őket.
- **BR-12** Ügyintéző inaktiválása nem érinti a már kiosztott jegyeit; ezeket csak Admin oszthatja át kézzel. Inaktív ügyintéző új jegyet nem kaphat (BR-04).
- **BR-13** Kategória nem törölhető, csak inaktiválható. Inaktív kategória új jegyhez nem választható és nem javasolható; a meglévő jegyeken megmarad.

## 11. Hibakezelés és naplózás

- Hibatípus: `Error(string Code, string Message, ErrorType Type)`.
  - `ErrorType` értékei: `Validation`, `NotFound`, `Forbidden`, `Conflict`, `BusinessRule`, `Unavailable`.
  - A `Message` magyar, felhasználónak szóló szöveg; a `Code` formátuma `Ticket.ClosedCannotBeAssigned`.
- Validációs hibánál a kód mezőnként `Validation.<Property>` alakú, hogy a UI a mezőhöz tudja rendelni.
- Az `Error` opcionális, csak olvasható `FieldErrors` listát hordoz (mezőnév → üzenetek). Csak a `Validation` típusú hibák töltik ki (`Error.Validation(...)`, az Applicationben a `ValidatorExtensions.ValidateToResultAsync`), és minden hibás mezőt tartalmaz. A fő `Code` és `Message` az első hibáé (`Validation.<Property>`), így az egyetlen hibát olvasó hívók változatlanul működnek. Más hibatípusnál a lista üres.
- A UI a `Message`-et `ISnackbar`-ral, a validációs hibákat a mezőknél mutatja.
- Kivétel csak váratlan helyzetben keletkezik. A `UseCaseRunner` naplózza, a `MainLayout` `ErrorBoundary`-je általános üzenetet és „Újrapróbálás” gombot mutat.
- Strukturált naplózás `ILogger` message template-tel; a naplóban string-interpoláció TILOS.
- Naplószintek: Information – üzleti esemény; Warning – AI-hiba, konkurencia-ütközés, megtagadott hozzáférés; Error – váratlan kivétel.

## 12. Tesztelés

- Domain.Tests: minden BR-hez legalább egy pozitív és egy negatív teszt, `[Trait("BR", "BR-03")]` jelöléssel. Az állapotgép összes átmenetét táblázatos `[Theory]` fedi le.
- Application.Tests: handler-tesztek NSubstitute-tal. Minden handlerhez autorizációs teszt: nem bejelentkezett → `Forbidden`, rossz szerepkör → `Forbidden`, idegen jegy → `NotFound`.
- Infrastructure.Tests: LocalDB `ServiceDesk_Tests` adatbázis. Migráció egyszer fut (assembly fixture), tesztenként Respawn-reset; ebben a projektben a párhuzamosítás ki van kapcsolva.
- Web.Tests: bUnit MudBlazor-szolgáltatásokkal (`AddMudServices`, laza JS-interop); az `IUseCaseRunner` helyettesítve; valamint HTTP-szintű tesztek WebApplicationFactory-val a `Http/` mappában.
- ArchitectureTests: a 4. szakasz minden szabálya.
- Idő `FakeTimeProvider`-rel. Elnevezés: `Method_Scenario_ExpectedResult`; Arrange–Act–Assert szerkezet.

## 13. Kódolási konvenciók

- `Directory.Build.props`: `net10.0`, `Nullable` és `ImplicitUsings` bekapcsolva, `TreatWarningsAsErrors=true`, `EnforceCodeStyleInBuild=true`, `AnalysisLevel=latest`. A Domain és az Application projektben ezen felül `AnalysisMode=Recommended`.
- File-scoped namespace, fájlonként egy publikus típus, `sealed` alapértelmezés, primary constructor a DI-hoz, `record` a commandokhoz, query-khez és DTO-khoz.
- Async végig: minden publikus aszinkron metódus `Async` utótagú, és utolsó paramétere `CancellationToken ct`. `.Result`, `.Wait()` és `async void` TILOS.
- Azonosítók és kommentek angolul; UI-szöveg és hibaüzenet magyarul. A szabályt megvalósító kódsor mellett `// BR-xx` jelölés.
- Nincs `#region`, nincs kikommentezett kód, nincs nem használt `using`.

## 14. Prompt-napló

Minden prompt végén hozd létre a `docs/prompts/P-<azonosító>-<slug>.md` fájlt ezzel a sablonnal:

```markdown
# P-<azonosító> – <cím>
- Dátum: YYYY-MM-DD
- Eszköz és modell: <ügynök, modell>
- Cél és érintett szabályok: <BR-xx, AGENTS.md §x>
- Átadott kontextus: <fájlok>

## Prompt
<a felhasználó promptja szó szerint>

## Eredmény
- Létrehozott és módosított fájlok: …
- Döntések és indoklásuk: …

## Ellenőrzés
- dotnet build: …
- dotnet test: … (x/y zöld)
- Javasolt kézi ellenőrzés: …

## Emberi javítás
TODO – a hallgató tölti ki

## Commit
TODO – a hallgató tölti ki
```

Ha a lépés architekturális döntést hoz vagy változtat, írj ADR-t: `docs/adr/ADR-<nnn>-<slug>.md`, szakaszai: Kontextus, Döntés, Alternatívák, Következmények.

## 15. Definition of Done

- [ ] `dotnet build`: 0 hiba, 0 figyelmeztetés
- [ ] `dotnet test`: minden zöld; az új BR-ekhez tartozik teszt
- [ ] Az architektúra-tesztek zöldek
- [ ] Komponensben nincs üzleti logika; az autorizáció az Application rétegben is megtörténik
- [ ] Nincs tiltott minta (17. szakasz)
- [ ] Modellváltozás esetén a migráció elkészült és lefut
- [ ] UI-szövegek magyarul; a hibaüzenetek felhasználóbarátok
- [ ] A prompt-napló, és ha kell, az ADR elkészült

## 16. Parancsok

```bash
dotnet tool restore
dotnet build
dotnet test
dotnet test --filter "BR=BR-03"
dotnet run --project src/ServiceDesk.Web
dotnet ef migrations add <Nev> -p src/ServiceDesk.Infrastructure -s src/ServiceDesk.Web -o Persistence/Migrations
dotnet ef database update -p src/ServiceDesk.Infrastructure -s src/ServiceDesk.Web
sqllocaldb info MSSQLLocalDB
ollama pull gemma3:4b
```

## 17. Tiltólista (összesítés)

- Régi Blazor- és ASP.NET-minták (6. szakasz), `Startup.cs`
- REST API végpont vagy controller üzleti funkcióra (kivétel: a sablon Identity-végpontjai)
- MediatR, AutoMapper, Swashbuckle, Newtonsoft.Json, FluentAssertions, Moq, Scrutor, Docker-függőség
- Közvetlen `DateTime.Now` / `DateTime.UtcNow` (helyette `TimeProvider`), `.Result`, `.Wait()`, `async void`
- Publikus setter domain entitáson; EF Core, ASP.NET Core vagy MudBlazor hivatkozás a Domainben vagy az Applicationben
- DbContext, repository vagy `UserManager` oldal-komponensben; `IHttpContextAccessor` interaktív komponensben
- `MarkupString` felhasználói vagy LLM-eredetű szövegre; `FromSqlRaw` string-összefűzéssel
- Generikus repository; üzleti logika „Service” osztályokban a domain helyett
- Titok a repóban; `EnsureCreated()`
- LLM tool/function calling, vagy automatikus művelet a modell kimenete alapján
