# ADR-002 – Clean Architecture négy projekttel

- Státusz: elfogadva
- Dátum: 2026-10-28

## Kontextus

A ServiceDesk üzleti szabályai (állapotgép, prioritásmátrix, kiosztás, jogosultság) a projekt lényegét adják.
Ezeket UI, adatbázis és LLM nélkül, gyorsan kell tudni tesztelni. Az AI-szolgáltató (Ollama vagy
OpenAI-kompatibilis végpont) cserélhető, és az adatelérés sem szivároghat be az üzleti logikába. A rétegszabályokat
gépi ellenőrzésnek kell védenie, hogy ne csak konvencióként létezzenek.

## Döntés

Négy forrásprojekt, befelé mutató függőségekkel:

| Projekt | Felelősség | Hivatkozhat |
|---|---|---|
| `ServiceDesk.Domain` | entitások, enumok, domain service-ek, `Result`/`Error` | semmire (külső csomagra sem) |
| `ServiceDesk.Application` | use case-ek feature-mappákban, portok, DTO-k, validátorok | `Domain` |
| `ServiceDesk.Infrastructure` | EF Core, Identity-tárolás, repository-k, seeder, AI-adapter | `Application`, `Domain` |
| `ServiceDesk.Web` | Blazor UI, Identity UI, `UseCaseRunner`, DI-összerakás | `Application`, `Infrastructure` |

- Az Application TILTOTT függőségei: EF Core, ASP.NET Core, MudBlazor, `Microsoft.Extensions.AI`, Infrastructure, Web.
- Infrastructure-típust a Webben csak a `Program.cs` és a sablon `Components/Account` mappája használhat.
  A `ServiceDesk.Web.Components.Pages` névtér nem függhet az Infrastructure-tól és az EF Core-tól.
- A szabályokat a `ServiceDesk.ArchitectureTests` projekt ellenőrzi NetArchTest.Rules-szal. A Domain
  „nincs külső csomag” szabályát a lefordított assembly hivatkozásai alapján ellenőrzi.
- Rétegenként külön tesztprojekt (`Domain.Tests`, `Application.Tests`, `Infrastructure.Tests`, `Web.Tests`).
- Közös build-beállítások a `Directory.Build.props`-ban; csomagverziók központilag a `Directory.Packages.props`-ban.

## Alternatívák

- **Egyetlen projekt mappákkal:** kevesebb fájl, de a rétegszabályokat semmi nem kényszeríti ki: a fordító
  megengedi, hogy egy komponens közvetlenül DbContextet használjon.
- **Három projekt (Core + Infrastructure + Web):** a Domain és az Application összevonása elmosná a különbséget
  a tiszta üzleti szabályok és a use case-szintű autorizáció, illetve a portok között.
- **Vertical Slice egy projektben:** jól skálázódik, de a projekt tanulási célja a rétegzett felépítés és annak
  automatikus ellenőrzése.
- **MediatR-alapú CQRS:** további csomag és indirekció. A use case-eket közvetlenül hívott handlerek
  (`IUseCaseRunner`) egyszerűbben adják ugyanezt (a MediatR az AGENTS.md szerint tiltott).

## Következmények

- Több projekt és több fájl kell. Új funkciónál a port–adapter párt két rétegben kell felvenni.
- A Domain és az Application gyorsan, külső függőség nélkül tesztelhető. Az Infrastructure integrációs tesztjei
  LocalDB-n futnak.
- Átmenetileg a sablon Identity-kódja (`Data/ApplicationDbContext`, `ApplicationUser`, a hozzájuk tartozó
  EF/Identity-csomagok) a Webben van. Egy későbbi lépésben kerül az Infrastructure-be (`AppDbContext`).
- A NetArchTest a típusszintű függőséget ellenőrzi: a nem használt projekt- vagy csomaghivatkozás nem jelenik meg
  a lefordított kódban, ezért ezt a csproj-ok code review-ja fedi le.
