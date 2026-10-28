# P-2A – Domain-modell: jegy, állapotgép, prioritás
- Dátum: 2026-10-28
- Eszköz és modell: Claude Code (asztali alkalmazás), Claude Opus 5.5 (`claude-opus-5-5`)
- Cél és érintett szabályok: `Result`/`Error`, enumok és prioritásmátrix, `Category`, `Agent`, a `Ticket` aggregátum
  állapotgéppel és jegytörténettel, hibakatalógusok, domaintesztek; BR-01 (domainrész), BR-03, BR-04, BR-05
  (`LastAssignedAt`, súlyok), BR-07 (állapotfeltétel), BR-10, BR-11, BR-12, BR-13; AGENTS.md §1, §5, §10, §11,
  §12, §13, §14
- Átadott kontextus: `AGENTS.md`, `CLAUDE.md`, `docs/prompts/P-1A-solution-skeleton.md`,
  `docs/prompts/P-1B-identity-and-shell.md`, `docs/adr/ADR-002-clean-architecture.md`, `docs/adr/ADR-003-sql-server-localdb.md`

## Prompt
[P-2A] Domain-modell: jegy, állapotgép, prioritás
Kontextus: AGENTS.md §5 (Domain), §10, §11, §12. Csak a ServiceDesk.Domain és a ServiceDesk.Domain.Tests projektet módosítsd.

Feladat:
1. Domain/Common: Result, Result<T>, Error(Code, Message, Type), ErrorType (§11).
2. Enumok: TicketStatus, Impact, Urgency, Priority. PriorityMatrix.Calculate(Impact, Urgency) és a prioritás-súly a §10 táblázatai szerint.
3. Category: Create, Rename, UpdateDescription, Activate, Deactivate (BR-13); kódformátum és hosszkorlátok (§10).
4. Agent: UserId, DisplayName, IsActive, LastAssignedAt, AgentCompetency-gyűjtemény; HasCompetency, SetCompetencies, Activate, Deactivate (BR-12), RecordAssignment(now).
5. Ticket aggregate: Ticket.Create(reporterId, title, description, impact, urgency, categoryId, now); metódusok: ChangeImpactAndUrgency, Recategorize, AssignTo(agent, actorUserId, now), Unassign (csak a BR-07 esethez), Start, Resolve, Reopen, Close. A prioritás mindig számított (BR-11).
6. TicketHistoryEntry a Ticket-en belül (BR-10): ActorUserId (null = rendszer), At, Action, OldValue, NewValue.
7. TicketErrors, AgentErrors, CategoryErrors magyar üzenetekkel; a szabályt kódoló sor mellett // BR-xx.
8. Tesztek [Trait("BR", …)] jelöléssel: a BR-01 domainrésze (mezőkorlátok), BR-03, BR-04, BR-10, BR-11, BR-12, BR-13 pozitív és negatív esetei; az állapotgép összes (állapot, művelet) párja egy táblázatos [Theory]-ben; a prioritásmátrix mind a 9 cellája.

Ne csináld: kiosztási stratégia (P-2B), perzisztencia, Application-kód.

Elfogadás:
- A Domain projektnek nincs külső csomag- és projektreferenciája.
- Minden teszt és az architektúra-tesztek zöldek.

Zárás: AGENTS.md §1 és §14, napló: P-2A-domain-ticket.

Jóváhagyás a terv után (eltérésekkel):
A tervet jóváhagyom a következő eltérésekkel:
- K1: az alternatíva. A Ticket.Create és a Ticket.Recategorize Category paramétert kap; inaktív kategóriára CategoryErrors.Inactive hibát ad (// BR-13), a jegy pedig csak a CategoryId-t tárolja. A BR-13-hoz írj domaintesztet erre az esetre is.
- K2 és K3: a javaslat szerint.
- A többi javasolt döntést elfogadom (trimelés, kivételek, idempotens Activate/Deactivate, RecordAssignment az AssignTo-ban, változás nélküli hívás történetbejegyzés nélkül).
- Pótlás: írd meg a docs/adr/ADR-004-rich-domain-model.md fájlt (gazdag domain modell, Result minta, a végrehajtó és az idő paraméterként; Alternatívák: anemikus modell + service-ek, kivételalapú hibakezelés).
Mehet a megvalósítás.

## Eredmény
- Létrehozott és módosított fájlok:
  - `src/ServiceDesk.Domain` (új fájlok; a csproj nem változott, nincs csomag- és projektreferencia):
    - `Common/`: `ErrorType.cs`, `Error.cs`, `Result.cs`, `ResultOfT.cs`;
    - `Tickets/`: `TicketStatus.cs`, `Impact.cs`, `Urgency.cs`, `Priority.cs`, `PriorityMatrix.cs`, `Ticket.cs`,
      `TicketHistoryEntry.cs`, `TicketHistoryAction.cs`, `TicketErrors.cs`;
    - `Categories/`: `Category.cs`, `CategoryErrors.cs`;
    - `Agents/`: `Agent.cs`, `AgentCompetency.cs`, `AgentErrors.cs`.
  - `tests/ServiceDesk.Domain.Tests`:
    - csproj: `Shouldly` hivatkozás (a verzió már a `Directory.Packages.props`-ban volt);
    - `TestData.cs` (közös tesztadatok, a jegy adott állapotba léptetése a fő úton);
    - `Common/ResultTests.cs`, `Tickets/PriorityMatrixTests.cs`, `Tickets/TicketCreateTests.cs`,
      `Tickets/TicketStateMachineTests.cs`, `Tickets/TicketAssignmentTests.cs`, `Tickets/TicketChangeTests.cs`,
      `Tickets/TicketHistoryTests.cs`, `Categories/CategoryTests.cs`, `Agents/AgentTests.cs`.
  - `docs/adr/ADR-004-rich-domain-model.md` (a felhasználó kérésére), ez a napló.
- Döntések és indoklásuk:
  - **A felhasználó döntései (a terv jóváhagyásakor):**
    - **K1 – aktív kategória a domainben.** A `Ticket.Create(reporterId, title, description, impact, urgency, category, now)`
      és a `Ticket.Recategorize(category, actorUserId, now)` `Category` paramétert kap. Inaktív kategóriára a válasz
      `CategoryErrors.Inactive` (`// BR-13`); a jegy csak a `CategoryId`-t tárolja. Ez eltér a prompt
      `categoryId` szignatúrájától.
    - **K2:** a hatás és a sürgősség `Resolved` állapotban is módosítható, mert csak a BR-03 tilt (lezárt jegy).
    - **K3:** a `Resolved` jegy átosztható, az állapota nem változik (BR-03 csak a `Closed`-ot tiltja, és a BR-12 szerint
      az inaktív ügyintéző jegyei átoszthatók).
    - A szöveges mezőket trimelve tároljuk; a csak szóközből álló érték túl rövidnek számít. `null` argumentum →
      `ArgumentNullException`, üres `reporterId`, `userId` vagy `actorUserId` → `ArgumentException`.
    - Az `Activate` és a `Deactivate` idempotens, `void` (`Category`, `Agent`).
    - Az `AssignTo` sikeres kiosztáskor maga hívja az `agent.RecordAssignment(now)`-t (BR-05 holtverseny).
    - A változás nélküli hívás (ugyanaz a hatás és sürgősség, kategória vagy ügyintéző) sikeres, de nem ír
      történetbejegyzést, és nem frissíti a `LastAssignedAt`-et.
  - **Állapotgép (a tábla a tesztben):**
    - Az `AssignTo` `New` állapotban `Assigned`-ba visz; `Assigned`, `InProgress` és `Resolved` állapotban átoszt,
      az állapot marad; `Closed` esetén `Ticket.ClosedCannotBeAssigned` (BR-03).
    - A `Start`, `Resolve`, `Reopen` és `Close` csak a §10 szerinti egy-egy kiinduló állapotból indulhat; minden
      más, a `Closed`-ból induló is, `Ticket.InvalidTransition`. Mivel `Closed`-ból egy átmenet sem indul,
      a lezárt jegy terminális (BR-03).
    - Az `Unassign` csak `Assigned` és `InProgress` állapotban engedett (→ `New`), egyébként `InvalidTransition`.
    - A `Recategorize` `New`, `Assigned` és `InProgress` állapotban engedett. `Resolved` esetén
      `Ticket.RecategorizationNotAllowed` (BR-07), `Closed` esetén `Ticket.ClosedCannotBeModified` (BR-03).
    - A `ChangeImpactAndUrgency` `Closed` esetén `Ticket.ClosedCannotBeModified`, minden más állapotban engedett.
  - **Ellenőrzési sorrend:** először a lezárt állapot (BR-03), aztán az állapotfeltétel, majd a másik aggregátum
    (inaktív ügyintéző vagy hiányzó kompetencia: BR-04, BR-12; inaktív kategória: BR-13), végül a „nincs változás”
    eset. A teszt rögzíti, hogy lezárt jegy és inaktív ügyintéző együttesénél a BR-03 hibája jön.
  - **Végrehajtó (a §10 „Ki végzi” oszlopa szerint):** a csak Admin által végezhető műveletek (`Start`, `Resolve`,
    `Reopen`, `Close`, `Recategorize`, `ChangeImpactAndUrgency`) kötelező `string actorUserId`-t kapnak.
    Az `AssignTo` `string?`-et (`null` = rendszer, BR-05). Az `Unassign`-nak nincs végrehajtó-paramétere,
    a bejegyzésben `null` (rendszer) szerepel.
  - **Prioritás (BR-11):** `Priority { get; private set; }`, amelyet csak a `Create` és a `ChangeImpactAndUrgency`
    állít, a `PriorityMatrix.Calculate`-tel. Azért tárolt és nem csak getter, mert a későbbi terhelésszámítás
    (BR-05) lekérdezésben fogja használni.
  - **Jegytörténet (BR-10):**
    - A `TicketHistoryAction` értékei: `Created`, `Assigned`, `Reassigned`, `Unassigned`, `StatusChanged`,
      `Recategorized`, `ImpactChanged`, `UrgencyChanged`.
    - Az első kiosztás `Assigned` és `StatusChanged` (New → Assigned) bejegyzést ír, az `Unassign` `Unassigned`
      és `StatusChanged` (→ New) bejegyzést.
    - A hatás és a sürgősség külön bejegyzést kap, csak akkor, ha változott.
    - A régi és az új érték enum-név vagy Guid; a 200 karakteres korlátot guard ellenőrzi (programozási hiba
      esetén `ArgumentOutOfRangeException`).
  - **Kompetenciák:** az `AgentCompetency` az `(AgentId, CategoryId)` gyerek-entitás. A `SetCompetencies` lecseréli
    a halmazt (duplikátum nélkül), a megmaradó elemeket nem cseréli ki. Üres halmaz megengedett; `Guid.Empty`
    esetén `Validation.CategoryIds` hiba, és a meglévő kompetenciák nem változnak.
  - **Kategória:** a `Code` létrehozás után nem változik (a prompt nem kért rá metódust); a `Description` opcionális
    (üres is lehet, legfeljebb 500 karakter). A kódformátum (`^[A-Z_]+$`) karakterellenőrzéssel készült, így a
    regex `$`-jelének sorvégi viselkedése sem okozhat eltérést.
  - **Hibák:** az üzleti hibák `BusinessRule` típusúak (`Ticket.InvalidTransition`, `Ticket.ClosedCannotBeAssigned`,
    `Ticket.ClosedCannotBeModified`, `Ticket.RecategorizationNotAllowed`, `Agent.Inactive`, `Agent.MissingCompetency`,
    `Category.Inactive`). A mezőhibák `Validation.<Property>` kódúak (`Title`, `Description`, `Impact`, `Urgency`,
    `Code`, `Name`, `DisplayName`, `CategoryIds`). A `NotFound` hibák az Application-lépéssel jönnek, mert a Domain
    nem használja őket.
  - **`Result`:** a `Result<T>` a `Result`-ból származik (`private protected` konstruktor, így más assemblyben nem
    származtatható). Implicit konverzió van `Error` → `Result` / `Result<T>` és `T` → `Result<T>` irányba.
    A `Value` és az `Error` rossz ágon `InvalidOperationException`-t dob.
  - **CA1716 (`Error` típusnév):** az `AnalysisMode=Recommended` szerint az `Error` ütközik egy Visual Basic
    kulcsszóval. A nevet a §11 rögzíti, a solution csak C#, ezért célzott `[SuppressMessage]` került az `Error`
    típusra indoklással. Globális elnyomás nincs; a `.editorconfig` (a Domain projekten kívül) nem változott.
  - **Idő a tesztekben:** rögzített `DateTimeOffset` értékek. A Domain `now` paramétert kap, így nincs mit
    `FakeTimeProvider`-rel helyettesíteni; az az Application-tesztekkel érkezik (a csomag felvétele a projekten
    kívüli `Directory.Packages.props` módosítását is igényelné).
  - **Hatókörön kívül maradt (P-2B és későbbi lépések):** a BR-05 kiosztási stratégia, a BR-07 következményeinek
    kezelése (a felelős marad, újrakiosztás vagy `Unassign`), a perzisztencia és az Application-kód.
    A `Recategorize` csak a kategóriát cseréli.

## Ellenőrzés
- dotnet build: Build succeeded – 0 Warning(s), 0 Error(s) (`--no-incremental` újrafordítással is).
- dotnet test: Passed – 181/181 zöld (Domain.Tests 168, ArchitectureTests 12, Infrastructure.Tests 1; az Application.Tests
  és a Web.Tests még üres). Az architektúra-tesztek változatlanul zöldek, köztük a
  `Domain_Assembly_ReferencesOnlySharedFrameworkAssemblies`.
  - Domain.Tests bontásban: `ResultTests` 7, `PriorityMatrixTests` 16 (9 mátrixcella, 2 érvénytelen érték, 5 súly),
    `TicketCreateTests` 23, `TicketStateMachineTests` 40 (8 művelet × 5 állapot), `TicketAssignmentTests` 13,
    `TicketChangeTests` 17, `TicketHistoryTests` 11, `CategoryTests` 27, `AgentTests` 14.
  - `dotnet test --filter "BR=BR-03"`: zöld, 47 teszt (a teljes állapotgép-tábla és a lezárt jegyre vonatkozó tesztek).
- Negatív próba: a `Close` kiinduló állapotát ideiglenesen `InProgress`-re cseréltem. 15 teszt lett piros (a két érintett
  tábla-sor és minden teszt, amely lezárt jegyet készít elő). A visszaállítás után ismét 181/181 zöld.
- Javasolt kézi ellenőrzés:
  - A `TicketStateMachineTests` 40 sorának összevetése az AGENTS.md §10 állapotgép-táblázatával és a K2/K3 döntéssel.
  - A magyar hibaüzenetek szövegezése (`TicketErrors`, `AgentErrors`, `CategoryErrors`).
  - `dotnet test --filter "BR=BR-13"` és `--filter "BR=BR-04"`: a pozitív és negatív esetek listája.
- Ismert korlátok:
  - Az `Unassign` „csak a BR-07 esetén” megkötését a Domain nem tudja kikényszeríteni (ehhez az összes ügyintézőt
    ismernie kellene); ezt komment jelzi, a hívás a P-2B kiosztási szolgáltatás felelőssége.
  - A `SetCompetencies` nem ellenőrzi, hogy a kategória létezik és aktív-e (csak azonosítót kap); ez az Application dolga.
  - A történet értékei nyers enum-nevek és Guid-ok; a megjelenítendő nevekre fordítás a UI feladata.
  - Egy műveleten belül több bejegyzés azonos `At` időbélyeget kap. A memóriában a lista sorrendje megmarad, de a
    perzisztenciánál a sorrendet külön kell biztosítani (a `Guid.CreateVersion7()` ezredmásodpercen belül nem garantál
    sorrendet).
  - A `Guid.CreateVersion7()` belül a rendszerórát olvassa; ez az egyetlen implicit időlekérdezés, és a §5 írja elő.

## Emberi javítás
Kézi kódmódosításra nem volt szükség.
## Commit
feat(domain): implement rich domain model, state machine, priority matrix and domain tests
