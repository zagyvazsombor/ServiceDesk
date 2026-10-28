# ADR-005 – Identity e-mail-megerősítés nélkül, jelszó- és zárolási szabályok

- Státusz: elfogadva
- Dátum: 2026-10-28

## Kontextus

A ServiceDesk az ASP.NET Core Identity sablonmechanizmusát használja (Individual accounts, cookie). A sablon
alapértelmezésként `RequireConfirmedAccount = true` értékkel fut: a regisztráló csak az e-mailben kapott link
megnyitása után léphet be. A projektben nincs e-mail-infrastruktúra. A sablon `IdentityNoOpEmailSender`-e semmit
sem küld, a megerősítő link csak a `RegisterConfirmation` oldalon jelenik meg. Az AGENTS.md §8 előírja:
- a megerősítés kikapcsolását, és azt, hogy ezt ADR rögzítse;
- legalább 10 karakteres jelszót;
- 5 hibás próbálkozás után 15 perces zárolást.

A belépést brute force ellen az Identity lockout védi (§8). A regisztráló szerepköre mindig `Reporter`, Admin
csak seedből jön létre.

## Döntés

- `SignIn.RequireConfirmedAccount = false`: a regisztráció után a felhasználó azonnal bejelentkezik, és a
  bejelentkezéshez nem kell megerősített e-mail-cím.
- `Password.RequiredLength = 10`. A többi jelszószabály (kisbetű, nagybetű, szám, nem alfanumerikus karakter)
  a sablon alapértéke marad.
- `Lockout.MaxFailedAccessAttempts = 5`, `Lockout.DefaultLockoutTimeSpan = 15 perc`,
  `Lockout.AllowedForNewUsers = true`. A `Login.razor` a `PasswordSignInAsync`-et `lockoutOnFailure: true`
  értékkel hívja, így a hibás jelszavak beleszámítanak a zárolásba.
- A beállítások egy helyen vannak: `DependencyInjection.AddInfrastructure`, az `AddIdentityCore` opcióiban.
  A sablon mechanizmusát (oldalak, végpontok, cookie-kezelés) nem írjuk felül.
- A regisztrációs és jelszóváltó űrlapok (`Register`, `ChangePassword`, `SetPassword`, `ResetPassword`)
  `MinimumLength` értéke 10, hogy az űrlap már beküldés előtt ugyanazt a szabályt mutassa, mint a szerver.

## Alternatívák

- **Megerősítés bekapcsolva, no-op küldővel.** A felhasználó a `RegisterConfirmation` oldalon megjelenő linkkel
  erősítené meg magát. Elvetve, mert ez a megerősítés látszata ellenőrzés nélkül: bárki bármilyen címet
  „megerősíthet”, a regisztráció pedig egy fölösleges lépéssel hosszabb.
- **Valódi e-mail-küldés (SMTP vagy külső szolgáltatás).** Elvetve: új infrastruktúra, titokkezelés és csomag
  kellene hozzá, ami egy egyetemi, lokálisan futó projektben nem indokolt (§3, Docker és külső függőség nélkül).
- **Az Identity alapértelmezett jelszó- és zárolási szabályai** (6 karakter; zárolás csak `lockoutOnFailure: true`
  mellett). Elvetve, mert ellentmondanak a §8-nak.

## Következmények

- Az e-mail-cím nem bizonyítottan a felhasználóé. Az alkalmazás nem küld e-mailt, és jelszó-visszaállítás sem
  működik e-mailen át. A sablon „Forgot your password?” és „Resend email confirmation” oldalai megmaradnak,
  de valódi levél nem érkezik.
- Az e-mail-megerősítést igénylő sablonoldalak (`ConfirmEmail`, `RegisterConfirmation`) továbbra is elérhetők.
  A normál regisztrációs folyamat azonban nem vezet rájuk.
- A seed-felhasználók `EmailConfirmed = true` értéke megmarad, de a bejelentkezéshez már nem szükséges.
- A zárolás a felhasználó saját fiókját is érinti: 5 elgépelés után 15 percig nem tud belépni, Admin-oldali
  feloldás nincs. Ez szándékos (§8).
- Ha később valódi e-mail-küldés kerül a projektbe, a döntést új ADR-rel kell felülvizsgálni, és a
  `RequireConfirmedAccount` visszakapcsolható.
