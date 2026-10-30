# ADR-009 – Szerepkörök minden környezetben, demóadat csak Developmentben

- Státusz: elfogadva
- Dátum: 2026-10-30

## Kontextus

Az AGENTS.md §7 szerint indításkor, csak Development környezetben fut a `MigrateAsync`, majd az idempotens
`DatabaseSeeder`. Ez a szerepköröket, a demófelhasználókat, a kategóriákat és az ügyintézőket is létrehozza.

- A regisztráció (`Register.razor`) a sikeres `CreateAsync` után `AddToRoleAsync(user, Roles.Reporter)`-t hív (§8).
- Developmenten kívül a `Reporter` szerepkör nem létezett, ezért a regisztráció hibát adott. Ezt a P-4A ismert
  korlátként rögzítette, a P-9A-ra halasztva.
- A szerepkörök nem demóadatok: az autorizáció mindhárom szintje rájuk épül.

## Döntés

- Új, idempotens `RoleSeeder` (`Infrastructure/Persistence/Seed`): csak a hiányzó `Reporter` és `Admin` szerepkört
  hozza létre.
- Indításkor:
  - Development: `InitializeDatabaseAsync`, vagyis migráció, majd a `DatabaseSeeder`. A szerepköröket ez is a
    `RoleSeeder`-rel hozza létre, utána a demóadatokat.
  - Minden más környezet: `SeedRolesAsync`, vagyis csak a `RoleSeeder`. Nincs migráció és nincs demóadat.
- Tesztek:
  - `DatabaseSeederTests`: a `SeedRolesAsync` idempotens, és csak a két szerepkört hozza létre.
  - `ProductionStartupTests`: a valódi `Program.cs` Production környezetben, saját `ServiceDesk_WebTests`
    adatbázison. Két indulás után pontosan a két szerepkör van, demófelhasználó, kategória és ügyintéző nincs. A
    regisztráció `Reporter` szerepkört ad.
- A Web.Tests adatbázis nélküli HTTP-factory-je (`ServiceDeskWebApplicationFactory`) csonkolt `IRoleStore`-t kap,
  amely minden szerepkört létezőnek mutat. Így ezek a tesztek továbbra sem nyitnak adatbázist.

## Alternatívák

- **Szerepkörök a migrációban (`HasData`):** az `IdentityRole` `ConcurrencyStamp`-je és `Id`-ja a migrációba
  égne. A Development-seeder és a `RoleManager` normalizálása mellett ez kettős forrás lenne. Elvetve.
- **`IHostedService`:** a Developmentben a migráció után kellene futnia, és a tesztekben nehezebb kikapcsolni.
  Elvetve.
- **A teljes seeder minden környezetben:** demófelhasználók ismert jelszóval kerülnének élesbe. Elvetve.

## Következmények

- A regisztráció minden környezetben működik, ha a séma telepítve van.
- Developmenten kívül a sémát telepíteni kell (`dotnet ef database update` vagy migrációs szkript). Hiányzó séma
  esetén az indulás a szerepkör-seednél hibát ad.
- Az AGENTS.md §7 indítási bekezdése ennek megfelelően frissült (P-9A).
- A Web-szintű adatbázistesztek saját `ServiceDesk_WebTests` adatbázist használnak. A `ServiceDesk_Tests`-et az
  Infrastructure-tesztek Respawn-nal ürítik, miközben a tesztprojektek párhuzamosan futnak.
