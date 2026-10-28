# ADR-003 – SQL Server LocalDB, Docker nélkül

- Státusz: elfogadva
- Dátum: 2026-10-28

## Kontextus

Az alkalmazásnak relációs adatbázis kell az Identity- és a domaintáblákhoz, tranzakciókkal és optimista
konkurenciakezeléssel (`RowVersion`). A fejlesztés és a bemutató Windows-gépen, Visual Studióval történik.
A beállítás legyen egyszerű, és ne kelljen hozzá konténer-futtatókörnyezet. Az integrációs teszteknek valódi
SQL Server-viselkedésre kell támaszkodniuk (rowversion, egyedi indexek, FK-k), nem memóriabeli helyettesítőre.

## Döntés

- Adatbázis: SQL Server Express LocalDB (`(localdb)\MSSQLLocalDB`), a Visual Studióval települ.
- ORM: EF Core 10 (`Microsoft.EntityFrameworkCore.SqlServer`), code-first migrációk egyetlen migrációs
  történettel. Az `EnsureCreated()` tiltott.
- Fejlesztői adatbázis: `ServiceDesk`. Integrációs tesztek: `ServiceDesk_Tests`, tesztenként Respawn-reset.
- Indításkor, csak Development környezetben: `Database.MigrateAsync()`, majd az idempotens seeder.
- Docker-függőség nincs, sem a fejlesztéshez, sem a tesztekhez.
- A `dotnet-ef` helyi eszközként van rögzítve (`.config/dotnet-tools.json`, `dotnet tool restore`).

## Alternatívák

- **SQLite:** nulla telepítés, de nincs natív `rowversion`, és eltérő a típus- és zárolási viselkedése.
  A konkurencia- és indextesztek nem a valós környezetet mérnék.
- **SQL Server Dockerben (Testcontainers):** izolált és CI-barát, de Docker Desktopot és WSL2-t igényel.
  Ez a projektkörnyezetben nem elvárható, és lassítja a tesztfuttatást.
- **EF Core InMemory provider:** nem relációs, nem érvényesíti a kényszereket és a tranzakciókat, ezért
  integrációs tesztre félrevezető.
- **PostgreSQL:** jó választás lenne, de külön telepítést igényel, és a tárgy környezete SQL Serverre épül.

## Következmények

- Csak Windowson fut (a LocalDB Windows-specifikus). Linux/macOS vagy CI esetén a kapcsolati string cseréjével
  teljes SQL Server példány használható. A kódnak ezért providerfüggetlennek kell maradnia (nincs nyers SQL
  string-összefűzéssel).
- A LocalDB első kapcsolódáskor automatikusan elindul. Az első teszt- vagy alkalmazásindítás ezért néhány
  másodperccel lassabb lehet.
- Az integrációs tesztek ugyanazon a gépi példányon futnak, ezért az Infrastructure.Tests projektben
  a párhuzamos futtatás ki van kapcsolva.
- A kapcsolati string (`ServiceDesk` adatbázis) az `appsettings.Development.json`-ban van. A migrációs történet
  az `Infrastructure/Persistence/Migrations` mappában az `InitialIdentity` migrációval indul (P-1B).
