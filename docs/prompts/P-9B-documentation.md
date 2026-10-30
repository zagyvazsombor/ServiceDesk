# P-9B – Dokumentáció és bemutató
- Dátum: 2026-10-30
- Eszköz és modell: Claude Code (asztali alkalmazás, Code fül), Claude Opus 5.5 (`claude-opus-5-5`)
- Cél és érintett szabályok: README, architektúra-leírás, követhetőségi mátrix, prompt- és ADR-index, 10 perces
  bemutató forgatókönyve; BR-01–BR-13 (dokumentálás, nincs kódváltozás); AGENTS.md §1, §2, §4, §7, §9, §10, §14
- Átadott kontextus: `AGENTS.md`, `CLAUDE.md`, `docs/prompts/P-1A` … `P-9A` naplók fejléce, `docs/adr/ADR-001` …
  `ADR-009`, `docs/ai/prompt-v1.md`, `docs/security-review.md`; a forrásban a `// BR-xx` jelölések és a teszteken a
  `[Trait("BR", …)]` jelölések; `Program.cs`, `appsettings*.json`, `launchSettings.json`, `global.json`,
  `.config/dotnet-tools.json`, `ServiceDesk.slnx`, `.claude/launch.json`; az `Ai/*` beállításai és validátora; a
  `UseCaseRunner`, a `CreateTicketHandler`, a `TicketAssignmentService`, a `DatabaseSeeder`; a `NewTicket`, a
  `TicketAdminPanel`, az `Agents`, a `NavMenu` és a `TicketLabels` feliratai; a `TicketErrors`, `AgentErrors`,
  `AiErrors`, `GeneralErrors` üzenetei

## Prompt
[P-9B] Dokumentáció és bemutató
Kontextus: AGENTS.md §2, §4, §7, §9, §10, §14. A P-9A elkészült.

Feladat:
1. README.md:
   - Előfeltételek: .NET 10 SDK, LocalDB, Ollama és a modell letöltése.
   - Indítás lépésről lépésre (dotnet tool restore, dotnet run); az első indítás létrehozza és feltölti az adatbázist.
   - A seed-felhasználók táblázata; a jelszó helye: appsettings.Development.json.
   - Váltás OpenAI-kompatibilis szolgáltatóra User Secrets-szel.
   - Tesztek futtatása, BR szerinti szűréssel.
2. docs/architecture.md: rétegek és függőségek Mermaid-diagrammal; egy use case útja: komponens → UseCaseRunner → handler → domain → UnitOfWork.
3. docs/traceability.md: BR-01…BR-13 → megvalósító osztály és metódus → tesztek, a [Trait] jelölések alapján.
4. docs/prompts/README.md: a P-naplók indexe (azonosító, cél, dátum, érintett BR-ek).
5. docs/adr/README.md: az ADR-ek listája egy-egy mondatos összefoglalóval.
6. docs/demo-script.md: 10 perces bemutató forgatókönyve a seed-felhasználókkal – jegy AI-javaslattal, automatikus kiosztás, lezárt jegy kiosztási kísérlete, inaktivált ügyintéző, prompt-injekciós próba.

Elfogadás: a README alapján egy tiszta gépen elindítható a projekt; minden hivatkozott fájl létezik.

Zárás: AGENTS.md §1 és §14, napló: P-9B-documentation.

## Eredmény
- Létrehozott fájlok (forráskód, teszt, `AGENTS.md` és meglévő dokumentum nem változott):
  - `README.md`:
    - előfeltételek ellenőrző parancsokkal;
    - indítás hat lépésben;
    - HTTPS-profil, adatbázis-újrakezdés, Developmenten kívüli sématelepítés;
    - seed-felhasználók;
    - OpenAI-kompatibilis szolgáltató User Secrets-szel;
    - tesztek BR-szűréssel, tesztprojektek és függőségeik;
    - hibaelhárítás, dokumentációs index.
  - `docs/architecture.md`:
    - rétegek és függőségek (Mermaid `flowchart`), tiltott irányok, a `DependencyRuleTests` tesztjei;
    - a jegyrögzítés útja (Mermaid `sequenceDiagram`): komponens → `UseCaseRunner` → handler → domain → `UnitOfWork`;
    - a háromszintű jogosultság helye;
    - az AI-javaslat útja, az indítás és az adatbázis.
  - `docs/traceability.md`: BR-enként a szabály, a megvalósítás (réteg, típus, metódus) és az összes `[Trait]`-jelölt
    tesztmetódus. Az összesítő táblában a szűrt futás tesztszáma szerepel. Összesen 300 metódus–BR pár.
  - `docs/prompts/README.md`: a P-1A … P-9B naplók indexe időrendben (azonosító, cél, dátum, BR-ek, keletkezett ADR).
  - `docs/adr/README.md`: az ADR-001 … ADR-009 listája dátummal, státusszal, a keletkezés lépésével és egymondatos
    összefoglalóval.
  - `docs/demo-script.md`:
    - előkészítés és öt jelenet időkeretekkel: AI-javaslat, automatikus kiosztás, lezárt jegy két fülön, inaktivált
      ügyintéző és visszaállítás, prompt-injekció;
    - zárás a BR-03 tesztjeivel, tartalékok.
  - `docs/prompts/P-9B-documentation.md` (ez a napló).
- Döntések és indoklásuk:
  - **Nyelv:** minden dokumentum magyar, mint a meglévő ADR-ek és naplók; a kódazonosítók eredeti alakban.
  - **Jelszó:** a README és a forgatókönyv csak a helyét adja meg (`Seed:DefaultPassword`), az értékét nem. A prompt is
    a helyet kérte, és így a dokumentáció nem duplikálja a titkot.
  - **Tesztszűrő:**
    - kipróbáltam a `--filter "BR=BR-03"` (AGENTS.md §16) és a `-- --filter-trait "BR=BR-03"` formát; mindkettő
      ugyanazt a 82 tesztet futtatja;
    - a README az AGENTS.md-beli formát adja meg elsőként, az xUnit-féle alternatívát másodikként.
  - **Követhetőség:**
    - a tesztoldalt szkript gyűjtötte ki a `[Trait]` jelölésekből (metódusszintű attribútumok);
    - a megvalósítási oldalt kézzel válogattam a `// BR-xx` jelölésekből, kiegészítve nem jelölt, de a szabályt
      érvényesítő pontokkal (hibakatalógus, `UnitOfWork`, `GetTicketQueueAsync` stb.);
    - a szkript minden megnevezett fájlt, típust és tagot ellenőrzött a forrásban; hiba nem maradt;
    - a DTO-k és a portok csak akkor szerepelnek, ha maguk hordozzák a szabályt (pl. a commandból hiányzó mezők).
    - A generáló szkript nem került a repóba, mert a prompt csak a dokumentumot kérte (AGENTS.md §1.2).
  - **K1 (a), a felhasználó döntése:** a bemutatót a fejlesztői adatbázison, a böngészőpanelen jártam végig, a
    `bejelento.dora` és az `admin.anna` seed-fiókkal. A forgatókönyv feliratai és üzenetei ennek a bejárásnak a szó
    szerinti eredményei.
  - **Mermaid:** a diagramokat a legszélesebb körben támogatott szintaxissal írtam:
    - idézőjeles csomópontcímkék;
    - `-->|"…"|` élcímkék;
    - `#lt;`/`#gt;` a `Result<Guid>`-ban;
    - pontosvessző nélküli üzenetek.
  - **ADR:** nem készült, mert a lépés nem hoz architekturális döntést.

## Ellenőrzés
- dotnet build: `dotnet build` → 0 hiba, 0 figyelmeztetés.
- dotnet test: `dotnet test --solution ServiceDesk.slnx` → **828/828 zöld** (a lépés nem változtatott kódot és tesztet).
  - BR-szűrt futások a traceability számaihoz: BR-01 38, BR-02 20, BR-03 82, BR-04 27, BR-05 32, BR-06 25, BR-07 70,
    BR-08 41, BR-09 37, BR-10 26, BR-11 23, BR-12 25, BR-13 53 teszt; mind zöld.
- Hivatkozások: szkript ellenőrizte a hat új dokumentum 327 relatív linkjét, horgonyát és útvonalát. Az egyetlen
  hiányzó cél ez a napló volt, ez most létrejött.
- A README parancsai ezen a gépen:
  - `dotnet tool restore` → „Restore was successful.”
  - `dotnet ef database drop --dry-run -p … -s …` → „This would drop the database 'ServiceDesk' on server
    '(localdb)\MSSQLLocalDB'.”
  - `dotnet user-secrets list --project src/ServiceDesk.Web` → működik (nincs beállított titok).
  - `dotnet test --project tests/ServiceDesk.Domain.Tests` → 257/257 zöld.
  - `sqllocaldb info MSSQLLocalDB` → Running; `ollama list` → `gemma3:4b`.
- Böngészős bejárás (`servicedesk-web`, Development, a meglévő adatbázison):
  - Az indításkor lefutó migráció: „No migrations were applied. The database is already up to date.”
  - **1. jelenet:**
    - az AI-javaslat [Hálózat], 95%;
    - [Elfogadom] után a rögzítés sikeres, a felelős Kiss Anna;
    - az előzmények: [Jegy létrehozva], [Kiosztás] és [Státuszváltás] a [Rendszer] nevében.
  - **2. jelenet:** az [Ügyintézők] oldal szerint a jegy előtt Anna és Csaba terhelése egyaránt 13 volt; a döntés a
    régebbi `LastAssignedAt` szerint született (BR-05).
  - **3. jelenet:**
    - az 1. fülön [Munka megkezdése] → [Megoldás] → [Lezárás]; utána nincs kiosztó gomb;
    - a 2. fülön az [Átosztás] eredménye: [Lezárt jegy nem osztható ki és nem osztható át.]
  - **4. jelenet:**
    - Béla inaktiválása után 1 nyitott jegye megmaradt;
    - az új ACCESS-jegy [Új] és [Nincs kiosztva];
    - az [Automatikus kiosztás újra] és a [Kiosztás] párbeszédablak a várt üzenetet adta;
    - Béla aktiválása után az újrafuttatás kiosztotta neki a jegyet.
  - **5. jelenet:** a beszúrt `</ticket>`-es, HACK kategóriát kérő leírásra a javaslat [Hardver] lett, 100%-kal; az
    indoklás sima szöveg.
  - A szervernaplóban nem volt Error. Az egyetlen Warning a `http` profilnál várható „Failed to determine the https
    port for redirect.”; ez bekerült a README hibaelhárítási táblájába.
- A fejlesztői adatbázisban a bejárás nyomai maradtak:
  - két demójegy: a VPN-es, lezárva; a HR-es, Nagy Bélánál;
  - két AI-javaslat: egy `Accepted`, valamint a prompt-injekciós próbáé, `Pending` állapotban, jegy nélkül.
  - Béla állapota a végén ismét aktív.
- Javasolt kézi ellenőrzés:
  - A Mermaid-diagramok megjelenítése GitHubon vagy a VS Code Markdown-előnézetében (`docs/architecture.md`, négy
    diagram).
  - A README végigkövetése egy tiszta gépen (vagy friss Windows-felhasználóval), különösen a LocalDB és az Ollama
    telepítése.
  - A bemutató főpróbája a `docs/demo-script.md` szerint, időméréssel. Ehhez opcionálisan friss adatbázis kell:
    `dotnet ef database drop`.

## Ismert korlátok
- A Mermaid-diagramok szintaxisát gép nem ellenőrizte. A böngészőben a mermaid könyvtár CDN-ről való betöltését az
  ügynök engedélykezelése külső kód futtatásaként elutasította; ezt nem kerültem meg. A szintaxist kézzel néztem át.
- A „tiszta gép” elfogadási feltételt nem tiszta gépen ellenőriztem, hanem ezen a gépen, a fenti parancsokkal és a
  meglévő adatbázison. Az üres adatbázisra történő első indítást (migráció és seed) a meglévő tesztek fedik le
  (`DatabaseSeederTests`, `ProductionStartupTests`).
- A `docs/traceability.md` és a forgatókönyv tesztszámai (828, BR-03: 82) a 2026-10-30-i állapotot mutatják. Új teszt
  vagy jelölés esetén kézzel kell frissíteni őket.
- Az AI-javaslat kimenete nem determinisztikus. A forgatókönyv ezért elfogadható kimeneteket ad meg, nem pontos
  értékeket.

## Emberi javítás
Kézi kódmódosításra nem volt szükség. A magyar nyelvű dokumentációkat (README.md, architecture.md a Mermaid-diagramokkal, traceability.md a 13 üzleti szabály teljes lefedettségével, a P-naplók és ADR-ek indexei, valamint a 10 perces demo-script.md) átnéztem és jóváhagytam. A bemutató forgatókönyvének mind az 5 jelenetét végigjártuk a futó alkalmazásban. A build tiszta, mind a 828/828 teszt változatlanul zöld.
## Commit
docs: add readme, architecture guide, traceability matrix, indexes and demo script
