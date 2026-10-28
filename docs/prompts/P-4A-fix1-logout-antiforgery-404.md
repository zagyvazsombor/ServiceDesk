# P-4A-fix1 – Kijelentkezés a 404-es oldalról antiforgery-hibát ad
- Dátum: 2026-10-29
- Eszköz és modell: Claude Code (asztali alkalmazás), Claude Opus 5.5 (`claude-opus-5-5`)
- Cél és érintett szabályok:
  - A 404-es oldalról is működjön a kijelentkezés, az antiforgery-ellenőrzés megkerülése nélkül.
  - Előbb a gyökérok bizonyítékkal, utána javítás és reprodukáló teszt.
  - Szabályok: AGENTS.md §1, §3, §6 (`UseAntiforgery`, globális interaktivitás prerender nélkül, NotFound), §8, §12,
    §14.
- Átadott kontextus:
  - `AGENTS.md`, `CLAUDE.md`, `docs/prompts/P-4A-identity-runner.md`, `docs/prompts/P-4B-authorization.md`.
  - Web: `Program.cs`, `Components/App.razor`, `Routes.razor`, `Pages/NotFound.razor`, `Pages/Error.razor`,
    `Layout/MainLayout.razor`, `Layout/UserMenu.razor`, `Account/IdentityComponentsEndpointRouteBuilderExtensions.cs`,
    `Properties/launchSettings.json`, `appsettings.json`.
  - Application: `IUserQueries.cs`, `GetCurrentUserProfileHandler.cs`, `CurrentUserProfileDto.cs`.
  - Infrastructure: `DependencyInjection.cs`.
  - Tesztek: `ServiceDesk.Web.Tests.csproj`, `TestUsers.cs`, `UserMenuTests.cs`, `UseCaseRunnerTests.cs`;
    `Directory.Packages.props`.
  - A .NET 10 forrása (dotnet/aspnetcore, `release/10.0`): `RazorComponentEndpointInvoker.cs`,
    `DefaultAntiforgeryStateProvider.cs`, `AntiforgeryToken.cs`, `EndpointHtmlRenderer.Prerendering.cs`,
    `EndpointHtmlRenderer.Streaming.cs`.

## Prompt
[P-4A-fix1] Kijelentkezés a 404-es oldalról antiforgery-hibát ad
Jelenség:
Bejelentkezett felhasználóként a 404-es (Not Found) oldalon a Kijelentkezés gomb kezeletlen kivételt okoz:
- AntiforgeryValidationException: The required antiforgery request token was not provided in either form field "__RequestVerificationToken" or header value "RequestVerificationToken".
- BadHttpRequestException: Invalid anti-forgery token found when reading parameter "string returnUrl" from the request body as form.
A kivétel útja: AntiforgeryMiddleware → RequestLocalizationMiddleware → StatusCodePagesMiddleware → AuthorizationMiddleware.
Reprodukálás:
1. dotnet run --project src/ServiceDesk.Web
2. Belépés bejelento.dora@servicedesk.local fiókkal.
3. A címsorba: /nincs-ilyen-oldal
4. Az AppBarban a Kijelentkezés gomb (Layout/UserMenu.razor, POST Account/Logout).
Normál oldalról (pl. /tickets/my) a kijelentkezés működik.

Elvárt viselkedés:
A kijelentkezés minden oldalról működik, a 404-es oldalról is, és a felhasználó hiba nélkül kijelentkezett állapotba kerül. Érintett: AGENTS.md §6 (UseAntiforgery, globális interaktivitás prerender nélkül, NotFound), §8.

Korlát:
- Csak ezt javítsd; ne refaktorálj, ne bővíts funkciót.
- TILOS az antiforgery-ellenőrzés kikapcsolása vagy megkerülése (DisableAntiforgery, IgnoreAntiforgeryToken, GET-alapú kijelentkezés).
- Előbb derítsd ki és írd le a gyökérokot, bizonyítékkal. Vizsgáld meg, hogyan renderelődik a 404-es oldal (UseStatusCodePagesWithReExecute, Router NotFoundPage, statikus vagy interaktív mód, prerender: false), és hogy a kijelentkező űrlapban megjelenik-e a __RequestVerificationToken mező: a 404-es oldalon és egy normál oldalon is. Utána írd le a javítás tervét, és várd meg a jóváhagyásomat.
- Ha a hiba automatikus teszttel csak az AGENTS.md §3 listáján kívüli csomaggal reprodukálható (pl. Microsoft.AspNetCore.Mvc.Testing), a tervben jelezd, és kérj engedélyt. Ha automatikus reprodukció ésszerűen nem megoldható, javasolj pontos kézi reprodukciós protokollt.

Elfogadás:
- A hibát reprodukáló teszt előbb piros, a javítás után zöld; ha a fenti okból csak kézi reprodukció van, a protokoll a javítás előtt hibát, utána sikert mutat, és ezt a napló rögzíti.
- A kijelentkezés normál oldalról továbbra is működik.
- A teljes tesztcsomag zöld; dotnet build 0 hiba, 0 figyelmeztetés.

Zárás: AGENTS.md §1 és §14, napló: P-4A-fix1-logout-antiforgery-404.

Jóváhagyás a terv után (eltérésekkel):
Jóváhagyom az F1-et, a következőkkel:
- K1: igen. Az AGENTS.md §6-ot most módosítsd: a PageRenderMode a sablon logikáját követi, azzal a kivétellel, hogy a státuszkód-újrafuttatott és a hibakezelő kérések statikusan renderelődnek (indok és hivatkozás: ADR-006).
- K2: igen. A Microsoft.AspNetCore.Mvc.Testing kerüljön a §3 tesztcsomag-listájára; a §12 Web.Tests sora bővüljön: „valamint HTTP-szintű tesztek WebApplicationFactory-val a Http/ mappában”. A teszt-hitelesítési séma és a csonkok csak a tesztprojektben létezhetnek.
- K3: igen, ADR-006.
- K4: a hatókörbe tartozik, mert ugyanaz a gyökérok. A feltétel a hibakezelő kérést is zárja ki (IExceptionHandlerFeature), így a hibaoldal is statikusan renderelődik. Ha a WebApplicationFactory-val Production környezetben egy kivételt dobó, csak tesztben regisztrált végponttal ésszerűen tesztelhető, írj rá tesztet; ha nem, rögzítsd a naplóban, hogy ez az ág csak kódszinten ellenőrzött.
- A kézi protokollt (M1–M4) a javítás után futtasd le, és az eredményt rögzítsd a naplóban.
Mehet a megvalósítás.

Utólagos utasítás a megvalósítás után:
Az IExceptionHandlerFeature feltétel kerüljön ki: a mérés szerint felesleges, és az indoklása félrevezető. Konkrétan:
- App.razor: a PageRenderMode csak az IStatusCodeReExecuteFeature-t vizsgálja; a komment csak ezt indokolja.
- A hibaoldalas HTTP-tesztek maradnak; ezek őrzik a framework mai viselkedését, és jeleznek, ha az megváltozik.
- ADR-006 és AGENTS.md §6: a kivétel csak a státuszkód-újrafuttatott kérésekre vonatkozzon. A hibaoldalról egy mondat: a framework eleve statikusan rendereli, ezt teszt ellenőrzi.
- AGENTS.md §4: a ServiceDesk.Web.Tests megjegyzése legyen „bUnit és HTTP-szintű tesztek”.
- A P-4A-fix1 naplóban rögzítsd, hogy a K4 feltevése tévesnek bizonyult, és a feltétel ezért került ki.
Utána dotnet build és dotnet test; más nem változik.

## Eredmény
- Létrehozott és módosított fájlok:
  - `AGENTS.md` (K1, K2): **a szabályrendszer ezzel a lépéssel változott.**
    - §3: a tesztcsomagok listája bővült a `Microsoft.AspNetCore.Mvc.Testing`-gel.
    - §4: a könyvtárfában a `ServiceDesk.Web.Tests/` megjegyzése „bUnit és HTTP-szintű tesztek” lett (utólagos
      utasítás).
    - §6: a `PageRenderMode`-szabály kivételt kapott: a státuszkód-újrafuttatott (`IStatusCodeReExecuteFeature`)
      kérések statikusan renderelődnek (ADR-006). Egy mondat rögzíti, hogy a hibakezelő oldalt a framework eleve
      statikusan rendereli, és ezt teszt ellenőrzi.
    - §12: a Web.Tests sora bővült: „valamint HTTP-szintű tesztek WebApplicationFactory-val a `Http/` mappában”.
  - `Directory.Packages.props`: `Microsoft.AspNetCore.Mvc.Testing` 10.0.14, ugyanaz a verzió, mint a többi
    `Microsoft.AspNetCore.*` csomagé.
  - `src/ServiceDesk.Web/Components/App.razor`: a `PageRenderMode` csak akkor interaktív, ha az
    `AcceptsInteractiveRouting()` igaz, és a kérés nem státuszkód-újrafuttatás (`IsStatusCodePage`). Ehhez
    `@using Microsoft.AspNetCore.Diagnostics` kellett.
  - `tests/ServiceDesk.Web.Tests`:
    - `ServiceDesk.Web.Tests.csproj`: `PackageReference` a `Microsoft.AspNetCore.Mvc.Testing`-re.
    - új `Http/ServiceDeskWebApplicationFactory.cs`: `WebApplicationFactory<Program>`.
      - Production környezetben fut: él a hibakezelő oldal, és nincs adatbázis-indítás.
      - Teszt-kapcsolati stringet kap; az adatbázist a tesztek nem nyitják meg.
      - A teszt-hitelesítési séma lesz az alapértelmezett hitelesítő séma.
      - Az `IUserQueries` NSubstitute-csonk.
      - Egy `IStartupFilter` regisztrálja a kivételt dobó útvonalat.
    - új `Http/TestAuthenticationHandler.cs`: minden kérés a `TestUsers.Reporter` felhasználó (K2: csak a
      tesztprojektben létezik).
    - új `Http/ThrowingRequestStartupFilter.cs`: a csővezeték végére fűzött, csak tesztben létező kezelő.
      A `/test/throw` útvonalon kivételt dob.
    - új `Http/LogoutFormTests.cs`: 5 teszt.
      - `Get_StatusCodeOrErrorPage_RendersStaticallyWithLogoutToken`, két esettel (404 és 500): nincs interaktív
        marker, és a kijelentkező űrlapban van token.
      - `PostLogout_TokenOfStatusCodeOrErrorPage_SignsOutAndRedirectsBack`, ugyanazzal a két esettel: a kivett
        tokennel küldött POST 302-t ad a `ReturnUrl`-re, és törli az Identity-cookie-t.
      - `Get_NormalPage_StaysInteractiveWithPersistedState`: kontrollteszt. A `/tickets/my` interaktív marad, és van
        megőrzött állapota.
      - Az osztálykomment a feltétel kivétele után pontosítva: a hibaoldalas esetek a framework viselkedését őrzik.
  - új `docs/adr/ADR-006-static-status-code-pages.md`. Az első változat neve
    `ADR-006-static-status-code-and-error-pages.md` volt; a hatókör szűkülése miatt a fájl átnevezve (még nem volt
    commitolva).
- Gyökérok (bizonyítékkal, a javítás előtti állapotban):
  - A közvetlenül megnyitott ismeretlen URL-t a `UseStatusCodePagesWithReExecute("/not-found")` újrafuttatja.
    A végpont a NotFound oldal, ezért az `AcceptsInteractiveRouting()` igaz, és az oldal interaktív lesz, prerender
    nélkül.
  - A .NET 10 `RazorComponentEndpointInvoker` újrafuttatott kérésnél nem írja ki a megőrzött komponensállapotot
    (`if (!isErrorHandlerOrReExecuted) { … PrerenderPersistedStateAsync … }`).
  - Az interaktív circuit csak ebből az állapotból kapja meg az antiforgery-tokent
    (`DefaultAntiforgeryStateProvider`, `[PersistentState]`). Ha a token `null`, az `AntiforgeryToken` semmit
    sem renderel.
  - A nyers HTML-válaszok, bejelentkezve:
    - `/tickets/my`: 200, 2 interaktív marker, van `Blazor-Server-Component-State`.
    - `/nincs-ilyen-oldal`: 404, 2 marker, **nincs** állapot.
    - `/not-found` közvetlenül: 200, 2 marker, van állapot.
    - A kijelentkező űrlap egyik válasz HTML-jében sincs benne, mert csak a circuitben jön létre.
  - A DOM a böngészőben:
    - `/tickets/my` újratöltve: van token (198 karakter).
    - `/nincs-ilyen-oldal` újratöltve: **nincs token**.
    - Ugyanebben a circuitben `/tickets/my`-ra lépve: **továbbra sincs token**.
    - `/tickets/my`-ról alkalmazáson belül a 404-re lépve: van token.
    - A hibát tehát a 404-es válaszból induló circuit okozza, nem maga a NotFound oldal.
  - A Kijelentkezés a 404-es oldalon 400-as „Bad Request” oldalt adott. A szervernaplóban
    `BadHttpRequestException` volt, belső kivételként `AntiforgeryValidationException`, ahogy a bejelentésben.
  - Az `Account/Manage` statikus oldal HTML-jében a kijelentkező űrlap tokennel együtt szerepel. Statikus
    renderelésben tehát a `UserMenu` működik.
- Döntések és indoklásuk:
  - **F1:** a státuszkód-újrafuttatott oldal statikusan renderelődik, így a token a `HttpContext`-ből a HTML-be
    kerül. Az antiforgery-védelem változatlan.
    - Elvetett lehetőségek (ADR-006): `[ExcludeFromInteractiveRouting]` a NotFound oldalon; a re-execute
      elhagyása; a megőrzött állapot kikényszerítése, ami nem lehetséges; GET-alapú kijelentkezés, ami tiltott.
  - **K4 – a feltevés tévesnek bizonyult, a feltétel kikerült.**
    - A feltevés: a tervben azt írtam, hogy a hibakezelő oldal ugyanúgy token nélküli circuitet indítana. Erre épült a
      jóváhagyott K4. **Ez nem volt igaz.**
    - Mérés: a hibaoldal két HTTP-tesztje a javítás előtt is zöld volt.
    - A framework forrása szerint az `EndpointHtmlRenderer` hibakezelés közben (`_isHandlingErrors`) figyelmen
      kívül hagyja a render mode határait („Ignore the render mode boundary in error scenarios”). A hibaoldal tehát
      eleve statikus.
    - Az `IExceptionHandlerFeature` feltétel először a K4 szerint bekerült. Az utólagos utasítás alapján kikerült,
      mert felesleges, és az indoklása félrevezető volt.
    - Az `App.razor` ma csak az `IStatusCodeReExecuteFeature`-t vizsgálja. Az ADR-006 és az AGENTS.md §6 kivétele
      is csak a státuszkód-újrafuttatott kérésekre vonatkozik.
    - A hibaoldalas HTTP-tesztek maradnak. A framework mai viselkedését őrzik, és jeleznek, ha az megváltozik. Ezek
      regressziós tesztek, nem piros–zöld reprodukciók.
  - **Kivételt dobó útvonal:** valódi végpontot a teszt nem tud a `Program.cs` route-builderéhez adni. Helyette egy
    `IStartupFilter` a csővezeték végére fűz egy kezelőt. Ez a `Program.cs` hibakezelőjén belül fut, csak egy útvonalra
    dob kivételt, és csak a tesztprojektben létezik.
  - **HTTP-tesztek adatbázis nélkül:**
    - A teszt-hitelesítési séma helyettesíti az Identity-cookie-t.
    - Az `IUserQueries` csonkja helyettesíti a profillekérdezést, így a `UserMenu` statikus renderelése nem nyúl
      LocalDB-hez.
    - A kijelentkezés valódi végponton fut: `SignInManager.SignOutAsync`, Identity-cookie törlése.
  - **HTML-elemzés:** AngleSharp. A bUnit függőségeként már a projekt része, és a bUnit-tesztek is az AngleSharp
    típusait használják. Új csomagreferencia nem kellett.
  - A `Program` osztály nyilvános (a .NET 10 generálja), ezért a `Program.cs` nem változott.

## Ellenőrzés
- dotnet build: Build succeeded – 0 Warning(s), 0 Error(s) (`--no-incremental` újrafordítással).
- dotnet test: Passed – 300/300 zöld. Projektenként: Domain 213, Application 16, Infrastructure 21, Web 36
  (ebből 5 új HTTP-teszt), Architecture 14.
- Piros–zöld az új teszteken (`--filter-class ServiceDesk.Web.Tests.Http.LogoutFormTests`):
  - A javítás előtt: 5-ből 2 piros, 3 zöld.
    - Piros a `Get_…(/nincs-ilyen-oldal, NotFound)`: a HTML interaktív markert tartalmaz.
    - Piros a `PostLogout_…(/nincs-ilyen-oldal)`: a HTML-ben nincs kijelentkező űrlap, így token sincs.
    - Zöld a két hibaoldalas eset (lásd a K4 helyesbítését) és a kontrollteszt.
  - A javítás után: 5/5 zöld.
  - A piros–zöld mérés még az `IExceptionHandlerFeature` feltétellel készült. A feltétel kivétele után újra
    lefuttatva: build 0 hiba, 0 figyelmeztetés (`--no-incremental`); `dotnet test` 300/300 zöld; a
    `LogoutFormTests` 5/5 zöld. A hibaoldalas esetek a feltétel nélkül is zöldek, ami megerősíti, hogy a framework
    eleve statikusan rendereli a hibaoldalt.
- Kézi protokoll a beépített böngészőben: `http://localhost:5123`, Development környezet, fejlesztői `ServiceDesk`
  adatbázis, `bejelento.dora` fiók.

  | Lépés | A javítás előtt | A javítás után |
  |---|---|---|
  | M1: `/nincs-ilyen-oldal` újratöltve, majd Kijelentkezés | Nincs token az űrlapban. A szerver 400-as „Bad Request” oldalt ad, a naplóban `AntiforgeryValidationException` van. | Az oldal statikus (0 interaktív marker, 404), van token (198 karakter). A kijelentkezés után `/not-found`, kijelentkezett állapot, megjelenik a Login link. |
  | M2: `/tickets/my` újratöltve, majd Kijelentkezés | Működik (a bejelentés szerint). | Van token. A kijelentkezés után `/Account/Login?ReturnUrl=%2Ftickets%2Fmy`, kijelentkezett állapot. |
  | M3: `/nincs-ilyen-oldal` újratöltve, menülinkkel `/tickets/my`-ra, ott Kijelentkezés | A `/tickets/my` oldalon sincs token, mert ugyanaz a circuit fut. | A 404-en is és a `/tickets/my`-n is van token. A kijelentkezés után Login oldal, kijelentkezett állapot. |
  | M4: `/tickets/my` újratöltve, alkalmazáson belül `/nincs-ilyen-oldal`-ra, ott Kijelentkezés | Van token (az M4 kijelentkezését a javítás előtt nem futtattam). | Van token. A kijelentkezés után `/nincs-ilyen-oldal`, kijelentkezett állapot. |

  - A szervernaplóban a javítás utáni futásban 0 `fail` sor és 0 antiforgery-bejegyzés volt. Az egyetlen `warn` a
    http-profil miatti „Failed to determine the https port for redirect”.
  - Az ellenőrzéshez az alkalmazás háttérben futott (`dotnet run --launch-profile http`), utána leállítottam.
    Projektfájl nem keletkezett.
  - Az M1–M4 lépések még az `IExceptionHandlerFeature` feltétellel futottak. A feltétel kivétele csak a hibakezelő
    ágat érinti, ami Developmentben nem fut (ott a fejlesztői kivételoldal működik). Az utólagos utasítás szerint
    ezért csak build és teszt futott újra, a kézi protokoll nem.
- Javasolt kézi ellenőrzés:
  - Az M1–M4 lépések egy Admin fiókkal is (`admin.anna`).
  - A hibaoldal kézi kipróbálása éles környezetben. A HTTP-teszten túl ezt nem ellenőriztem kézzel, mert
    Developmentben a fejlesztői kivételoldal fut.

## Ismert korlátok
- Ha a 404-es oldalt közvetlenül nyitották meg, a kijelentkezés utáni visszairányítás a `/not-found` címre visz,
  nem az eredeti URL-re. Statikus renderelésben ugyanis a `NavigationManager` az újrafuttatott útvonalat látja, és a
  `ReturnUrl` ebből készül. A megjelenő oldal ugyanaz a NotFound oldal (200-as állapottal). Alkalmazáson belüli 404-nél
  (M4) az eredeti URL marad.
- A HTTP-tesztek nem vezérlik a circuitet. A normál, interaktív oldalról történő kijelentkezést (M2, M4) csak a kézi
  protokoll ellenőrzi.
- A HTTP-tesztek teszt-hitelesítési sémát és profilcsonkot használnak, ezért a valódi Identity-cookie és a LocalDB
  ezekben nem szerepel. Ezeket az Infrastructure-tesztek és a kézi protokoll fedik le.
- Előzetes állapot, nem ennek a lépésnek a része: a NotFound és az Error sablonoldal szövegei angolok
  („404 - Page Not Found”, „Error.”), pedig a §6 magyar UI-szöveget ír elő.

## Emberi javítás
A kézi tesztelés során feltárt 404/Logout Antiforgery hibát jeleztem. A gyökérok elemzését és az F1 javítást (statikus renderelés az újrafuttatott státuszkód és hibaoldalakon) jóváhagytam a K1–K4 pontokkal együtt (ADR-006, AGENTS.md frissítés, Microsoft.AspNetCore.Mvc.Testing bevezetése). Az IExceptionHandlerFeature feltételt defenzív védelemként a kódban hagytam. Az M1–M4 kézi protokoll és a 300/300 teszt hibátlanul lefutott.
## Commit
fix(web): render status code pages statically to preserve antiforgery token (ADR-006)
