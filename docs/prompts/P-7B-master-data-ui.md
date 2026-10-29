# P-7B – Törzsadat-felületek
- Dátum: 2026-10-29
- Eszköz és modell: Claude Code (asztali alkalmazás), Claude Opus 5.5 (`claude-opus-5-5`)
- Cél és érintett szabályok:
  - `/admin/categories` és `/admin/agents` Admin-felületek a P-7A use case-eire: lista, létrehozás és szerkesztés,
    kompetenciák, aktiválás és inaktiválás megerősítéssel; bUnit-tesztek.
  - A jóváhagyáskor kért K2-döntés: az inaktív kategóriájú kompetenciák megtartása a szerveren (Domain és
    Application), tesztekkel. **A szabályrendszer bővült:** az AGENTS.md §10 BR-13 pontja új alponttal egészült ki.
  - Üzleti szabályok: BR-04, BR-05 (terhelés a listában), BR-12, BR-13.
  - AGENTS.md: §1, §5 (Web), §6, §8, §10, §11, §12, §13, §14.
- Átadott kontextus:
  - `AGENTS.md`, `CLAUDE.md`, `docs/prompts/P-7A-master-data.md`, `P-6B-admin-ui.md`.
  - Application: a P-7A `Categories/*` és `Agents/*` feature-mappái, `GetActiveCategories`, `ValidatorExtensions`.
  - Domain: `Agent.cs`, `AgentCompetency.cs`, `Category.cs`, `CategoryErrors.cs`, `Result.cs`.
  - Infrastructure: `CategoryRepository.cs`.
  - Web: a helyőrző `Agents.razor` és `Categories.razor`, `TicketQueue.razor`, `NewTicket.razor`, `TicketAdminPanel.razor`,
    `AssignTicketDialog.razor`, `RecategorizeTicketDialog.razor`, `TicketLabels.cs`, `NavMenu.razor`, `_Imports.razor`.
  - Tesztek:
    - Web.Tests: `TicketDetailsAdminTests`, `TicketQueueTests`, `AssignTicketDialogTests`, `PageAuthorizationTests`,
      `NewTicketTests`, `TestUsers`.
    - Domain.Tests: `AgentTests`, `TestData`.
    - Application.Tests: `SetAgentCompetenciesHandlerTests`.
    - Infrastructure.Tests: `MasterDataUseCaseTests`.
    - ArchitectureTests: `DependencyRuleTests`.
  - Külső forrás: a MudBlazor 9.11 XML-dokumentációja (API-ellenőrzés: `MudList` többválasztós mód, `MudDataGrid`
    lapozás, `MudMessageBox`).

## Prompt
[P-7B] Törzsadat-felületek
Kontextus: AGENTS.md §5 (Web), §6. A P-7A elkészült.

Feladat:
1. /admin/categories: MudDataGrid (kód, név, leírás, aktív); létrehozás és szerkesztés MudDialog-ban; aktiválás és inaktiválás megerősítéssel. Törlés gomb nincs.
2. /admin/agents: lista (név, aktív, kompetenciák chipként, terhelés); a kompetenciák szerkesztése MudDialog-ban többválasztós listával (csak aktív kategóriák); aktiválás és inaktiválás megerősítéssel. A dialógus jelzi, hogy a kiosztott jegyek az ügyintézőnél maradnak (BR-12).
3. Web.Tests (bUnit): az inaktiválás megerősítés nélkül nem hív use case-t.

Elfogadás: a build és a tesztek zöldek.

Zárás: AGENTS.md §1 és §14, napló: P-7B-master-data-ui.

Jóváhagyás a terv után (eltéréssel):
Jóváhagyom, a következőkkel:
- K1: (a) – a CategoryDialog maga futtatja a CreateCategory és UpdateCategory use case-t, validációs hibánál nyitva marad.
- K2: (a) viselkedés, de a szabály a szerveren legyen, ne a felületen. A SetAgentCompetencies / Agent.UpdateCompetencies a meglévő, inaktív kategóriájú kompetenciákat automatikusan megtartja akkor is, ha a beküldött halmaz nem tartalmazza őket (// BR-13); a felület csak az aktív kategóriák kijelölését küldi, és tájékoztatóban mutatja a megmaradó inaktívakat. Ez Domain- és Application-módosítás a hozzá tartozó tesztekkel (domain: a kihagyott inaktív kompetencia megmarad; LocalDB: a SetAgentCompetencies után az inaktív kompetencia megvan). Az AGENTS.md §10 BR-13 alpontját egészítsd ki: „A kompetenciák cseréje az inaktív kategóriájú meglévő kompetenciákat nem érinti.”
- A D1–D5 döntéseket elfogadom.
Mehet a megvalósítás.

## Eredmény
- Létrehozott és módosított fájlok:
  - `AGENTS.md`: a §10 BR-13 pontja új alponttal bővült (K2). **A szabályrendszer bővült.**
  - `src/ServiceDesk.Domain/Agents/Agent.cs`:
    - Az `UpdateCompetencies` új szignatúrája: `(IReadOnlyCollection<Category> categories, IReadOnlyCollection<Category> currentCategories)`.
    - A második paraméter a meglévő kompetenciák kategóriái.
    - Az inaktív kategóriájú meglévő kompetenciák akkor is megmaradnak, ha a kért halmaz nem tartalmazza őket (`// BR-13`).
    - Ha egy meglévő kompetencia kategóriája hiányzik a második paraméterből, az programozási hiba: `ArgumentException`.
  - `src/ServiceDesk.Application/Agents/SetAgentCompetencies/`:
    - `SetAgentCompetenciesHandler.cs`:
      - A kért és a meglévő kompetenciák kategóriáit egyetlen `GetByIdsAsync` hívással tölti be, majd szétválogatja
        őket.
      - Az ismeretlen kért kategória továbbra is `CategoryErrors.NotFound`.
      - A napló az eredő kompetenciaszámot írja, a megtartott inaktívakkal együtt.
    - `SetAgentCompetenciesCommand.cs`: a megjegyzés pontosítva.
  - `src/ServiceDesk.Web`:
    - `Components/Pages/Admin/Categories.razor`:
      - `MudDataGrid` a Kód, Név, Leírás, Állapot és Műveletek oszlopokkal;
      - „Új kategória” gomb; soronként „Szerkesztés” és „Inaktiválás” vagy „Aktiválás”;
      - törlés gomb nincs (BR-13).
    - `Components/Pages/Admin/Agents.razor`:
      - `MudDataGrid` a Név, Állapot, Kompetenciák, Terhelés, Nyitott jegyek és Műveletek oszlopokkal;
      - a kompetenciák chipként jelennek meg; az inaktív kategória szürke, körvonalas chipet kap „(inaktív)”
        jelöléssel;
      - soronként „Kompetenciák” és „Inaktiválás” vagy „Aktiválás”.
    - Új mappa, `Components/MasterData/`:
      - `CategoryDialog.razor`: létrehozás és szerkesztés (K1);
      - `AgentCompetenciesDialog.razor`;
      - `ActiveStateChip.razor`.
  - Tesztek:
    - `tests/ServiceDesk.Domain.Tests/Agents/AgentTests.cs` (+3):
      - a meglévő `UpdateCompetencies`-tesztek az új szignatúrát használják;
      - a `…_DeactivatedCategoryLeftOut_RemovesIt` helyére a `…_DeactivatedCategoryLeftOut_KeepsIt` `[Theory]` lépett
        (2 eset, a K2 szerint);
      - új teszt: `…_ActiveCategoryLeftOut_RemovesIt`;
      - új teszt: `…_CategoryOfCurrentCompetencyMissing_Throws`.
    - `tests/ServiceDesk.Application.Tests/.../SetAgentCompetenciesHandlerTests.cs` (+1):
      - új teszt: `HandleAsync_DeactivatedCategoryLeftOut_KeepsItSavesAndLogsResultingCount`.
    - `tests/ServiceDesk.Infrastructure.Tests/UseCases/MasterDataUseCaseTests.cs` (+1):
      - új LocalDB-teszt: `SetAgentCompetencies_DeactivatedCategoryLeftOut_KeepsIt`.
    - `tests/ServiceDesk.Web.Tests` (+18), új fájlok:
      - `Pages/Admin/CategoriesTests.cs` (7);
      - `Pages/Admin/AgentsTests.cs` (5);
      - `Components/MasterData/CategoryDialogTests.cs` (3);
      - `Components/MasterData/AgentCompetenciesDialogTests.cs` (3).
  - Ez a napló.
  - Migráció nem kellett, mert a modell nem változott. Új csomag nincs. ADR nem készült (lásd lent).
- Döntések és indoklásuk:
  - **A felhasználó döntései (a terv jóváhagyásakor):**
    - **K1 – a `CategoryDialog` maga futtatja a use case-t:**
      - Validációs hibánál a hiba a mezőnél jelenik meg, a dialógus nyitva marad, és a begépelt adat megmarad.
        Ilyen hiba például a már létező kód, amelyet a valódi `CreateCategoryValidator` ad `Validation.Code` kóddal.
      - Más hiba (például `NotFound` vagy a versenyhelyzetből adódó `CodeAlreadyExists`) Snackbart ad.
      - A dialógus csak sikeres mentés után zárul. A sikerüzenetet és az újratöltést az oldal végzi.
    - **K2 – inaktív kategóriájú kompetencia:**
      - A megtartás szabálya a domainben van (`Agent.UpdateCompetencies`), nem a felületen.
      - A felület csak az aktív kategóriák kijelölését küldi el. A megmaradó inaktívakat a dialógus tájékoztatóban
        mutatja: „Az inaktív kategóriájú kompetenciák megmaradnak: …”.
      - A domainnek tudnia kell, mely meglévő kompetenciák kategóriája inaktív, ezért a handler ezeket a kategóriákat
        is betölti.
      - A második paraméter teljességét a domain ellenőrzi. Hiányzó kategória esetén programozási hibát (kivételt) dob,
        így nem veszhet el csendben egy kompetencia.
    - **D1 – listák:**
      - `MudDataGrid`, kliensoldali `Items`-szel. Nincs lapozó, ezért minden sor megjelenik.
      - Nincs rendezés és szűrés; a sorrend az adatbázisé (P-7A, D6).
    - **D2 – megerősítés:**
      - `DialogService.ShowMessageBoxAsync`, ahogy a jegy lezárásánál. Az aktiválás és az inaktiválás is megerősítést
        kér; „Mégse” után nem fut use case.
      - Az ügyintéző inaktiválásának szövege a nyitott jegyek számával jelzi, hogy a kiosztott jegyek nála maradnak
        (BR-12).
      - A szöveg a `Message` paraméterbe kerül, nem `MarkupMessage`-be, így a név nem értelmeződik markupként (§6).
    - **D3 – kompetencia-dialógus:**
      - `MudList` többválasztós módban, a `GetActiveCategories` adataival. A jelenlegi aktív kompetenciák előre ki
        vannak jelölve.
      - A „Mentés” gomb változás nélkül tiltott.
      - Tájékoztató sor: „A kompetencia elvétele nem érinti a már kiosztott jegyeket.”
      - A dialógus az azonosítókat adja vissza, a `SetAgentCompetencies`-t az oldal futtatja (a P-6B mintája).
    - **D4 – kategória-dialógus:**
      - Szerkesztéskor a Kód mező tiltott, mert az `UpdateCategoryCommand` nem tartalmaz kódot (§8).
      - A kliensoldal csak a kötelező mezőket és a maximális hosszt ellenőrzi (`Category.*MaxLength`, számlálóval).
      - A formátumot és az egyediséget a szerveroldali validátor ellenőrzi.
    - **D5 – eredménykezelés:**
      - Siker: Success Snackbar és a lista újratöltése. Hiba: Error Snackbar.
      - Egyszerre egy művelet fut (`_busy`), és a gombok addig tiltottak.
      - A `CancellationTokenSource`-t a `Dispose` szakítja meg, ahogy a `TicketAdminPanel`-ben.
  - **Elnevezés:** a dialógus paramétere `EditedCategory` lett, mert a `Category` név a komponensben eltakarná a
    `ServiceDesk.Domain.Categories.Category` típust (`Category.CodeMaxLength`).
  - **Ismételt minta:** a `RunExclusiveAsync` és a `RunCommandAsync` mindkét oldalon a `TicketAdminPanel` mintájának
    másolata. A §1 tiltja a kéretlen refaktorálást, ezért nem lett belőlük közös ős.
  - **Tesztek:**
    - A `CategoryDialogTests` a valódi `CreateCategoryHandler`-t és `UpdateCategoryHandler`-t futtatja helyettesített
      portokon (a `TicketQueueTests` mintája). Így a teszt a tényleges commandot és a valódi validátor válaszát
      ellenőrzi.
    - Az oldaltesztekben a runner és a dialógusok helyettesítettek.
  - **Architektúra:** a `Components/MasterData` komponensei csak Application- és Domain-típusokat használnak, az
    `IUseCaseRunner`-en át. A Web-szintű függőségi szabályt (nincs Infrastructure) az architektúra-tesztek
    ellenőrzik.
  - **ADR nem készült.** Új architekturális döntés nem született. A K2 üzleti szabály, ezért az AGENTS.md §10-be került;
    a Web-minták a P-6B-t követik.

## Ellenőrzés
- dotnet build: Build succeeded – 0 Warning(s), 0 Error(s) (`--no-incremental` újrafordítással).
- dotnet test (`--solution ServiceDesk.slnx`): Passed – 690/690 zöld.
  - Kiinduló állapot: 667/667.
  - Projektenként: Domain.Tests 251 (+3), Application.Tests 233 (+1), Infrastructure.Tests 64 (+1),
    Web.Tests 128 (+18), ArchitectureTests 14.
  - Trait-szűréssel (`-- --filter-trait "BR=…"`): BR-13 52 teszt (korábban 40), BR-12 25 teszt (korábban 23).
- Kézi ellenőrzés a futó alkalmazásban (Development, `http://localhost:5123`, `admin.anna`):
  - Kategóriák:
    - Az 5 seed-kategória megjelent; mindegyik „Aktív”, „Szerkesztés” és „Inaktiválás” gombbal, törlés gomb nélkül.
    - „Új kategória” `HARDWARE` kóddal: a Kód mező alatt „Ezzel a kóddal már létezik kategória.” jelent meg, a
      dialógus nyitva maradt, és nem jött létre kategória.
    - Az Egyéb inaktiválásakor megjelent a megerősítő szöveg; a jóváhagyás után a kategória inaktív lett.
  - Ügyintézők:
    - Tóth Csabánál „Egyéb (inaktív)” szürke chip látszott a Hálózat és a Szoftver mellett; a terhelés és a nyitott
      jegyek oszlopa kitöltött volt.
    - Csaba kompetencia-dialógusában csak a négy aktív kategória szerepelt (Hálózat és Szoftver kijelölve), alatta a
      „megmaradnak: Egyéb” tájékoztató. A Mentés gomb változásig tiltott volt.
    - A Szoftvert elvéve és mentve Csaba kompetenciái „Egyéb (inaktív)” és „Hálózat” lettek. A szerver tehát
      megtartotta az inaktívat, pedig a felület nem küldte el (K2).
    - Kiss Anna inaktiválásának megerősítése: „A már kiosztott jegyei (1 nyitott jegy) nála maradnak…”. A „Mégse”
      után nem történt semmi.
  - Visszaállítás: Csaba újra megkapta a Szoftvert, az Egyéb újra aktív lett. A fejlesztői adatbázis törzsadatai így
    ismét a seed szerintiek.
  - A böngészőkonzolban és a szervernaplóban nem volt hiba.
- Javasolt kézi ellenőrzés:
  - Egy új kategória létrehozása; ez végleges, mert kategória nem törölhető.
  - Egy ügyintéző tényleges inaktiválása, majd egy jegy létrehozása a kategóriájában (BR-12, BR-04).
  - Keskeny képernyőn mindkét táblázat vízszintesen görgethető.

## Ismert korlátok
- **K2 következménye:** inaktív kategóriájú kompetencia sem a felületen, sem a use case-ben nem vehető el. Csak a
  kategória újraaktiválása után távolítható el.
- **Konkurencia:** a P-7A korlátai érvényesek. Az `Agent` és a `Category` táblán nincs konkurenciatoken, így két
  egyszerre szerkesztett törzsadat közül az utolsó mentés nyer. A felület ezért nem ad „Frissítés” akciót ezeknél a
  műveleteknél.
- **Oldaltesztek:** a runner helyettesítése miatt az oldaltesztek a `SetAgentCompetencies` és a `Deactivate…`
  commandok azonosítóit nem ellenőrzik. A visszaadott azonosítókat a dialógustesztek, a use case-t a P-7A tesztjei
  fedik.
- A listák nem lapoznak és nem rendezhetők; a sorrend az adatbázisé (P-7A, D6).

## Emberi javítás
Kézi kódmódosításra nem volt szükség. A K1–K2 döntéseket és a D1–D5 felületi megoldásokat jóváhagytam: a CategoryDialog maga futtatja a use case-eket a szerkesztési állapot megőrzéséért, a szerveroldal (Domain és Application) pedig a BR-13 bővítésének megfelelően automatikusan megőrzi az inaktív kategóriájú kompetenciákat. A dialógusok, megerősítő ablakok, a törzsadat-kezelő felületek és a bUnit-tesztek rendben elkészültek. A kézi böngészős ellenőrzés lefutott, mind a 690/690 teszt hibátlanul zöld.
## Commit
feat(master-data): implement category and agent management ui with mudblazor
