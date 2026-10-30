# Architekturális döntések (ADR)

Minden ADR szakaszai: Kontextus, Döntés, Alternatívák, Következmények (AGENTS.md §14). Új ADR akkor készül, ha egy
lépés architekturális döntést hoz vagy változtat; a fájlnév `ADR-<nnn>-<slug>.md`.

| ADR | Cím | Dátum | Státusz | Lépés | Összefoglaló |
|---|---|---|---|---|---|
| [ADR-001](ADR-001-blazor-web-app-interactive-server.md) | Blazor Web App, globális Interactive Server, MudBlazor, egyetlen alkalmazás | 2026-10-28 | elfogadva | P-1A | Egyetlen .NET 10 Blazor Web App globális Interactive Server móddal, prerender nélkül, kizárólag MudBlazor UI-val, REST API és külön frontend nélkül. |
| [ADR-002](ADR-002-clean-architecture.md) | Clean Architecture négy projekttel | 2026-10-28 | elfogadva | P-1A | Domain, Application, Infrastructure és Web projekt befelé mutató függőségekkel, amelyeket NetArchTest-alapú architektúra-tesztek ellenőriznek. |
| [ADR-003](ADR-003-sql-server-localdb.md) | SQL Server LocalDB, Docker nélkül | 2026-10-28 | elfogadva | P-1A | SQL Server Express LocalDB EF Core 10 code-first migrációkkal; az integrációs tesztek valódi LocalDB-n, Respawn-resettel futnak, Docker nélkül. |
| [ADR-004](ADR-004-rich-domain-model.md) | Gazdag domain modell, Result minta, végrehajtó és idő paraméterként | 2026-10-28 | elfogadva | P-2A | Privát setteres entitások factoryval és állapotváltó metódusokkal; a szabálysértés `Result`/`Error`, az időpont és a végrehajtó paraméter, így a domain I/O és óra nélkül tesztelhető. |
| [ADR-005](ADR-005-identity-without-email-confirmation.md) | Identity e-mail-megerősítés nélkül, jelszó- és zárolási szabályok | 2026-10-28 | elfogadva | P-4A | E-mail-infrastruktúra híján `RequireConfirmedAccount = false`; legalább 10 karakteres jelszó, 5 hibás próbálkozás után 15 perces zárolás. |
| [ADR-006](ADR-006-static-status-code-pages.md) | Statikus renderelés a státuszkód-újrafuttatott oldalakon | 2026-10-29 | elfogadva | P-4A-fix1 | A státuszkód-újrafuttatott (pl. 404-es) kérések statikus SSR-ben renderelődnek, mert ezekből a framework nem ír megőrzött állapotot, és a circuitnek nem volna antiforgery-tokenje. |
| [ADR-007](ADR-007-llm-provider-and-prompt-injection.md) | LLM-szolgáltató és védelem a prompt-injekció ellen | 2026-10-29 | elfogadva | P-8B | Alapértelmezésként lokális Ollama (`gemma3:4b`), cserélhető OpenAI-kompatibilis végpontra; a jegy szövege határolt adatként megy a modellhez, a választ szigorúan validáljuk, és a modell semmilyen műveletet nem indít. |
| [ADR-008](ADR-008-security-headers-csp-report-only.md) | Biztonsági fejlécek és Report-Only CSP | 2026-10-30 | elfogadva | P-9A | Saját middleware állítja be a biztonsági fejléceket (`X-Frame-Options: DENY`, `nosniff`, `Referrer-Policy`, `frame-ancestors 'none'`); a Content Security Policy egyelőre Report-Only, az importmap dinamikusan számolt hash-ével. |
| [ADR-009](ADR-009-roles-in-every-environment.md) | Szerepkörök minden környezetben, demóadat csak Developmentben | 2026-10-30 | elfogadva | P-9A | Az idempotens `RoleSeeder` minden környezetben létrehozza a `Reporter` és `Admin` szerepkört; migráció és demóadat csak Developmentben fut. |

A lépések naplói: [docs/prompts/README.md](../prompts/README.md).
