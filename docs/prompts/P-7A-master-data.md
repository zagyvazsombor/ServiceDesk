# P-7A – Törzsadat use case-ek: kategóriák és ügyintézők
- Dátum: 2026-10-29
- Eszköz és modell: Claude Code (asztali alkalmazás), Claude Opus 5.5 (`claude-opus-5-5`)
- Cél és érintett szabályok:
  - Admin törzsadat-kezelés az Application rétegben:
    - kategóriák: lista, létrehozás, módosítás, inaktiválás, aktiválás;
    - ügyintézők: lista, kompetenciák beállítása, inaktiválás, aktiválás.
  - A szükséges portok, lekérdezések és tesztek.
  - Üzleti szabályok: BR-04, BR-05 (terhelés a listában), BR-12, BR-13.
  - AGENTS.md: §1, §5, §7, §8, §10, §11, §12, §13, §14.
  - **A szabályrendszer bővült:** az AGENTS.md §10 BR-13 pontja a jóváhagyáskor kért pontosítással egészült ki
    (lásd a K1 döntést).
- Átadott kontextus:
  - `AGENTS.md`, `CLAUDE.md`, `docs/prompts/P-6A-admin-use-cases.md`.
  - Domain: `Agent.cs`, `AgentCompetency.cs`, `AgentErrors.cs`, `Category.cs`, `CategoryErrors.cs`, `Common/*`,
    `Assignment/*`.
  - Application: a portok (`Common/Persistence/*`), `Common/Security/*`, `Common/Validation/*`, `Common/Errors/*`,
    `DependencyInjection.cs`, `TicketAssignmentService.cs`, továbbá a `CreateTicket`, `RecategorizeTicket`,
    `AssignTicketManually`, `RetryAutoAssign`, `GetActiveCategories`, `GetAssignableAgents`, `GetTicketQueueFilters`,
    `GetMyTickets` és `GetCurrentUserProfile` feature-mappák.
  - Infrastructure: `DependencyInjection.cs`, a repositoryk, `UnitOfWork.cs`, `TicketQueries.cs`, a `Category`, az
    `Agent`, az `AgentCompetency` és a `TicketHistoryEntry` konfigurációja, `DatabaseSeeder.cs`, a migrációk
    indexnevei.
  - Tesztek:
    - Domain: `TestData`, `AgentTests`, `CategoryTests`.
    - Application: `TestData`, `TestUsers`, `RecordingLogger`, `LogEntry`, `DependencyInjectionTests`, valamint a
      `RecategorizeTicket`, `CreateTicket`, `CreateTicketValidator`, `GetTicketQueueFilters` és a státuszváltó handlerek
      tesztjei.
    - Infrastructure: fixture, `IntegrationTest`, builderek, `AgentRepositoryTests`, `TicketQueriesTests`,
      `UnitOfWorkTests`, `CloseAndAssignConflictTests`, `TestCurrentUser`.
    - ArchitectureTests: `DependencyRuleTests`.
  - `Web/Components/Pages/Admin/Agents.razor`, `Categories.razor`: csak olvasva; ezek helyőrzők, a felület a P-7B-ben
    készül.

## Prompt
[P-7A] Törzsadat use case-ek: kategóriák és ügyintézők
Kontextus: AGENTS.md §5, §8, §10 (BR-04, BR-12, BR-13), §12. A 6. lépés elkészült.

Feladat (csak Admin):
1. Kategóriák: ListCategories (az inaktívakkal együtt), CreateCategory, UpdateCategory (név és leírás; a kód nem módosítható), DeactivateCategory, ActivateCategory. Törlés nincs (BR-13). A kód egyediségét validátor és adatbázis-index is védi.
2. Ügyintézők: ListAgents (név, aktív állapot, kompetenciák, aktuális terhelés), SetAgentCompetencies (csak aktív kategóriák), DeactivateAgent, ActivateAgent. Az inaktiválás nem nyúl a kiosztott jegyekhez (BR-12).
3. A GetActiveCategories (5. lépés) továbbra is csak aktív kategóriát ad.
4. Tesztek:
   - Minden handlerre Reporter → Forbidden.
   - Inaktív kategória nem választható új jegyhez, és nem adható kompetenciaként.
   - Inaktivált ügyintéző nyitott jegyei változatlanok, de új jegyet nem kap (BR-12, BR-04).
   - Duplikált kód elutasítva.

Ne csináld: új Admin-felhasználó létrehozása (csak seed), UI (P-7B).

Elfogadás: a build és minden teszt zöld.

Zárás: AGENTS.md §1 és §14, napló: P-7A-master-data.

Jóváhagyás a terv után (kiegészítéssel):
Jóváhagyom a K1 és a K2 ajánlott változatával és a D1–D6 döntésekkel. Egy kiegészítés:
- A K1 szabályát rögzítsd az AGENTS.md §10-ben a BR-13 pontosításaként: „Az ügyintéző meglévő kompetenciája a kategória inaktiválása után is megmarad; új kompetenciaként csak aktív kategória vehető fel.” Jelöld a naplóban, hogy a szabályrendszer bővült.
- Az Agent táblán hiányzó konkurenciatoken miatti két versenyhelyzetet ismert korlátként fogadom el; kerüljön a naplóba és a P-9A security-review.md-be.
Mehet a megvalósítás.

## Eredmény
- Létrehozott és módosított fájlok:
  - `AGENTS.md`: a §10 BR-13 pontja új alponttal bővült (K1). **A szabályrendszer bővült.**
  - `src/ServiceDesk.Domain`:
    - `Categories/Category.cs`: az `IsValidCode` `private`-ből `public static` lett, `null`-ellenőrzéssel. Így a
      validátor ugyanazt a szabályt használja, nincs lemásolt minta.
    - `Categories/CategoryErrors.cs`: új `CodeAlreadyExists` hiba (`Category.CodeAlreadyExists`, Conflict, „Ezzel a
      kóddal már létezik kategória.”).
    - `Agents/Agent.cs`: új `UpdateCompetencies(IReadOnlyCollection<Category>)` metódus.
      - Új kompetencia csak aktív kategóriára vehető fel; a már meglévő megmarad (`CategoryErrors.Inactive`,
        `// BR-13`).
      - Ezután a változatlan `SetCompetencies(ids)`-t hívja.
  - `src/ServiceDesk.Application`:
    - `Common/Persistence/ICategoryRepository.cs`: új `GetByIdsAsync` és `CodeExistsAsync`.
    - `Common/Persistence/ITicketQueries.cs`: új `GetCategoriesAsync` és `GetAgentsAsync`.
    - `Common/Persistence/IUnitOfWork.cs`: a megjegyzés kiegészült a `CodeAlreadyExists` esettel.
    - új feature-mappák:
      - `Categories/ListCategories/`: `ListCategoriesQuery`, `ListCategoriesHandler`, `CategoryListItemDto`;
      - `Categories/CreateCategory/`: `CreateCategoryCommand`, `…Handler`, `…Validator`;
      - `Categories/UpdateCategory/`: `UpdateCategoryCommand`, `…Handler`, `…Validator`;
      - `Categories/DeactivateCategory/`, `Categories/ActivateCategory/`: command és handler;
      - `Agents/ListAgents/`: `ListAgentsQuery`, `ListAgentsHandler`, `AgentListItemDto`, `AgentCompetencyDto`;
      - `Agents/SetAgentCompetencies/`: `SetAgentCompetenciesCommand`, `…Handler`, `…Validator`;
      - `Agents/DeactivateAgent/`, `Agents/ActivateAgent/`: command és handler.
  - `src/ServiceDesk.Infrastructure`:
    - `Persistence/Repositories/CategoryRepository.cs`: a két új metódus.
    - `Persistence/Queries/TicketQueries.cs`: a két új lekérdezés.
    - `Persistence/Configurations/CategoryConfiguration.cs`: `CodeIndexName = "IX_Categories_Code"`, ami az EF
      alapértelmezett neve, így migráció nem kellett.
    - `Persistence/UnitOfWork.cs`:
      - Ennek az indexnek a megsértése `CategoryErrors.CodeAlreadyExists` eredmény lesz, Warning naplóval (K2).
      - A két indexfelismerő egy közös `IsUniqueIndexViolation` segédet használ.
  - Tesztek:
    - `tests/ServiceDesk.Domain.Tests`:
      - `Agents/AgentTests.cs` (+4): `UpdateCompetencies`;
      - `Categories/CategoryTests.cs` (+10): `IsValidCode`.
    - `tests/ServiceDesk.Application.Tests` (+73), új fájlok:
      - `Categories/CreateCategory/CreateCategoryValidatorTests.cs` (16) és `…HandlerTests.cs` (6);
      - `Categories/UpdateCategory/UpdateCategoryHandlerTests.cs` (8);
      - `Categories/CategoryActivationHandlersTests.cs` (12);
      - `Categories/ListCategories/ListCategoriesHandlerTests.cs` (3);
      - `Agents/ListAgents/ListAgentsHandlerTests.cs` (3);
      - `Agents/SetAgentCompetencies/SetAgentCompetenciesHandlerTests.cs` (11);
      - `Agents/AgentActivationHandlersTests.cs` (14).
    - `tests/ServiceDesk.Infrastructure.Tests` (+14):
      - új `Persistence/CategoryRepositoryTests.cs` (4);
      - `Persistence/TicketQueriesTests.cs` (+2): `GetCategoriesAsync`, `GetAgentsAsync`;
      - `Persistence/UnitOfWorkTests.cs`:
        - a `SaveChangesAsync_DuplicateCategoryCode_DoesNotReturnConcurrencyConflict` teszt helyére a
          `…_ReturnsCodeAlreadyExists` lépett (K2);
        - új `SaveChangesAsync_DuplicateAgentUser_ThrowsDbUpdateException`: minden más egyedi index megsértése
          továbbra is váratlan hiba (kivétel);
      - új `UseCases/MasterDataUseCaseTests.cs` (7): valódi handlerek LocalDB-n.
  - Ez a napló.
  - Migráció nem kellett. Az `AppDbContextModelTests` ellenőrzi, hogy a modell nem változott.
  - ADR nem készült (lásd lent).
- Döntések és indoklásuk:
  - **A felhasználó döntései (a terv jóváhagyásakor):**
    - **K1 – kompetencia inaktivált kategóriára (BR-13 pontosítása, AGENTS.md §10-ben rögzítve):**
      - Az ügyintéző meglévő kompetenciája a kategória inaktiválása után is megmarad; új kompetenciaként csak aktív
        kategória vehető fel.
      - Indok: szigorú szabálynál a kompetenciák szerkesztése csendben elvenné ezt a kompetenciát. Ekkor az inaktív
        kategóriájú meglévő jegyeket senkire nem lehetne kézzel átosztani (BR-04).
      - A szabály a domainben van (`Agent.UpdateCompetencies`). A Guid-alapú `SetCompetencies` a seedernek és a
        tesztbuildereknek marad.
      - Új név kellett, mert túlterhelésként a meglévő `SetCompetencies([])` hívás kétértelmű lenne.
    - **K2 – duplikált kód versenyhelyzetben:**
      - A szokásos esetet a `CreateCategoryValidator` szűri ki: `MustAsync` + `ICategoryRepository.CodeExistsAsync`,
        csak helyes formátumú kódra, `Validation.Code` hibakóddal és a mezőhöz rendelt üzenettel.
      - Ha két Admin egyszerre hozza létre ugyanazt a kódot, mindkettő átjut a validátoron. Ilyenkor az egyedi index
        utasítja el a másodikat, és a `UnitOfWork` ezt `CategoryErrors.CodeAlreadyExists` (Conflict) eredménnyé
        alakítja. Így nem kivétel és `ErrorBoundary` lesz belőle.
    - **D1 – idempotens aktiválás és inaktiválás:** a már a célállapotban lévő elemre a válasz siker, mentés és napló
      nélkül.
    - **D2 – kategória inaktiválása:** az ügyintézők kompetenciái változatlanok maradnak; újraaktiváláskor minden
      visszaáll.
    - **D3 – inaktív ügyintéző:** a kompetenciái szerkeszthetők.
    - **D4 – inaktív kategória:** a neve és a leírása módosítható, és inaktív marad.
    - **D5 – kód:** a validátor és a domain is levágott szóközökkel (trim) kezeli; az egyediséget csak helyes formátumú
      kódra ellenőrzi.
    - **D6 – rendezés:** a listák az adatbázis rendezését követik, nem a `hu-HU` kultúrát.
  - **Portok:**
    - A §5 portlistája rögzített, ezért a két listázó lekérdezés az `ITicketQueries` része, ahogy a P-6A-ban a
      `GetTicketQueueFiltersAsync`.
    - A kompetenciák kategóriáinak betöltése (`GetByIdsAsync`) és a kódellenőrzés (`CodeExistsAsync`) az
      `ICategoryRepository`-ba került.
  - **Handler-sorrend (§5):**
    - Mind a 9 handler első lépése `RequireRole(Roles.Admin)`; ezt követi a validáció (ahol van), majd a betöltés, a
      domain-művelet, egyetlen `SaveChangesAsync` és az eredmény.
    - Validátor csak ott készült, ahol a commandnak az azonosítón kívül más mezője is van: `CreateCategory`,
      `UpdateCategory`, `SetAgentCompetencies`.
    - A csak azonosítót tartalmazó commandok üres `Guid`-ja egyszerűen `NotFound`, ahogy a P-6A-ban.
  - **Over-posting (§8):** az `UpdateCategoryCommand`-ban nincs kódmező, így a kód nem módosítható és nem is
    küldhető be. Új Admin-felhasználót semmi nem hoz létre; ügyintézői profil továbbra is csak a seedből jön.
  - **`SetAgentCompetencies`:**
    - A command a teljes új kompetenciakészlet; üres lista minden kompetenciát töröl.
    - Az ismétlődő azonosítók egyszer számítanak.
    - Ismeretlen kategóriára `CategoryErrors.NotFound`, új inaktívra `CategoryErrors.Inactive`. Ilyenkor semmi nem
      változik.
  - **`DeactivateAgent` (BR-12):** a handlernek nincs `ITicketRepository` függősége, így a jegyekhez szerkezetileg sem
    nyúlhat. A BR-04 miatt az inaktív ügyintéző sem automatikusan, sem kézzel nem kap új jegyet; ezt a meglévő
    `Agent.IsEligibleFor` és `Ticket.AssignTo` biztosítja.
  - **`ListAgents`:**
    - A terhelés az `AgentCandidate.Load`-dal számol, ugyanúgy, mint az automatikus kiosztás (BR-05). Az inaktív
      ügyintéző nyitott jegyei is beleszámítanak, mert megtartja őket (BR-12).
    - A kompetenciák között az időközben inaktivált kategória is szerepel, `IsActive = false` értékkel.
    - Egyetlen `AsSplitQuery` lekérdezés: nincs N+1, és a kompetenciák és a nyitott jegyek sorai nem szorzódnak össze.
  - **`CreateCategory`:** `Result<Guid>`, az új kategória azonosítójával; az új kategória mindig aktív. A leírás
    opcionális: `null` üres szövegként kerül a domainbe.
  - **Naplózás (§11):** minden sikeres módosítás Information szintű, `[LoggerMessage]`-dzsel írt bejegyzést ad, csak
    azonosítókkal (kategória, kód, ügyintéző, kompetenciák száma, végrehajtó). A duplikált kód versenyhelyzete Warning.
  - **ADR nem készült.** Új architekturális döntés nem született. A handlerek, a portbővítések és a `UnitOfWork`
    hibafordítása a meglévő mintát követik (ADR-002, P-6A K7, a `TicketHistoryEntry` indexének kezelése).

## Ellenőrzés
- dotnet build: Build succeeded – 0 Warning(s), 0 Error(s) (`--no-incremental` újrafordítással).
- dotnet test: Passed – 667/667 zöld.
  - Projektenként: Domain.Tests 248 (+14), Application.Tests 232 (+73), Infrastructure.Tests 63 (+14),
    Web.Tests 110, ArchitectureTests 14.
  - Kiinduló állapot: 566/566.
  - Trait-szűréssel (`dotnet test --solution ServiceDesk.slnx -- --filter-trait "BR=…"`): BR-13 40 teszt, BR-12 23
    teszt.
- Javasolt kézi ellenőrzés (a felület a P-7B-ben készül, addig csak adatbázis-szinten):
  - Seed-adatokkal Nagy Béla (SOFTWARE, ACCESS) inaktiválása után a nyitott jegyei nála maradnak. Egy új ACCESS-jegy
    `New` marad, felelős nélkül, mert más nem kompetens rá. Egy új SOFTWARE-jegy Tóth Csabához kerül.
  - Az OTHER kategória inaktiválása után az új jegy űrlapján nem választható. Csaba OTHER-kompetenciája megmarad,
    másik ügyintézőhöz viszont nem adható hozzá.

## Ismert korlátok
- **Az `Agent` táblán nincs konkurenciatoken.** A felhasználó ismert korlátként elfogadta; a P-9A
  `security-review.md`-jébe is fel kell venni. Két versenyhelyzet:
  - Ha egy ügyintézőt egy kiosztás (automatikus vagy kézi) betöltése és mentése között inaktiválnak, a jegyet még
    megkaphatja (BR-04, BR-12).
  - Ha egy kategóriát egy jegy létrehozása vagy egy kompetencia-beállítás betöltése és mentése között inaktiválnak, a
    jegy vagy a kompetencia még a már inaktív kategóriára kerülhet (BR-13). A `Category` táblán sincs
    konkurenciatoken.
- Két egyszerre szerkesztett ügyintéző- vagy kategóriaadat közül az utolsó mentés nyer (nincs ütközésjelzés).
  Ha két kompetencia-beállítás egyszerre veszi fel ugyanazt a kategóriát, az elsődleges kulcs megsértése váratlan
  hibaként jelenik meg.
- A listák névsorrendje az adatbázis alapértelmezett rendezését követi, nem a `hu-HU` kultúrát (D6).
- A `ListCategories` és a `ListAgents` nem lapoz; a törzsadatok száma kicsi.

## Emberi javítás
Kézi forráskód-módosításra nem volt szükség. A build és a tesztek zöldek.
## Commit
feat(master-data): implement category and agent management use cases and queries
