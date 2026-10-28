# ADR-004 – Gazdag domain modell, Result minta, végrehajtó és idő paraméterként

- Státusz: elfogadva
- Dátum: 2026-10-28

## Kontextus

A ServiceDesk lényege néhány összefüggő üzleti szabály: állapotgép, terminális lezárt jegy (BR-03), kiosztási
jogosultság (BR-04), számított prioritás (BR-11), kötelező jegytörténet (BR-10) és inaktiválható törzsadatok
(BR-12, BR-13). Ezeket a szabályokat egy helyen kell érvényesíteni, hogy se a UI, se egy későbbi use case ne
kerülhesse meg őket. A szabálysértés a normál működés része (például egy lezárt jegy kiosztási kísérlete). Ezt
a felhasználónak magyar üzenettel kell jelezni, és az Application rétegnek el kell tudnia dönteni, milyen
hibatípusról van szó (§11). A Domain nem végezhet I/O-t és nem kérdezheti le az időt, mert a szabályokat UI,
adatbázis és óra nélkül, determinisztikusan kell tudni tesztelni.

## Döntés

- **Gazdag domain modell.**
  - Az entitások (`Ticket`, `Category`, `Agent`) privát setterekkel rendelkeznek.
  - Érvényes példány csak factory metódussal jön létre (`Ticket.Create`, `Category.Create`, `Agent.Create`).
  - Állapot csak metóduson át változik (`AssignTo`, `Start`, `Resolve`, `Reopen`, `Close`, `Unassign`,
    `Recategorize`, `ChangeImpactAndUrgency`, `Activate`, `Deactivate`).
  - A gyerek-entitások (`TicketHistoryEntry`, `AgentCompetency`) csak az aggregátumukon át jönnek létre (`internal` factory).
  - Az EF Core-nak egy `private` paraméter nélküli konstruktor áll rendelkezésre.
- **Számított és származtatott adatok a domainben.**
  - A `Priority` mindig a `PriorityMatrix`-ból számolódik (BR-11). Lekérdezhetőség miatt tárolt tulajdonság,
    de setter nem állítja.
  - A `TicketHistoryEntry` bejegyzéseket a `Ticket` saját metódusai írják (BR-10), így audit nélküli
    állapotváltás nem lehetséges.
- **Más aggregátum a döntéshez paraméterként, tárolva csak az azonosítója.**
  - A `Ticket.AssignTo` az `Agent`-et kapja (aktív-e, van-e kompetenciája – BR-04).
  - A `Ticket.Create` és a `Ticket.Recategorize` a `Category`-t kapja (aktív-e – BR-13).
  - A jegy csak a `CategoryId`-t és az `AssigneeAgentId`-t tárolja.
  - Az `AssignTo` sikeres kiosztáskor frissíti az ügyintéző `LastAssignedAt` értékét (BR-05 holtverseny-feloldás).
- **Result minta.**
  - Az üzleti szabály megsértését `Result` / `Result<T>` jelzi. Hiba esetén `Error(Code, Message, Type)`
    az értéke, ahol `ErrorType` a `Validation`, `NotFound`, `Forbidden`, `Conflict`, `BusinessRule` vagy `Unavailable`.
  - A hibák központi statikus osztályokban vannak (`TicketErrors`, `AgentErrors`, `CategoryErrors`); a validációs
    kódok `Validation.<Property>` alakúak.
  - Kivételt csak programozási hiba dob (`null` argumentum, üres felhasználó-azonosító, ismeretlen enum-érték
    a `PriorityMatrix`-ban).
- **Végrehajtó és idő paraméterként.**
  - Minden állapotváltó metódus `DateTimeOffset now` paramétert kap; a Domain nem kérdezi le az órát.
    Az Application a `TimeProvider`-ből adja át.
  - A végrehajtó: `string actorUserId` a csak Admin által végezhető műveleteknél, `string? actorUserId`
    a kiosztásnál (`null` = rendszer, BR-05), az `Unassign` pedig mindig rendszerművelet.
  - Az azonosító `Guid.CreateVersion7()` (§5).

## Alternatívák

- **Anemikus modell + service-ek.** Publikus setterű adatosztályok; a szabályok handlerekben vagy „Service”
  osztályokban élnek.
  - Elvetve, mert ugyanaz a szabály (például a BR-03 vagy a BR-10) több use case-ben ismétlődne, és bármelyik
    kód megkerülhetné.
  - Az invariánsok csak konvencióként léteznének, a tesztekhez pedig mindig az Application réteget kellene felépíteni.
  - Az AGENTS.md §17 kifejezetten tiltja a publikus settert és az üzleti logikát „Service” osztályokban.
- **Kivételalapú hibakezelés.** Szabálysértésnél domain-kivétel (például `TicketClosedException`), amelyet
  a UI vagy egy middleware fordít üzenetté.
  - Elvetve, mert a szabálysértés várt kimenet, nem kivételes helyzet. A kivétel a szignatúrában nem látszik,
    így a hívó könnyen elfelejti kezelni.
  - A kivételes vezérlés drágább és nehezebben tesztelhető.
  - Keverné a váratlan hibát (amelyet a `UseCaseRunner` naplóz, és az `ErrorBoundary` kezel, §11)
    a felhasználónak szóló üzleti visszajelzéssel.
- **Idő és végrehajtó a Domainen belül** (például `DateTime.UtcNow` vagy injektált kontextus). Elvetve: rejtett
  függést és nem determinisztikus teszteket eredményezne, és a Domainnek külső absztrakcióra kellene hivatkoznia.

## Következmények

- A Domain szabályai UI, adatbázis és óra nélkül tesztelhetők. A tesztek rögzített `DateTimeOffset` értékekkel
  futnak, és az állapotgép minden (állapot, művelet) párját táblázatos teszt fedi le.
- Az Application handlerek vékonyak maradnak: autorizálnak, validálnak, betöltik az aggregátumot (és a döntéshez
  szükséges másik aggregátumot), meghívják a domain-metódust, és a `Result`-ot továbbadják.
- Minden hívónak kezelnie kell a `Result`-ot. A `Value` és az `Error` rossz ágon való olvasása
  `InvalidOperationException`-t dob, így a hiba fejlesztéskor kiderül.
- Mivel az `AssignTo`, a `Create` és a `Recategorize` entitást kap, a hívónak előbb be kell töltenie
  az `Agent`-et, illetve a `Category`-t. Az Application ezért mindkét aggregátumot ugyanabban a scope-ban
  (egy DbContext) kezeli.
- A tárolt `Priority` és a privát setterek miatt az EF-konfigurációnak (későbbi lépés) a mezőkre és a backing
  gyűjteményekre kell leképeznie (`_history`, `_competencies`).
- Az `Error` típusnév a .NET-elemzők szerint ütközik egy Visual Basic kulcsszóval (CA1716). A solution csak C#,
  ezért ezt a szabályt az `Error` típuson célzottan, indoklással kikapcsoljuk.
