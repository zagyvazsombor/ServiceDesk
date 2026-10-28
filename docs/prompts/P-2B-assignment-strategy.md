# P-2B – Kiosztási stratégia
- Dátum: 2026-10-28
- Eszköz és modell: Claude Code (asztali alkalmazás), Claude Opus 5.5 (`claude-opus-5-5`)
- Cél és érintett szabályok: kiosztási jelölt, terhelésszámítás és a legkisebb terhelésű ügyintézőt választó stratégia
  a domainben; BR-04 (csak hivatkozás: a szűrés a hívóé, az `AssignTo` újraellenőrzi), BR-05, BR-06; AGENTS.md §1,
  §5, §10, §12, §13, §14
- Átadott kontextus: `AGENTS.md`, `CLAUDE.md`, `docs/prompts/P-2A-domain-ticket.md`,
  `docs/adr/ADR-004-rich-domain-model.md`, a P-2A domainkódja (`Agent.cs`, `PriorityMatrix.cs`, `Ticket.cs`)
  és tesztjei (`TestData.cs`, `PriorityMatrixTests.cs`, `TicketAssignmentTests.cs`)

## Prompt
[P-2B] Kiosztási stratégia
Kontextus: AGENTS.md §5 (Domain), §10 (BR-04, BR-05, BR-06). A P-2A elkészült.

Feladat:
1. AgentCandidate rekord: AgentId, LastAssignedAt, OpenTicketPriorities (a nyitott, azaz Assigned és InProgress jegyek prioritásai).
2. A terhelés számítása a §10 súlyaival, a domainben (nem adatbázisban).
3. IAssignmentStrategy és LeastLoadedAssignmentStrategy: Select(IReadOnlyList<AgentCandidate>) → AgentId?, a BR-05 sorrendjével (terhelés, LastAssignedAt – a null a legrégebbi –, végül Id). Üres listánál null (BR-06).
4. A stratégia csak jelöltet választ; a BR-04-et a Ticket.AssignTo is ellenőrzi.
5. Tesztek (BR-05, BR-06): választás terhelés szerint; egyezésnél LastAssignedAt szerint; a null LastAssignedAt elsőbbsége; teljes egyezésnél Id szerint; üres lista; súlyozás (egy Critical nagyobb terhelés, mint egy Low és egy Planning együtt).

Elfogadás: minden teszt zöld, a determinisztikus sorrendet teszt rögzíti.

Zárás: AGENTS.md §1 és §14, napló: P-2B-assignment-strategy.

Jóváhagyás a terv után (eltéréssel):
Jóváhagyom, egy eltéréssel:
- K1: az alternatíva. A metódus neve SelectAgent, elnyomás nélkül. A naplóban rögzítsd, hogy a CA1716 miatt tértél el a prompt Select nevétől.
- A többi döntést (Domain/Assignment mappa, Guid-rendezés a domainben, nincs külön ADR, negatív próba) elfogadom.
Mehet a megvalósítás.

## Eredmény
- Létrehozott és módosított fájlok:
  - `src/ServiceDesk.Domain/Assignment/` (új mappa, névtér `ServiceDesk.Domain.Assignment`; a csproj nem változott):
    `AgentCandidate.cs`, `IAssignmentStrategy.cs`, `LeastLoadedAssignmentStrategy.cs`.
  - `tests/ServiceDesk.Domain.Tests/Assignment/`: `AgentCandidateTests.cs`, `LeastLoadedAssignmentStrategyTests.cs`.
  - Ez a napló. Meglévő fájl nem változott.
- Döntések és indoklásuk:
  - **A felhasználó döntései (a terv jóváhagyásakor):**
    - **K1 – `SelectAgent` a prompt `Select` neve helyett, a CA1716 miatt.** Repón kívüli próbaprojektben ellenőriztem:
      az `AnalysisMode=Recommended` és a `TreatWarningsAsErrors` mellett az interfész `Select` tagja build-hibát ad
      (`CA1716`: a `Select` Visual Basic-kulcsszó, és interfész-tag neve nem ütközhet vele). A felhasználó döntése
      szerint a metódus neve `SelectAgent`, elnyomás (`[SuppressMessage]`) nélkül.
    - Mappa: `Domain/Assignment`. A három típus a jegyet (`Priority`) és az ügyintézőt is érinti, egyik meglévő
      mappába sem illik tisztán.
    - Külön ADR nem készült: a „terhelés a domainben, nem az adatbázisban” döntést maga a prompt hozza, és
      a rétegszerkezet nem változott.
  - **`AgentCandidate`:**
    - `sealed record` a prompt szerinti három mezővel; az `OpenTicketPriorities` `IReadOnlyList<Priority>`.
      `null` lista → `ArgumentNullException` (programozási hiba).
    - A terhelés (`Load`) számított tulajdonság: a prioritások súlyainak összege a P-2A-ban már meglévő
      `PriorityMatrix.GetLoadWeight` függvénnyel (Critical 5 … Planning 1), így a súlytábla egy helyen marad.
      Ismeretlen `Priority` érték esetén az `ArgumentOutOfRangeException` innen jön, a P-2A-val egyezően.
    - A rekord nem ellenőrzi, hogy a prioritások valóban nyitott (`Assigned`, `InProgress`) jegyekhez tartoznak; a lista
      összeállítása a hívó dolga (lásd Ismert korlátok).
  - **`IAssignmentStrategy` / `LeastLoadedAssignmentStrategy`:**
    - `Guid? SelectAgent(IReadOnlyList<AgentCandidate> candidates)`. A stratégia csak választ: feltételezi, hogy
      a lista már csak jogosult (aktív, kompetens – BR-04) ügyintézőket tartalmaz. Ezt az interfész kommentje rögzíti,
      és a `Ticket.AssignTo` a BR-04-et újra ellenőrzi.
    - Üres lista → `null` (`// BR-06`); `null` lista → `ArgumentNullException`.
    - Rendezés (`// BR-05`): `Load` növekvő, aztán `LastAssignedAt.HasValue` (a `null` előre), aztán `LastAssignedAt`
      növekvő, végül `AgentId`. Az utolsó kulcs egyedi, így az eredmény a bemenet sorrendjétől független.
    - A `DateTimeOffset` összehasonlítása az UTC-időpont szerint történik, így az eltérő időzóna-eltolás nem zavarja a
      sorrendet.
  - **Guid-rendezés a domainben:** az `AgentId` a .NET `Guid.CompareTo` szerint rendeződik, memóriában. Az SQL Server
    `uniqueidentifier`-rendezése ettől eltér, ezért a holtverseny-feloldást nem szabad lekérdezésbe áthelyezni; az
    Application rétegnek a jelölteket kell betöltenie, a választás a domainben marad.
  - **Tesztek felépítése:** minden stratégia-teszt úgy van összeállítva, hogy a döntő kulcs utáni kulcsok más ügyintézőt
    választanának (például a legkisebb terhelésűnek a legfrissebb a `LastAssignedAt`-je és a legnagyobb az Id-ja).
    Az Id-k rögzített Guidok (`…0001` – `…0004`), az időpontok a `TestData.Now` / `TestData.Later`, illetve
    a `DateTimeOffset.MinValue` (a `null` ennél is előbbre kerül).

## Ellenőrzés
- dotnet build: Build succeeded – 0 Warning(s), 0 Error(s) (`--no-incremental` újrafordítással is).
- dotnet test: Passed – 201/201 zöld (Domain.Tests 188, ArchitectureTests 12, Infrastructure.Tests 1; az Application.Tests
  és a Web.Tests még üres). Az architektúra-tesztek változatlanul zöldek, köztük a
  `Domain_Assembly_ReferencesOnlySharedFrameworkAssemblies`.
  - Új tesztek (20): `AgentCandidateTests` 7 (üres lista, 4 súlyösszeg, Critical > Low + Planning, `null` lista),
    `LeastLoadedAssignmentStrategyTests` 13 (terhelés, súlyozás, régebbi `LastAssignedAt`, `null` elsőbbsége,
    teljes egyezés Id szerint 2 esettel, 4 bemeneti sorrend, üres lista, egy jelölt, `null` lista).
  - `dotnet test --filter "BR=BR-05"`: zöld, 23 teszt (ebből 16 új; 7 a P-2A-ból: 5 súlyteszt és 2 `LastAssignedAt`-teszt).
  - `dotnet test --filter "BR=BR-06"`: zöld, 2 teszt.
- Negatív próba (a `LeastLoadedAssignmentStrategy.cs` ideiglenes módosítása, utána visszaállítás és ellenőrzés `diff`-fel):
  - a két `LastAssignedAt` kulcs törlése → 6 piros (régebbi `LastAssignedAt`, `null` elsőbbsége, 4 bemeneti sorrend);
  - az `AgentId` kulcs törlése → 4 piros (2 teljes egyezés, 2 bemeneti sorrend);
  - a `null` a sor végére (`!HasValue`) → 5 piros (`null` elsőbbsége, 4 bemeneti sorrend);
  - csak a `HasValue` kulcs törlése → 0 piros. Ennek oka, hogy a `Nullable<T>` alapértelmezett összehasonlítója is
    a `null`-t teszi előre, így ez a kulcs a viselkedést nem változtatja, csak kifejezetté teszi a szándékot.
  - A visszaállítás után ismét 201/201 zöld.
- Javasolt kézi ellenőrzés:
  - A `LeastLoadedAssignmentStrategyTests` eseteinek összevetése az AGENTS.md §10 BR-05 és BR-06 szövegével.
  - `dotnet test --filter "BR=BR-05"`: a lista tartalmazza-e az összes elvárt esetet.
- Ismert korlátok:
  - A stratégia nem szűr: a BR-04 szerinti jelöltlistát (aktív, kompetens ügyintézők) és a nyitott jegyek
    prioritásait az Application állítja össze (`TicketAssignmentService`, későbbi lépés). Ha a listában nem jogosult
    ügyintéző is szerepel, a kiosztást az `AssignTo` BR-04-ellenőrzése elutasítja. Ha viszont a prioritások nem csak
    nyitott jegyekből jönnek, a terhelés hibás lesz, és ezt semmi nem jelzi.
  - A P-2A napló szerint az `Unassign` BR-07 szerinti hívása „a P-2B kiosztási szolgáltatás felelőssége”. Ez a lépés csak
    a domain-stratégiát hozta; az `Unassign` hívása és a BR-07 kezelése az Application `TicketAssignmentService`-ére
    marad.
  - Ismétlődő `AgentId` a listában nincs ellenőrizve; az eredmény ilyenkor is determinisztikus.
  - Az `AgentCandidate` nem másolja le a kapott listát, és a rekord-egyenlőség a lista referenciáján alapul. A hívó
    a választás alatt nem módosíthatja a listát.

## Emberi javítás
Kézi kódmódosításra nem volt szükség. A K1 döntésben az alternatívát választottam: a metódus neve SelectAgent lett a CA1716 kódellenőrzési figyelmeztetés és a felesleges [SuppressMessage] attribútum elkerülése érdekében. A determinisztikus sorrendet ellenőrző és BR tesztek hibátlanul lefutottak.
## Commit
feat(domain): implement least loaded assignment strategy with tie-breaking rules
