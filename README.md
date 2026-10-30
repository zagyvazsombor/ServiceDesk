# ServiceDesk

Hibajegykezelő webalkalmazás a „Webes alkalmazások” egyetemi projekthez.

- Jegy rögzítése; kategorizálás LLM-javaslattal és emberi jóváhagyással.
- Prioritás a Hatás × Sürgősség mátrixból; automatikus és kézi kiosztás.
- Státuszkezelés; kategóriák és ügyintézők törzsadat-kezelése.
- Két szerepkör: `Reporter` (Bejelentő) és `Admin` (ügyintéző és adminisztrátor egyben).

Technológia: .NET 10, Blazor Web App (globális Interactive Server, prerender nélkül), MudBlazor 9, ASP.NET Core
Identity, EF Core 10 SQL Server LocalDB-n, `Microsoft.Extensions.AI` lokális Ollamával. A teljes szabályrendszer az
[AGENTS.md](AGENTS.md)-ben van, az architektúra a [docs/architecture.md](docs/architecture.md)-ben.

## Előfeltételek

| Eszköz | Megjegyzés | Ellenőrzés |
|---|---|---|
| Windows 10 vagy 11 | A LocalDB csak Windowson fut. | – |
| [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) | A `global.json` nem rögzít SDK-verziót, bármely 10.0.x megfelel. | `dotnet --version` → `10.0.…` |
| SQL Server Express LocalDB | A Visual Studio „ASP.NET and web development” munkaterhelésével települ, vagy önállóan az SQL Server Express telepítőjéből (LocalDB, `SqlLocalDB.msi`). Docker nem kell. | `sqllocaldb info MSSQLLocalDB` |
| [Ollama](https://ollama.com/download) és a `gemma3:4b` modell | A kategóriajavaslathoz kell. A modell kb. 3,4 GB. | `ollama list` |
| Git | A klónozáshoz. | `git --version` |

Ollama nélkül is elindul és működik az alkalmazás. Ekkor az „AI-javaslat kérése” gomb nem ad javaslatot, hanem ezt
írja ki: „Most nem sikerült kategóriát javasolni, válassz kézzel.” A kategóriát ilyenkor kézzel kell kiválasztani
(BR-08).

## Indítás lépésről lépésre

1. A forráskód letöltése:

   ```bash
   git clone <a repository URL-je> ServiceDesk
   cd ServiceDesk
   ```

2. A helyi .NET-eszközök visszaállítása. A [.config/dotnet-tools.json](.config/dotnet-tools.json) a `dotnet-ef`
   eszközt rögzíti, ez kell a migrációs parancsokhoz:

   ```bash
   dotnet tool restore
   ```

3. A LocalDB-példány ellenőrzése. Az `MSSQLLocalDB` példány az első kapcsolódáskor magától elindul. Ha a parancs
   szerint a példány nem létezik, hozd létre és indítsd el:

   ```bash
   sqllocaldb info MSSQLLocalDB
   ```

   ```bash
   sqllocaldb create MSSQLLocalDB -s
   ```

4. Az Ollama és a modell. Az Ollama telepítés után a háttérben fut, a `http://localhost:11434` címen. A modellt
   egyszer kell letölteni:

   ```bash
   ollama pull gemma3:4b
   ```

5. Az alkalmazás indítása:

   ```bash
   dotnet run --project src/ServiceDesk.Web
   ```

   - Az alapértelmezett `http` indítási profil Development környezetben, a `http://localhost:5123` címen indít
     ([launchSettings.json](src/ServiceDesk.Web/Properties/launchSettings.json)).
   - **Az első indítás létrehozza és feltölti az adatbázist.** Developmentben az indítás előbb lefuttatja a
     migrációkat (`Database.MigrateAsync()`), ez létrehozza a `ServiceDesk` adatbázist a LocalDB-ben. Utána az
     idempotens `DatabaseSeeder` felveszi a két szerepkört, az öt felhasználót, az öt kategóriát és a három
     ügyintézői profilt. A további indításokon a seeder csak a hiányzó elemeket pótolja.
   - A kapcsolati string a [src/ServiceDesk.Web/appsettings.Development.json](src/ServiceDesk.Web/appsettings.Development.json)
     fájlban van: `Server=(localdb)\MSSQLLocalDB;Database=ServiceDesk;…`.

6. Nyisd meg a `http://localhost:5123` címet, és jelentkezz be az egyik seed-felhasználóval (következő szakasz).

HTTPS-en a `https` profillal indul, a `https://localhost:7133` címen. Ehhez egyszer meg kell bízni a fejlesztői
tanúsítványban:

```bash
dotnet dev-certs https --trust
```

```bash
dotnet run --project src/ServiceDesk.Web --launch-profile https
```

A fejlesztői adatbázis bármikor újrakezdhető: töröld, és a következő indítás újra létrehozza és feltölti. A törlés
minden jegyet és felhasználót eltávolít; a parancs megerősítést kér, a `--dry-run` kapcsolóval csak kiírja, melyik
adatbázist törölné:

```bash
dotnet ef database drop -p src/ServiceDesk.Infrastructure -s src/ServiceDesk.Web
```

Developmenten kívül nincs automatikus migráció és nincs demóadat, csak a két szerepkör jön létre indításkor
([ADR-009](docs/adr/ADR-009-roles-in-every-environment.md)). Ilyenkor a sémát előre kell telepíteni:

```bash
dotnet ef database update -p src/ServiceDesk.Infrastructure -s src/ServiceDesk.Web
```

## Seed-felhasználók

| Felhasználó | Szerepkör | Ügyintézői profil |
|---|---|---|
| `admin.anna@servicedesk.local` | Admin | Kiss Anna – HARDWARE, NETWORK, aktív |
| `admin.bela@servicedesk.local` | Admin | Nagy Béla – SOFTWARE, ACCESS, aktív |
| `admin.csaba@servicedesk.local` | Admin | Tóth Csaba – NETWORK, SOFTWARE, OTHER, aktív |
| `bejelento.dora@servicedesk.local` | Reporter | – |
| `bejelento.erik@servicedesk.local` | Reporter | – |

- **Jelszó:** mind az öt felhasználónak ugyanaz. Az értéke a `Seed:DefaultPassword` kulcs a
  [src/ServiceDesk.Web/appsettings.Development.json](src/ServiceDesk.Web/appsettings.Development.json) fájlban.
- A seeder csak a hiányzó felhasználót hozza létre. A kulcs későbbi átírása a már létező felhasználók jelszavát nem
  változtatja meg.
- Ez csak fejlesztői demóadat; élesben a demóseeder nem fut.
- Kategóriák (mind aktív): `HARDWARE` Hardver, `SOFTWARE` Szoftver, `NETWORK` Hálózat, `ACCESS` Jogosultság és
  hozzáférés, `OTHER` Egyéb.
- Regisztrálni is lehet („Regisztráció” menüpont). Az új felhasználó mindig `Reporter` szerepkört kap, `Admin`
  csak seedből jön létre.
- A jelszó legalább 10 karakter; 5 hibás próbálkozás után a fiók 15 percre zárolódik
  ([ADR-005](docs/adr/ADR-005-identity-without-email-confirmation.md)).

## Váltás OpenAI-kompatibilis szolgáltatóra

Az AI-beállítások az [src/ServiceDesk.Web/appsettings.json](src/ServiceDesk.Web/appsettings.json) `Ai`
szekciójában vannak. Az alapértelmezés: `Provider = Ollama`, `gemma3:4b`, 20 másodperces időkorlát, `v1` prompt.

Más szolgáltatóra a repó módosítása nélkül, User Secrets-szel lehet váltani. A Web projekt már tartalmaz
`UserSecretsId`-t. A parancsokat a repó gyökeréből kell futtatni:

```bash
dotnet user-secrets set "Ai:Provider" "OpenAICompatible" --project src/ServiceDesk.Web
```

```bash
dotnet user-secrets set "Ai:OpenAICompatible:Endpoint" "https://api.openai.com/v1" --project src/ServiceDesk.Web
```

```bash
dotnet user-secrets set "Ai:OpenAICompatible:Model" "<modellnév>" --project src/ServiceDesk.Web
```

```bash
dotnet user-secrets set "Ai:OpenAICompatible:ApiKey" "<API-kulcs>" --project src/ServiceDesk.Web
```

- Az `Endpoint` bármely OpenAI-kompatibilis végpont lehet; egy helyi szerveré például `http://localhost:1234/v1`.
- **Az API-kulcs csak User Secrets-ben vagy környezeti változóban lehet, `appsettings*.json`-ban és a repóban TILOS**
  (AGENTS.md §8). Környezeti változóként a kulcs neve `Ai__OpenAICompatible__ApiKey`; a többi kulcs ugyanígy, `__`
  elválasztóval adható meg.
- Az indításkor futó `AiOptionsValidator` (`ValidateOnStart`) ellenőrzi a kiválasztott szolgáltató végpontját,
  modelljét és API-kulcsát. Hiányzó vagy hibás érték esetén az alkalmazás el sem indul, és a hibaüzenet megnevezi a
  kulcsot.
- A beállított értékek listázása, illetve visszaváltás Ollamára:

  ```bash
  dotnet user-secrets list --project src/ServiceDesk.Web
  ```

  ```bash
  dotnet user-secrets remove "Ai:Provider" --project src/ServiceDesk.Web
  ```

- Felhős szolgáltatónál a jegy címe és leírása elhagyja a gépet. Az e-mail-címeket a prompt kitakarja, felhasználó-
  azonosító nem kerül a modellhez ([ADR-007](docs/adr/ADR-007-llm-provider-and-prompt-injection.md),
  [docs/ai/prompt-v1.md](docs/ai/prompt-v1.md)).
- A javaslatkérés felhasználónként legfeljebb 5 kérés/perc.

## Tesztek

A tesztek a megoldásfájllal futnak. A paraméter nélküli `dotnet test` nem futtat tesztet („Zero tests ran”), mert a
repó a Microsoft.Testing.Platform futtatót használja ([global.json](global.json)).

```bash
dotnet test --solution ServiceDesk.slnx
```

Egy üzleti szabály tesztjei a `[Trait("BR", "BR-xx")]` jelölés alapján szűrhetők:

```bash
dotnet test --solution ServiceDesk.slnx --filter "BR=BR-03"
```

Ugyanez az xUnit v3 saját szűrőjével:

```bash
dotnet test --solution ServiceDesk.slnx -- --filter-trait "BR=BR-03"
```

Egyetlen tesztprojekt:

```bash
dotnet test --project tests/ServiceDesk.Domain.Tests
```

| Projekt | Tartalom | Külső függőség |
|---|---|---|
| `ServiceDesk.Domain.Tests` | entitások, állapotgép, prioritás, kiosztási stratégia | – |
| `ServiceDesk.Application.Tests` | handlerek NSubstitute-tal, autorizáció, validátorok | – |
| `ServiceDesk.Infrastructure.Tests` | repository-k, lekérdezések, seeder, konkurencia, AI-adapter | LocalDB: `ServiceDesk_Tests` |
| `ServiceDesk.Web.Tests` | bUnit-komponenstesztek, HTTP-szintű tesztek | LocalDB: `ServiceDesk_WebTests` (Production-indítás) |
| `ServiceDesk.ArchitectureTests` | a rétegek függőségi szabályai (NetArchTest) | – |

- A tesztadatbázisokat a tesztek maguk hozzák létre és migrálják; a fejlesztői `ServiceDesk` adatbázist nem érintik.
- A tesztekhez nem kell Ollama: az AI-adapter tesztjei helyettesített `IChatClient`-tel futnak.
- Melyik teszt melyik szabályt fedi: [docs/traceability.md](docs/traceability.md).

## Hibaelhárítás

| Tünet | Teendő |
|---|---|
| Indításkor `SqlException`: „A network-related or instance-specific error…” | A LocalDB nincs telepítve vagy nem fut: `sqllocaldb info MSSQLLocalDB`, szükség esetén `sqllocaldb create MSSQLLocalDB -s`. |
| „Most nem sikerült kategóriát javasolni, válassz kézzel.” | Nem fut az Ollama, vagy hiányzik a modell (`ollama list`). Az első kérés a modell betöltése miatt lassabb; ha nem fér bele a 20 másodpercbe, az `Ai:TimeoutSeconds` növelhető. |
| „Túl sok javaslatkérés, próbáld újra egy perc múlva, vagy válassz kézzel.” | A felhasználónkénti korlát (5 kérés/perc) betelt. |
| Indításkor `OptionsValidationException` egy `Ai:…` kulccsal | Hibás vagy hiányos az `Ai` szekció, illetve a User Secrets; a hibaüzenet megnevezi a kulcsot. |
| Warning a naplóban: `Failed to determine the https port for redirect.` | A `http` profilnál várható, nincs teendő; HTTPS-hez a `https` profillal indíts. |
| A `5123`-as port foglalt | Állítsd le a másik példányt, vagy indíts más címen: `dotnet run --project src/ServiceDesk.Web --urls http://localhost:5200`. |

## Dokumentáció

| Dokumentum | Tartalom |
|---|---|
| [AGENTS.md](AGENTS.md) | a projekt kötelező szabályrendszere: stack, rétegek, üzleti szabályok (BR-01…BR-13) |
| [docs/architecture.md](docs/architecture.md) | rétegek, függőségek, egy use case útja |
| [docs/traceability.md](docs/traceability.md) | üzleti szabály → megvalósítás → tesztek |
| [docs/demo-script.md](docs/demo-script.md) | 10 perces bemutató forgatókönyve |
| [docs/adr/README.md](docs/adr/README.md) | az architekturális döntések (ADR) listája |
| [docs/prompts/README.md](docs/prompts/README.md) | a fejlesztési lépések prompt-naplóinak indexe |
| [docs/ai/prompt-v1.md](docs/ai/prompt-v1.md) | a futásidejű LLM-prompt `v1` változata |
| [docs/security-review.md](docs/security-review.md) | biztonsági átvizsgálás és ismert korlátok (P-9A) |
