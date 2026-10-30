# P-9A – Biztonsági és minőségi átvizsgálás
- Dátum: 2026-10-30
- Eszköz és modell: Claude Code (asztali alkalmazás, Code fül), Claude Opus 5.5 (`claude-opus-5-5`)
- Cél és érintett szabályok: AGENTS.md §1, §6, §7, §8, §11, §12, §13, §14, §15, §17; BR-01–BR-13 (jelölt tesztek),
  különösen BR-01, BR-02, BR-09 (erőforrás-ellenőrzés), BR-04, BR-12, BR-13 (ismert korlátok)
- Átadott kontextus: AGENTS.md, CLAUDE.md, a teljes `src/` és `tests/`, a `docs/prompts/P-1A` … `P-8B` naplók,
  `docs/adr/`

## Prompt
```
[P-9A] Biztonsági és minőségi átvizsgálás
Kontextus: a teljes AGENTS.md, különösen §8, §11, §15, §17. A 8. lépés elkészült.

Feladat:
1. Készíts docs/security-review.md táblázatot a kódból: handler → szükséges szerepkör → erőforrás-ellenőrzés (ha van) → az ezt bizonyító teszt neve. Ahol hiányzik az ellenőrzés vagy a teszt, pótold.
2. Ellenőrizd a §17 tiltólistáját a teljes kódbázisban, és javítsd az eltéréseket.
3. Biztonsági fejlécek kis middleware-rel: X-Content-Type-Options: nosniff, Referrer-Policy: strict-origin-when-cross-origin, X-Frame-Options: DENY. Content-Security-Policy csak Report-Only módban, és csak ha a MudBlazor és a Blazor-kapcsolat működését nem töri.
4. Naplózás átnézése: nincs jelszó, kulcs és jegyleírás a naplóban; a megtagadott hozzáférés Warning szinten jelenik meg.
5. A dotnet format --verify-no-changes legyen tiszta; a sablon nem használt fájljai törölve.
6. A BR-01…BR-13 mindegyikére legyen legalább egy [Trait("BR", …)] jelölésű teszt; a hiányzókat pótold.

Ne csináld: új funkció, stackváltás.

Elfogadás: a security-review.md minden sora teszttel alátámasztott; a build, a tesztek és a formátum-ellenőrzés zöld.

Zárás: AGENTS.md §1 és §14, napló: P-9A-hardening.
```

A terv jóváhagyása (szó szerint):
```
Jóváhagyom a három javasolt döntéssel, a következő pontosításokkal és kiegészítésekkel:
- CSP-hash: a hash ne legyen beégetett konstans. Az importmap a fingerprintelt asset-URL-ek miatt buildenként változhat, ezért a hash-t futásidőben, ugyanabból az importmap-definícióból számold, amelyet az oldal kiír. A teszt azt ellenőrzi, hogy a fejlécben lévő hash egyezik a HTML importmapjével.
- Sorvég: a .gitattributes és az end_of_line = lf jöhet. Git-parancsot ne futtass; a renormalizálást (git add --renormalize .) én végzem el, a naplóba írd le a lépést.
- Korábbi döntések szerint ide tartozó tételek, ezek is a lépés részei:
  1. A szerepkörök (Reporter, Admin) létrehozása minden környezetben fusson idempotensen az indításkor; a demóadat (felhasználók, kategóriák, ügyintézők) seedelése maradjon csak Development. Teszt: Production környezetben indítva a szerepkörök léteznek, demófelhasználó nem jön létre; a regisztráció Reporter szerepkört ad.
  2. A sablon személyesadat-törlő oldala: kivételt adok a §6 alól. A Manage menüből a törlés linkje kerüljön ki, a DeletePersonalData oldal pedig törlés helyett magyar tájékoztatást adjon („A fiók törlése nem érhető el, fordulj az adminisztrátorhoz.”). Más Account-oldal nem változik. HTTP-teszt rá.
  3. A security-review.md kapjon „Ismert korlátok” szakaszt az összes prompt-napló ismert korlátaiból összegyűjtve (köztük az Agent-táblán hiányzó konkurenciatoken két versenyhelyzete, a seed-jelszó helye, az angol Account-oldalak), mindegyiknél: kockázat, indok, lehetséges javítás.
- LocalDB indulás: a tesztek kapcsolati stringje kapjon hosszabb Connect Timeout-ot (pl. 60 mp), hogy a leállított LocalDB első indítása ne buktassa el a futást. Ellenőrzés: leállított LocalDB-ből (sqllocaldb stop MSSQLLocalDB) elsőre zöld a teljes futás.
- ADR-008 a biztonsági fejlécekhez rendben.
Mehet a megvalósítás.
```

Döntés munka közben (az ügynök jelezte, hogy a Manage menüben nincs törlés-link, a „Delete” link a `PersonalData.razor`-on
van), szó szerint:
```
Delete link ki (Javasolt): a §6-kivétel kiterjed a PersonalData.razor-ra, de csak a Delete link és a hozzá tartozó bekezdés törlődik; a Download marad, a Personal data menüpont is marad. A DeletePersonalData oldal közvetlen URL-en a magyar tájékoztatást mutatja. Az AGENTS.md §6 kivételét egészítsd ki ennek megfelelően (PersonalData.razor: csak a Delete link), és rögzítsd a naplóban.
```

## Eredmény
- Létrehozott és módosított fájlok:
  - Repó-szint:
    - új: `.gitattributes` (`* text=auto eol=lf`, bináris: `*.ico`, `*.png`);
    - módosítva: `.editorconfig` (`end_of_line = lf`), `AGENTS.md` (§6: a fióktörlés kivétele).
  - `docs/`:
    - új: `security-review.md`, `adr/ADR-008-security-headers-csp-report-only.md`,
      `adr/ADR-009-roles-in-every-environment.md`, ez a napló.
  - `src/ServiceDesk.Web`:
    - új: `Security/SecurityHeadersMiddleware.cs`, `Security/SecurityHeadersApplicationBuilderExtensions.cs`,
      `Security/AccessDeniedLoggingResultHandler.cs`, `Components/Layout/NotAuthorizedAlert.razor`;
    - módosítva: `Program.cs` (fejléc-middleware, `IAuthorizationMiddlewareResultHandler`,
      `ContentSecurityFrameAncestorsPolicy = "'none'"`, `SeedRolesAsync` Developmenten kívül), `Components/Routes.razor`
      (`NotAuthorizedAlert`), `Components/_Imports.razor` (6 nem használt using törölve);
    - Account (a §6 kivétele szerint): `Pages/Manage/DeletePersonalData.razor` (csak tájékoztatás),
      `Pages/Manage/PersonalData.razor` (a Delete link és a figyelmeztetése törölve);
    - Account, csak formátum: `IdentityComponentsEndpointRouteBuilderExtensions.cs` (import-sorrend),
      `IdentityNoOpEmailSender.cs` (két szóközös üres sor).
  - `src/ServiceDesk.Infrastructure`:
    - új: `Persistence/Seed/RoleSeeder.cs`;
    - módosítva: `Persistence/Seed/DatabaseSeeder.cs` (a szerepköröket a `RoleSeeder` hozza létre),
      `Persistence/DatabaseInitializer.cs` (`SeedRolesAsync`), `DependencyInjection.cs` (`RoleSeeder` regisztráció),
      `Ai/LlmCategorySuggester.cs` (import-sorrend).
  - `src/ServiceDesk.Application`: `Tickets/SuggestCategory/SuggestCategoryHandler.cs` (import-sorrend).
  - `tests/ServiceDesk.Application.Tests`: új `Common/Security/ReporterInputShapeTests.cs`.
  - `tests/ServiceDesk.Infrastructure.Tests`: `TestDatabase.cs` (`Connect Timeout=60`),
    `Persistence/DatabaseSeederTests.cs` (+2 teszt).
  - `tests/ServiceDesk.Web.Tests`:
    - új, `Http/`: `SecurityHeadersTests.cs`, `AccessDeniedLoggingTests.cs`, `RecordingLoggerProvider.cs`,
      `ProductionDatabaseFixture.cs`, `ProductionDatabaseCollection.cs`, `ProductionWebApplicationFactory.cs`,
      `AccountForms.cs`, `ProductionStartupTests.cs`, `PersonalDataPagesTests.cs`;
    - új: `Security/AccessDeniedLoggingResultHandlerTests.cs`, `Layout/NotAuthorizedAlertTests.cs`;
    - módosítva: `Http/ServiceDeskWebApplicationFactory.cs` (csonkolt `IRoleStore`).
- Döntések és indoklásuk:
  - **A felhasználó döntései a terv jóváhagyásakor:**
    - **D1 – oldalszintű megtagadás naplózása:** két ág.
      - HTTP-kérés: az `AccessDeniedLoggingResultHandler` `Forbidden` esetén Warningot ír (útvonal query string
        nélkül, felhasználó-Id), majd az alapértelmezett kezelő válaszol (`AccessDenied` oldal). A névtelen
        challenge (bejelentkezés felé) nem megtagadás, nem naplózódik.
      - Circuiten belüli navigáció: a `NotAuthorizedAlert` ugyanazt a magyar üzenetet mutatja, és ugyanazzal a
        szöveggel ír Warningot.
      - Indok: a keretrendszer ezt Information szinten írja, amit a `Microsoft.AspNetCore: Warning` szűrő elnyel; a
        circuiten belül pedig semmi nem íródott.
    - **D2 – sorvég:** `end_of_line = lf` és `.gitattributes`. A 27 formátumhiba 24 sora abból jött, hogy minden
      fájl LF, de a `dotnet format` Windows-on CR LF-et várt. A `core.autocrlf=true` miatt egy friss klón CR LF-et
      kapott volna, ezért kell a `.gitattributes` is. A maradék 3 hiba az import-sorrend volt, ezt kézzel javítottam.
    - **D3 – CSP-hash futásidőben:** a végpont `ImportMapDefinition`-jéből, amelyet az `<ImportMap />` is kiír.
  - **Megvalósítás közbeni megállapítások:**
    - **A keretrendszer saját, kikényszerített CSP-je:** az Interactive Server végpontok
      `Content-Security-Policy: frame-ancestors 'self'` fejlécet kapnak. A böngésző ezt előnyben részesíti az
      `X-Frame-Options`-szel szemben, így a `DENY` hatástalan lett volna. Ezért a
      `ContentSecurityFrameAncestorsPolicy` értéke `'none'` lett (ADR-008), és teszt ellenőrzi.
    - **A hash és a sorvég:** az `ImportMapDefinition.ToJson()` internal; a publikus `ToString()` ugyanazt adja
      (a keretrendszer forrása szerint). A JSON Windows-on CR LF-fel van tagolva, a böngésző viszont a feldolgozott
      (LF-re normalizált) szöveg hash-ét veszi. Ezért a middleware is normalizál. A teszt AngleSharp-pal, a kézi
      próba a böngészőben számolta újra a hash-t; mindkettő egyezett a fejléccel.
    - **A fejlécek beállítása `OnStarting`-ban:** így túlélik a kivételkezelő `Response.Clear()`-jét, és felülírják
      az antiforgery `SAMEORIGIN` értékét.
  - **Kiegészítések (a jóváhagyás szerint):**
    - **Szerepkörök (ADR-009):** új `RoleSeeder`. Developmentben az `InitializeDatabaseAsync` fut (migráció, a
      szerepkörök, a demóadat), máshol a `SeedRolesAsync` (csak a szerepkörök, migráció nélkül).
      - Az adatbázis nélküli HTTP-factory csonkolt `IRoleStore`-t kap, így továbbra sem nyit adatbázist.
      - A Production-tesztek saját `ServiceDesk_WebTests` adatbázist használnak. Ezt egy közös collection fixture
        egyszer hozza létre (`EnsureDeleted` + `MigrateAsync`), és a két tesztosztály egymás után fut. Indok: a
        `ServiceDesk_Tests`-et az Infrastructure-tesztek Respawn-nal ürítik, miközben a tesztprojektek
        párhuzamosan futnak.
      - A fixture a `DbContext`-et az `AddInfrastructure` regisztrációiból hozza létre. A puszta
        `DbContextOptions` nem ismeri az Identity `SchemaVersion = Version3` beállítását, ezért a modell eltérne a
        migrációtól (`PendingModelChangesWarning`).
    - **Fióktörlés (§6 kivétel):**
      - A Manage menüben (`ManageNavMenu`) nem volt törlés-link, a link a `PersonalData.razor`-on volt. Ezt jeleztem;
        a felhasználó döntése szerint ott csak a Delete link és a hozzá tartozó figyelmeztetés került ki.
      - A Download és a „Personal data” menüpont maradt. A `PersonalData` angol bevezetője („download or delete”)
        változatlan.
      - A `DeletePersonalData.razor` csak tájékoztat; nincs űrlapja és nincs törlő kódja. A sablon régi űrlapjának
        megfelelő POST 400-at kap, és a felhasználó megmarad (teszt).
      - Az AGENTS.md §6 rögzíti a kivételt.
    - **LocalDB:** `Connect Timeout=60` az Infrastructure-tesztek és a `ServiceDesk_WebTests` kapcsolati stringjében.
      Az adatbázis nélküli HTTP-factory stringje nem változott, mert az nem nyit kapcsolatot.
  - **Over-posting teszt:** a `ReporterInputShapeTests` reflexióval ellenőrzi, hogy a `CreateTicketCommand` pontosan
    a megengedett hat mezőt tartalmazza. Azt is ellenőrzi, hogy egyik Bejelentő-bemenetben sincs szerveroldali mező
    (`ReporterId`, `UserId`, `RequestedByUserId`, `Status`, `AssigneeAgentId`, `Priority`, `Decision`).
  - **§17 és sablon:** kódsértés nem volt; a részletes ellenőrzőlista a `security-review.md` 7. szakaszában van.
    - Nem használt sablonfájl nem maradt: a Counter, Weather, Auth oldal és a `Data/` már korábban törlődött, minden
      komponensre van hivatkozás.
    - A `_Imports.razor`-ból törölt usingok: `System.Net.Http`, `System.Net.Http.Json`,
      `static …RenderMode`, `…Web.Virtualization`, `Microsoft.JSInterop`, `MudBlazor.Services`. A build igazolja, hogy
      nem kellettek.
  - **Naplózás:** átnéztem az összes naplóhívást; mind message template-et használ.
    - Nincs bennük jelszó, cookie, kulcs vagy jegyszöveg, és nincs `EnableSensitiveDataLogging`.
    - Nyitott pont (ismert korlátként rögzítve): a `LlmCategorySuggester.LogFailed` a teljes kivételt naplózza.
  - **BR-trait:** mind a 13 BR-hez volt jelölt teszt; az új tesztek is jelöltek. A BR-02-nek nincs Domain-tesztje,
    mert nincs domain-logikája.
  - **security-review.md:** 26 handler, UI-szint, Identity, fejlécek, naplózás, AI, §17, BR-lefedettség, ismert
    korlátok (33 nyitott, 14 megszűnt).
    - Egy segédszkript (a scratchpadben, a repón kívül) mind a 126 hivatkozott `Osztály.Metódus` tesztnevet
      megkereste a `tests/**/*.cs`-ben, és mindet megtalálta.
  - **Git:**
    - A feltárás elején, a „Git-parancsot ne futtass” utasítás előtt csak olvasó git-parancsok futottak: `ls-files`,
      `status`, `config --get core.autocrlf`, `log -S`.
    - Az utasítás után egy git-parancs sem futott. Commit, push, branch-váltás nem volt.

## Ellenőrzés
- dotnet build: `dotnet build --no-incremental` → 0 hiba, 0 figyelmeztetés.
- dotnet format: `dotnet format ServiceDesk.slnx --verify-no-changes` → kilépési kód 0 (előtte 27 hiba: 24 WHITESPACE,
  3 IMPORTS).
- dotnet test: `sqllocaldb stop MSSQLLocalDB` után `dotnet test --solution ServiceDesk.slnx` → elsőre zöld,
  **828/828** (a lépés előtt 796; +32 új teszt). Kiindulásként a leállított LocalDB-vel a 123 Infrastructure-teszt
  elbukott; ezt a `Connect Timeout=60` szüntette meg.
- Böngészős ellenőrzés (`servicedesk-web`, Development, a beépített böngészőpanel):
  - Fejlécek a kezdőlapon: `X-Frame-Options: DENY`, `X-Content-Type-Options: nosniff`,
    `Referrer-Policy: strict-origin-when-cross-origin`, `Content-Security-Policy: frame-ancestors 'none'`, és a
    Report-Only policy a hash-sel.
  - Pozitív kontroll: egy beszúrt inline script `securitypolicyviolation` eseményt adott (`script-src-elem`,
    `disposition: report`, a script lefutott). Az esemény-figyelő tehát érzékeli a jelentéseket, és a Report-Only nem
    blokkol.
  - A böngésző `crypto.subtle`-lel számolt importmap-hash-e egyezett a fejléccel.
  - Bejelentkezve Bejelentőként (`bejelento.dora`) és Adminként (`admin.anna`) bejártam a fő oldalakat. Mindvégig
    figyelt a `securitypolicyviolation`-figyelő, és egyetlen jelentés sem érkezett, a circuit végig élt. A bejárt
    oldalak:
    - kezdőlap, új jegy (MudSelect-popover), saját jegyek;
    - jegysor (9 sor), kategóriák („Új kategória” dialógus), ügyintézők („Kiss Anna kompetenciái” dialógus).
  - Betöltött erőforrások: csak `http://localhost:5123` és `https://fonts.googleapis.com`; az egyetlen inline script
    az importmap.
  - Megtagadás Bejelentőként az `/admin/queue` oldalon:
    - circuiten belül magyar üzenet és Warning a `NotAuthorizedAlert`-től;
    - közvetlen URL-en átirányítás az `AccessDenied` oldalra és Warning az `AccessDeniedLoggingResultHandler`-től.
    - Mindkét napló: `Page /admin/queue denied access to user <Id>`. A szervernaplóban Error nem volt.
- Javasolt kézi ellenőrzés:
  - **Renormalizálás (a hallgató lépése):** a `.gitattributes` miatt `git add --renormalize .`, majd `git status`.
    Mivel minden fájl már LF volt az indexben, várhatóan nincs tartalmi változás.
  - Böngészőben, DevTools Console: a fő oldalakon nincs `[Report Only]` CSP-üzenet. `dotnet watch` alatt a
    böngészőfrissítő script adhat ilyet; ez várható.
  - DevTools Network: a válaszfejlécek a fenti értékekkel; `/Error` és `/nincs-ilyen-oldal` esetén is.
  - Production-próba egy üres, migrált adatbázison:
    - `dotnet ef database update`, majd `ASPNETCORE_ENVIRONMENT=Production` mellett indítás;
    - regisztráció után a felhasználó `Reporter`;
    - az `AspNetRoles`-ban két sor van, demófelhasználó nincs.
  - Account → Manage → Personal data: van Download, nincs Delete. Az `/Account/Manage/DeletePersonalData` oldal a
    magyar tájékoztatást mutatja.
- Ismert korlátok: a `docs/security-review.md` 11. szakasza (33 nyitott tétel, a P-9A újaival). Kiemelve:
  - a CSP csak Report-Only, `report-uri` nélkül;
  - a hash a keretrendszer kiírására épül;
  - Developmenten kívül a sémát telepíteni kell.
- Utólagos módosítás (a felhasználó „Igen, frissítsd” válasza alapján): az AGENTS.md §7 indítási bekezdése az
  ADR-009 szerint frissült. Minden környezetben fut a `RoleSeeder` (migráció nélkül); csak Developmentben fut a
  `MigrateAsync` és a demó-`DatabaseSeeder`. A seed-jelszó bekezdése: élesben a demóseeder nem fut, csak a
  `RoleSeeder`. Csak dokumentáció változott, ezért build és teszt nem kellett hozzá.

## Emberi javítás
Kézi forráskód-módosításra nem volt szükség. A jóváhagyott döntéseket (dinamikus CSP-hash számítás normalizált importmapből, biztonsági fejlécek és X-Frame-Options DENY / frame-ancestors none, oldalszintű hozzáférés-megtagadás Warning naplózása, RoleSeeder környezetfüggetlen futtatása ADR-009 szerint, fióktörlés helyetti magyar tájékoztatás a §6 kivételével, valamint a leállított LocalDB-hez 60 mp-es timeout) maradéktalanul elfogadtam. A security-review.md táblázatot és a 33 ismert korlát feldolgozását átnéztem. A git renormalizálást a .gitattributes alapján elvégeztem. A dotnet format tiszta, mind a 828/828 teszt zöld.
## Commit
chore(security): apply security hardening, dynamic csp, role seeding and audit review
