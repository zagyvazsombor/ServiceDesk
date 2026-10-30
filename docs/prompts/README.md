# Prompt-naplók

A fejlesztés lépésenként, egy-egy AI-ügynöknek adott prompttal készült. Minden lépés végén a
`docs/prompts/P-<azonosító>-<slug>.md` napló rögzíti a promptot, az eredményt, az ellenőrzést, valamint a hallgató
javításait és commitját (AGENTS.md §14). Az érintett BR-ek a naplók fejlécéből származnak; a teljes lefedettség a
[docs/traceability.md](../traceability.md)-ben van.

| Azonosító | Cél | Dátum | Érintett BR-ek | ADR |
|---|---|---|---|---|
| [P-1A](P-1A-solution-skeleton.md) | Solution- és projektváz, build- és csomagkonfiguráció, architektúra-tesztek | 2026-10-28 | – | ADR-001, ADR-002, ADR-003 |
| [P-1B](P-1B-identity-and-shell.md) | Identity és DbContext az Infrastructure-be, új migráció, DI-belépési pontok, fejlesztői migráció indításkor, UI-keret | 2026-10-28 | – | – |
| [P-2A](P-2A-domain-ticket.md) | Domain-modell: `Result`/`Error`, enumok, prioritásmátrix, `Category`, `Agent`, a `Ticket` aggregátum állapotgéppel és jegytörténettel | 2026-10-28 | BR-01, BR-03, BR-04, BR-05, BR-07, BR-10, BR-11, BR-12, BR-13 | ADR-004 |
| [P-2B](P-2B-assignment-strategy.md) | Kiosztási stratégia: jelölt, terhelésszámítás, a legkisebb terhelésű ügyintéző kiválasztása | 2026-10-28 | BR-04, BR-05, BR-06 | – |
| [P-3A](P-3A-persistence.md) | Perzisztencia: portok, `CategorySuggestion`, `AppDbContext`, EF-konfigurációk, repository-k, UnitOfWork, migráció, seed | 2026-10-28 | BR-04, BR-05, BR-06, BR-08, BR-09, BR-10, BR-11, BR-13 | – |
| [P-3B](P-3B-integration-tests.md) | Integrációs tesztek LocalDB-n: fixture, Respawn, repository-k, konkurencia, seeder-idempotencia | 2026-10-28 | BR-04, BR-05, BR-10 | – |
| [P-4A](P-4A-identity-runner.md) | Identity-beállítások, Reporter szerepkör regisztrációkor, `ICurrentUser`, `UseCaseRunner`, szerepkörös menü | 2026-10-28 | – | ADR-005 |
| [P-4B](P-4B-authorization.md) | Application-szintű autorizáció (`RequireAuthenticated`, `RequireRole`), első use case, a futtató tesztjei | 2026-10-28 | – | – |
| [P-4A-fix1](P-4A-fix1-logout-antiforgery-404.md) | Javítás: a 404-es oldalról a kijelentkezés antiforgery-hibát adott | 2026-10-29 | – | ADR-006 |
| [P-5A](P-5A-reporter-use-cases.md) | Bejelentői use case-ek: jegy rögzítése, saját jegyek, részletek, automatikus kiosztás | 2026-10-29 | BR-01, BR-02, BR-04, BR-05, BR-06, BR-10, BR-11, BR-13 | – |
| [P-5B](P-5B-reporter-ui.md) | Bejelentői felületek MudBlazorral: új jegy, saját jegyek, jegy részletei | 2026-10-29 | BR-01, BR-02, BR-05, BR-06, BR-10, BR-11, BR-13 | – |
| [P-6A](P-6A-admin-use-cases.md) | Admin use case-ek: jegysor, triázs, átkategorizálás, kézi és automatikus kiosztás, státuszváltások | 2026-10-29 | BR-03, BR-04, BR-05, BR-06, BR-07, BR-10, BR-11, BR-12, BR-13 | – |
| [P-6B](P-6B-admin-ui.md) | Admin felületek: jegysor és jegykezelő panel, konkurenciaütközés kezelése | 2026-10-29 | BR-03, BR-04, BR-06, BR-07, BR-11, BR-12, BR-13 | – |
| [P-7A](P-7A-master-data.md) | Törzsadat use case-ek: kategóriák és ügyintézők, kompetenciák, aktiválás és inaktiválás | 2026-10-29 | BR-04, BR-05, BR-12, BR-13 | – |
| [P-7B](P-7B-master-data-ui.md) | Törzsadat-felületek: `/admin/categories`, `/admin/agents` | 2026-10-29 | BR-04, BR-05, BR-12, BR-13 | – |
| [P-8A](P-8A-ai-suggestion.md) | AI-kategóriajavaslat: port, `IChatClient`-adapter, rate limit, `SuggestCategory` use case | 2026-10-29 | BR-08, BR-09, BR-13 | – |
| [P-8B](P-8B-ai-approval.md) | AI-javaslat a létrehozó űrlapon, emberi jóváhagyással; a döntés mentése a jeggyel | 2026-10-30 | BR-01, BR-05, BR-06, BR-08, BR-09, BR-13 | ADR-007 |
| [P-9A](P-9A-hardening.md) | Biztonsági és minőségi átvizsgálás: fejlécek, CSP, szerepkörök minden környezetben, naplózás, tiltólista | 2026-10-30 | BR-01–BR-13 (jelölt tesztek), különösen BR-01, BR-02, BR-04, BR-09, BR-12, BR-13 | ADR-008, ADR-009 |
| [P-9B](P-9B-documentation.md) | Dokumentáció és bemutató: README, architektúra, követhetőség, indexek, demó-forgatókönyv | 2026-10-30 | BR-01–BR-13 (dokumentálás) | – |

- A táblázat időrendben halad. A P-4A-fix1 a P-4A-ban készült kijelentkezés javítása, a P-4B után.
- A P-8B munkája 2026-10-29-én kezdődött; a dátum a napló lezárásáé.
- Az ADR-ek listája: [docs/adr/README.md](../adr/README.md).
