# P-8A – AI-kategóriajavaslat: port, adapter, use case
- Dátum: 2026-10-29
- Eszköz és modell: Claude Code (asztali alkalmazás), Claude Opus 5.5 (`claude-opus-5-5`)
- Cél és érintett szabályok:
  - Az LLM-alapú kategóriajavaslat portjai, `SuggestCategory` use case-e, Infrastructure-adaptere
    (`IChatClient`, Ollama / OpenAI-kompatibilis) és felhasználónkénti rate limitje, a jegylétrehozás és a UI nélkül.
  - Üzleti szabályok: BR-08, BR-09 (a javaslat `Pending` állapotban mentve), BR-13.
  - AGENTS.md: §1, §3 (csomagok), §5, §8 (rate limit, titkok, naplózás), §9 (teljes), §11, §12, §13, §14.
- Átadott kontextus:
  - `AGENTS.md`, `CLAUDE.md`, `docs/prompts/P-7A-master-data.md`.
  - Domain: `CategorySuggestion.cs`, `CategorySuggestionDecision.cs`, `Category.cs`, `CategoryErrors.cs`, `Common/*`.
  - Application:
    - `Common/Errors/*`, `Common/Persistence/*` (különösen `ICategoryRepository`, `ICategorySuggestionRepository`,
      `ITicketQueries`, `IUnitOfWork`), `Common/Security/*`, `Common/Validation/ValidatorExtensions.cs`;
    - `DependencyInjection.cs`, a `CreateTicket` és a `GetActiveCategories` feature-mappa.
  - Infrastructure:
    - `DependencyInjection.cs`, `CategoryRepository.cs`, `CategorySuggestionRepository.cs`,
      `CategorySuggestionConfiguration.cs`;
    - `SeedOptions.cs`, `DatabaseSeeder.cs` (a seed-kategóriák), `DatabaseInitializer.cs`, `TicketQueries.cs`.
  - Web: `Program.cs`, `appsettings*.json`, `ServiceDesk.Web.csproj`, `UseCases/UseCaseRunner.cs`,
    `UseCases/UseCaseServiceCollectionExtensions.cs`.
  - Tesztek:
    - Application: `TestUsers`, `TestData`, `RecordingLogger`, `LogEntry`, `DependencyInjectionTests`,
      `CreateTicketHandlerTests`.
    - Infrastructure: `DatabaseFixture`, `IntegrationTest`, `CategoryRepositoryTests`, `Builders/*`.
    - ArchitectureTests: `DependencyRuleTests`, `SolutionAssemblies`.
    - Web: `Http/ServiceDeskWebApplicationFactory.cs`.
  - `Directory.Packages.props`, `Directory.Build.props`, `.editorconfig`, a projektfájlok.
  - A `Microsoft.Extensions.AI` 10.11.0, `Microsoft.Extensions.AI.OpenAI` 10.11.0 és `OllamaSharp` 5.5.0 csomag
    XML-dokumentációja és nuspec-függőségei (API- és verzióellenőrzéshez).

## Prompt
[P-8A] AI-kategóriajavaslat: port, adapter, use case
Kontextus: AGENTS.md §5, §8 (rate limit, titkok), a teljes §9, §10 (BR-08, BR-09, BR-13), §12. A 7. lépés elkészült.

Feladat:
1. Application: ICategorySuggester és ISuggestionRateLimiter port; CategorySuggestionInput és CategorySuggestionOutput a §9 szerint.
2. SuggestCategory use case (csak Reporter):
   - rate limit (5 kérés/perc/felhasználó), túllépésnél AiErrors.RateLimited (ErrorType: Unavailable);
   - aktív kategóriák betöltése, ICategorySuggester hívása;
   - CategorySuggestion mentése Pending döntéssel;
   - DTO: SuggestionId, CategoryId, kategórianév, Confidence, indoklás.
   Az AI-hiba nem kivétel, hanem AiErrors.NoSuggestion.
3. Infrastructure/Ai:
   - AiOptions ValidateOnStart-tal; szolgáltató-választás AddChatClient-tel a §9 szerint (alapértelmezés Ollama, alternatíva OpenAICompatible).
   - LlmCategorySuggester: v1 prompt a §9 szerint, GetResponseAsync<LlmCategoryAnswer>, TryGetResult, időkorlát az AiOptions.TimeoutSeconds alapján, BR-08 validáció, Warning-napló a jegy szövege nélkül.
   - ISuggestionRateLimiter: PartitionedRateLimiter, felhasználónkénti fix ablak.
4. A v1 rendszerprompt szövegét másold a docs/ai/prompt-v1.md fájlba is.
5. Tesztek:
   - Adapter hamis IChatClient-tel: érvényes válasz; nem létező vagy inaktív kód; hibás JSON; időtúllépés; Confidence 0–1-re szorítva; túl hosszú indoklás levágva.
   - Handler: Admin → Forbidden; rate limit túllépés; sikeres javaslat Pending állapotban mentve.
   - Prompt: a jegy szövege <ticket> határolók közé kerül; a kimenő üzenetben nincs e-mail-cím és felhasználó-azonosító.

Ne csináld: a jegylétrehozás módosítása és UI (P-8B); tool calling.

Elfogadás: a build és a tesztek zöldek; Ollama nélkül a javaslat NoSuggestion hibát ad, az alkalmazás nem omlik össze.

Zárás: AGENTS.md §1 és §14, napló: P-8A-ai-suggestion.

Jóváhagyás a terv után (kiegészítéssel):
Jóváhagyom, a következő döntésekkel:
- K1: az ajánlott – a küldés előtt az e-mail-címek [e-mail-cím]-re cserélődnek; teszt rá. Rögzítsd a docs/ai/prompt-v1.md-ben is.
- K2: az ajánlott – validáció a jegy mezőkorlátaival, a rate limit a validáció után.
- K3: az alternatíva – a rendszerprompt utasításai angolul (a kis lokális modellek ezt követik megbízhatóbban), a kategórianevek és -leírások magyarul, és a prompt kifejezetten magyar nyelvű indoklást kér. Teszt: a rendszerüzenet előírja a magyar indoklást.
- A D1–D8 döntéseket és az Infrastructure.Tests-nek szóló InternalsVisibleTo-t elfogadom (az adapterek internal-ok maradnak).
- A futó ServiceDesk.Web-et a záró build előtt leállítom.
Mehet a megvalósítás.

## Eredmény
- Létrehozott és módosított fájlok:
  - Csomagok:
    - `Directory.Packages.props`: `Microsoft.Extensions.AI` 10.11.0, `Microsoft.Extensions.AI.OpenAI` 10.11.0,
      `OllamaSharp` 5.5.0 (mind a §3 listájáról; a NuGet legfrissebb stabil, egymással kompatibilis verziói).
    - `src/ServiceDesk.Infrastructure/ServiceDesk.Infrastructure.csproj`: a három csomaghivatkozás és
      `InternalsVisibleTo` a `ServiceDesk.Infrastructure.Tests` számára.
  - Application, új:
    - `Common/Ai/ICategorySuggester.cs`, `CategorySuggestionInput.cs`, `SuggestionCategory.cs`,
      `CategorySuggestionOutput.cs`, `ISuggestionRateLimiter.cs`.
    - `Tickets/SuggestCategory/SuggestCategoryCommand.cs`, `SuggestCategoryValidator.cs`, `SuggestCategoryHandler.cs`,
      `CategorySuggestionDto.cs`.
  - Application, módosított:
    - `Common/Errors/AiErrors.cs`: új `RateLimited` (`Ai.RateLimited`, `Unavailable`).
    - `Common/Persistence/ICategoryRepository.cs`: új `GetActiveAsync`.
  - Infrastructure, új (`Ai/`): `AiProvider.cs`, `AiOptions.cs`, `OllamaOptions.cs`, `OpenAICompatibleOptions.cs`,
    `AiOptionsValidator.cs`, `ChatClientFactory.cs`, `LlmCategoryPrompt.cs`, `LlmCategoryAnswer.cs`,
    `LlmCategorySuggester.cs`, `SuggestionRateLimiter.cs`.
  - Infrastructure, módosított:
    - `Persistence/Repositories/CategoryRepository.cs`: `GetActiveAsync` (aktívak, kód szerint rendezve).
    - `DependencyInjection.cs`: `AddOptions<AiOptions>().Bind(…).ValidateOnStart()`, `AiOptionsValidator`,
      `AddChatClient(ChatClientFactory.Create)`, a suggester és a rate limiter singletonként,
      `TryAddSingleton(TimeProvider.System)`.
  - Web: `appsettings.json`: az `Ai` szekció a §9 értékeivel, API-kulcs nélkül. A `Program.cs` nem változott.
  - Dokumentáció: `docs/ai/prompt-v1.md` (új), `docs/prompts/P-8A-ai-suggestion.md` (ez a napló).
  - Tesztek, új:
    - `tests/ServiceDesk.Application.Tests/Tickets/SuggestCategory/SuggestCategoryHandlerTests.cs` (11 teszt).
    - `tests/ServiceDesk.Infrastructure.Tests/RecordingLogger.cs`, `LogEntry.cs` (az Application.Tests mintájára,
      a formázott üzenettel is).
    - `tests/ServiceDesk.Infrastructure.Tests/Ai/`: `FakeChatClient.cs`, `TestAi.cs`, `LlmCategorySuggesterTests.cs`,
      `LlmCategoryPromptTests.cs`, `SuggestionRateLimiterTests.cs`, `AiOptionsValidatorTests.cs`,
      `AiServiceRegistrationTests.cs` (összesen 52 teszt).
  - Tesztek, módosított: `tests/ServiceDesk.Infrastructure.Tests/Persistence/CategoryRepositoryTests.cs`
    (`GetActiveAsync`).
- Döntések és indoklásuk:
  - **K1 – e-mail-címek:** a cím és a leírás minden e-mail-címe `[e-mail-cím]`-re cserélődik, mielőtt a modellhez
    kerül, a jegybe beírt cím is. A minta Unicode-betűket is elfogad (pl. `jozsef@cég.hu`), a domain-címkék nem
    tartalmazhatnak pontot, így hosszú szövegen sincs jelentős visszalépés. Rögzítve a `docs/ai/prompt-v1.md`-ben.
  - **K2 – validáció:** a `SuggestCategoryValidator` a jegy mezőkorlátait (cím 5–200, leírás 10–4000) alkalmazza,
    a `CreateTicketValidator` üzeneteivel. A rate limit a validáció után jön, így az érvénytelen kérés nem fogyaszt
    keretet. A handler sorrendje:
    1. Reporter-autorizáció;
    2. validáció;
    3. rate limit;
    4. aktív kategóriák (ha nincs egy sem, `NoSuggestion` LLM-hívás nélkül);
    5. a suggester hívása;
    6. a visszakapott kód leképezése kategóriára;
    7. `CategorySuggestion.Create` (`Pending`);
    8. `Add` és `SaveChangesAsync`;
    9. DTO.
  - **K3 – prompt nyelve:** a rendszerprompt utasításai angolul vannak, a kategórianevek és -leírások magyarul, a
    prompt kifejezetten magyar indoklást kér („Write the reasoning in Hungarian…”). A jegy egy külön felhasználói
    üzenetben, `<ticket>`/`</ticket>` között szerepel.
  - **D1 – időkorlát:** `new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds), TimeProvider)`, a hívó
    tokenjével összekapcsolva. A teszt a `FakeTimeProvider` léptetésével determinisztikusan túllépi.
  - **D2 – modellnév:** a kimenetben (és így a `CategorySuggestion.Model`-ben) a kiválasztott szolgáltató
    konfigurált modellneve szerepel. A validátor 1–100 karakterre korlátozza, a `CategorySuggestion.ModelMaxLength`
    szerint.
  - **D3 – kódegyeztetés:** a válasz kódjáról levágódnak a szóközök, és kis- és nagybetűtől függetlenül egyeztetjük
    a kapott (aktív) kategóriákkal; a kategória saját kódja megy tovább. A modell nyers kódját nem naplózzuk, mert
    tetszőleges szöveg lehet.
  - **D4 – indoklás:** a vezérlőkarakterek szóközzé válnak, a szöveg levágott szóközökkel legfeljebb 500 karakter,
    és surrogate pár nem törik ketté. A szöveg sosem kerül `MarkupString`-be.
  - **D5 – Confidence:** NaN esetén `NoSuggestion`, egyébként `Math.Clamp(…, 0, 1)`.
  - **D6 – hibák:** a hívó által kért megszakítás `OperationCanceledException`-ként továbbmegy; a `UseCaseRunner`
    ezt nem tekinti hibának. Minden más kivétel `NoSuggestion` és Warning: a kivétel objektuma és a modellnév
    naplózódik, a jegy szövege és a modell válasza nem.
  - **D7 – határolók:** a szövegben lévő `<ticket>`/`</ticket>` (bármilyen kis- és nagybetűvel, szóközökkel)
    `[ticket]`/`[/ticket]` lesz, így a jegy szövege nem zárhatja le idő előtt az adatblokkot.
  - **D8 – konfiguráció helye:** az `Ai` szekció az `appsettings.json`-ban van, mert a Web HTTP-tesztjei Production
    környezetben indítják a hostot, és a `ValidateOnStart` különben elbuktatná őket. Kulcs nincs benne; az
    `Ai:OpenAICompatible:ApiKey` csak User Secretsből vagy környezeti változóból jöhet, és OpenAICompatible
    szolgáltatónál a validátor megköveteli.
  - **`AiOptionsValidator`:** csak a kiválasztott szolgáltató szekcióját ellenőrzi:
    - abszolút http(s) végpont;
    - modellnév;
    - OpenAICompatible esetén API-kulcs.

    Ezen felül `TimeoutSeconds` > 0, és `PromptVersion` = `v1` (a kódban csak ez a prompt van).
  - **Szolgáltató-választás:** a `ChatClientFactory` csak az első feloldáskor olvassa az opciókat, szó szerint a §9
    két konstrukciójával. Így a konfiguráció nélküli Infrastructure-tesztfixture-t a regisztráció nem érinti.
  - **A kérés beállításai:**
    - `useJsonSchemaResponseFormat: true`, így a kimenő üzenetek pontosan a promptéi, nem kerül hozzá sémaüzenet;
    - `Temperature = 0`, mert osztályozásról van szó;
    - `Tools` nincs beállítva.
  - **Rate limiter:** `PartitionedRateLimiter` felhasználó-azonosító szerinti fix ablakkal (5 engedély/perc,
    `QueueLimit = 0`, automatikus feltöltés). Singleton, így a felhasználó minden circuitje ugyanazt az ablakot
    használja. A DI dispose-olja.
  - **`GetActiveAsync`:** új repository-metódus, mert a `CategorySuggestion.Create` entitást vár. Egy lekérdezéssel
    adja az aktív kategóriákat, kód szerint rendezve, így a prompt kategórialistája stabil.
  - **Elhelyezés:** a portok az `Application/Common/Ai`-ban vannak, a use case a `Tickets/SuggestCategory`-ban (a
    `GetActiveCategories` mellett, mert a létrehozó űrlapot szolgálja ki). Az adapterek `internal`-ok, a tesztek az
    `InternalsVisibleTo`-n át érik el őket.
  - **Migráció és ADR:** a modell nem változott (a `CategorySuggestions` tábla a korábbi migrációban létrejött),
    ezért migráció nem kellett. ADR sem, mert az architekturális döntéseket a §9 rögzíti.

## Ellenőrzés
- dotnet build: `dotnet build --no-incremental` → 0 Warning(s), 0 Error(s). A futó `ServiceDesk.Web` leállítása után
  futott, így a korábbi MSB3026 fájlzárolási figyelmeztetések már nem jelentkeztek.
- dotnet test: `dotnet test --solution ServiceDesk.slnx` → 754/754 zöld (Domain, Application, Infrastructure, Web,
  ArchitectureTests). Ebből 64 új:
  - 11 Application-teszt;
  - 52 AI-teszt az Infrastructure.Testsben;
  - 1 új `CategoryRepositoryTests`-eset.
- Lefedett követelmények:
  - Adapter:
    - érvényes válasz;
    - nem létező, inaktív és üres kód;
    - hibás JSON (6 eset);
    - időtúllépés, és válasz közvetlenül az időkorlát előtt;
    - elérhetetlen modell;
    - a hívó megszakítása;
    - a `Confidence` szorítása (6 eset);
    - levágás 500 karakterre, surrogate pár, vezérlőkarakterek;
    - a Warning-naplóban nincs jegyszöveg;
    - JSON-séma tool nélkül.
  - Handler:
    - névtelen felhasználó, Admin és szerepkör nélküli felhasználó → `Forbidden`;
    - validáció (a keretet nem fogyasztja);
    - rate limit;
    - nincs aktív kategória;
    - `NoSuggestion`, illetve nem aktív kód → nincs mentés;
    - `Pending` mentés és DTO;
    - a modellhez küldött bemenet (vágott szöveg, aktív kategóriák, felhasználó-azonosító nélkül);
    - naplózás.
  - Prompt:
    - `<ticket>` határolók, külön felhasználói üzenet;
    - kategórialista és szabályok;
    - magyar indoklás előírása (K3);
    - a határolók semlegesítése (D7);
    - az e-mail-címek cseréje (K1);
    - a kimenő kérésben nincs e-mail-cím és felhasználói azonosság;
    - a `prompt-v1.md` egyezik a konstanssal.
  - Egyéb:
    - a rate limiter (6. kérés elutasítva, másik felhasználót nem érint);
    - az opciók validátora;
    - a DI szolgáltató-választás (`ChatClientMetadata`: `ollama` / `openai`);
    - a `ValidateOnStart` (`IStartupValidator`).
- Elfogadási feltétel (Ollama nélkül): az `AddInfrastructure_OllamaNotRunning_SuggestionReturnsNoSuggestion` teszt a
  valódi `OllamaApiClient`-et egy olyan helyi portra küldi, ahol nem figyel semmi. Az eredmény `NoSuggestion`, kivétel
  nélkül (~2 s). Az alkalmazás Development környezetben elindult (`dotnet run --launch-profile http`; DI-validáció,
  migráció, seed, „Application started”), hibanapló nélkül.
- Javasolt kézi ellenőrzés:
  - `ollama pull gemma3:4b`, majd a P-8B felületén néhány valós jegyszöveggel ellenőrizni, hogy a `gemma3:4b` létező
    kódot és magyar indoklást ad-e. Ebben a lépésben valódi modellel nem futott kérés.
  - Ollama leállítva az alkalmazás indul, a javaslat `NoSuggestion` (P-8B: kézi választás).
  - `"Ai": { "Provider": "OpenAICompatible" }` kulcs nélkül: az alkalmazás indításkor `OptionsValidationException`-nel
    áll le, és az `Ai:OpenAICompatible:ApiKey`-t nevezi meg. Kulcs beállítása:
    `dotnet user-secrets set "Ai:OpenAICompatible:ApiKey" "<kulcs>" --project src/ServiceDesk.Web`.
- Ismert korlátok:
  - A `System.Threading.RateLimiting` nem fogad `TimeProvider`-t, ezért az ablak valós idő szerint telik. A tesztek
    csak az egy ablakon belüli korlátot ellenőrzik, az ablak lejártát nem. A limiter folyamaton belüli, több
    alkalmazáspéldánynál példányonként számolna (a projekt egypéldányos).
  - A keretet a sikertelen (`NoSuggestion`) kérés is fogyasztja, mert a modellhívás megtörtént vagy megkísérlődött.
  - Az e-mail-felismerés mintaalapú: idézőjeles helyi rész vagy IP-literál domain nem cserélődik. Más személyes adat
    (név, telefonszám) a §9 szerint nem tiltott, ezért nem cserélődik.
  - Az indoklásból a HTML-jelölés nem törlődik, csak a vezérlőkarakterek. A UI szövegként jeleníti meg, `MarkupString`
    nélkül.
  - A kivétel üzenete (D6) a HTTP- vagy kliensrétegből jön. A jegy szövegét nem tartalmazza, de egy szolgáltató
    hibaüzenete elvben visszaidézheti a kérés egy részét.
  - A javaslat `Pending` marad, amíg a P-8B létrehozó űrlapja nem rögzíti a döntést. Az el nem döntött javaslatok
    (pl. elhagyott űrlap) az adatbázisban maradnak.

## Emberi javítás
Kézi kódmódosításra nem volt szükség. Mind a 754/754 teszt zölden lefutott.
## Commit
feat(ai): implement llm category suggestion port, adapter, rate limiter and use case
