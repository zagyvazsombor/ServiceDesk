# ADR-006 – Statikus renderelés a státuszkód-újrafuttatott oldalakon

- Státusz: elfogadva
- Dátum: 2026-10-29

## Kontextus

Az alkalmazás globális Interactive Server módban fut, prerender nélkül (AGENTS.md §3, §6, ADR-001). Az `App.razor`
`PageRenderMode` értékéről a sablon logikája szerint a `HttpContext.AcceptsInteractiveRouting()` dönt. Az ismeretlen
URL-ekre a `UseStatusCodePagesWithReExecute("/not-found")` újrafuttatja a kérést.

A hiba (P-4A-fix1): a bejelentkezett felhasználó a 404-es oldalon nem tud kijelentkezni, mert a
`POST Account/Logout` `AntiforgeryValidationException` miatt elbukik.

A gyökérok a .NET 10 frameworkben van:
- Újrafuttatott kérésnél a `RazorComponentEndpointInvoker` nem írja ki a megőrzött komponensállapotot
  (`if (!isErrorHandlerOrReExecuted) { … PrerenderPersistedStateAsync … }`).
- Interaktív módban a circuit csak ebből az állapotból kapja meg az antiforgery-tokent
  (`DefaultAntiforgeryStateProvider`, `[PersistentState]`).
- Ezért az újrafuttatott 404-es válaszból induló circuitben az `<AntiforgeryToken />` semmit sem renderel, és az AppBar
  kijelentkező űrlapja token nélkül megy el.
- A token ugyanebben a circuitben a többi oldalon is hiányzik, egészen egy teljes oldalújratöltésig.

Bizonyíték: a P-4A-fix1 naplója. A 404-es válaszban nincs `Blazor-Server-Component-State` megjegyzés, a normál
oldalak válaszában van.

## Döntés

- Ha a kérés státuszkód-újrafuttatás (`IStatusCodeReExecuteFeature`), az `App.razor` `PageRenderMode` értéke
  `null`, vagyis statikus SSR. Minden más esetben a sablon logikája marad: `AcceptsInteractiveRouting()` esetén
  `InteractiveServerRenderMode(prerender: false)`.
- Statikus rendereléskor az `<AntiforgeryToken />` a kérés `HttpContext`-jéből kap tokent, ezért a kijelentkező
  űrlap a tokennel együtt kerül a HTML-be. Az `Account` oldalak is így működnek.
- Az antiforgery-ellenőrzés nem változik: a kijelentkezés továbbra is antiforgery-védett `POST`.
- A kivételt az AGENTS.md §6 rögzíti.
- A hibakezelő oldalt a framework eleve statikusan rendereli, ezért arra nem kell feltétel; ezt a `Web.Tests`
  `Http/LogoutFormTests` tesztje ellenőrzi.

## Alternatívák

- **`[ExcludeFromInteractiveRouting]` a `NotFound` oldalon.** Elvetve, mert:
  - az alkalmazáson belüli „nem található” navigációt is érintené, pedig ott a circuitnek van tokenje;
  - a sablonoldal nem dokumentált pontján módosítana.
- **A `UseStatusCodePagesWithReExecute` elhagyása.** Elvetve: közvetlenül megnyitott ismeretlen URL-nél a böngésző
  üres 404-es választ kapna, és a NotFound oldal csak az alkalmazáson belül jelenne meg.
- **A megőrzött állapot kikényszerítése az újrafuttatott válaszban.** Nem lehetséges: a framework ezt a lépést
  feltétel nélkül kihagyja, és saját kódból nem pótolható.
- **GET-alapú kijelentkezés, `DisableAntiforgery`, `IgnoreAntiforgeryToken`.** Tiltott (P-4A-fix1), és CSRF-rést
  nyitna.

## Következmények

- A közvetlenül megnyitott 404-es oldalon nincs circuit, az oldal statikus. Az állapotkód továbbra is 404.
- Az oldalról induló linkek gyorsított navigációval interaktív oldalt töltenek be, megőrzött állapottal. Onnantól a
  circuitnek van tokenje.
- Az alkalmazáson belüli navigáció ismeretlen URL-re interaktív marad. A Router `NotFoundPage`-e a meglévő circuitben,
  a meglévő tokennel renderelődik.
- A layout komponenseinek statikus renderelésben is működniük kell, mert a státuszkód-oldalak így jelennek meg.
  A `UserMenu`, a `NavMenu` és a `MudStaticNavDrawerToggle` ma is így működik, ahogy az Account oldalakon.
- Ha egy későbbi .NET-verzió az újrafuttatott válaszhoz is kiírja a megőrzött állapotot, a kivétel felülvizsgálható.
  A `Web.Tests` `Http/LogoutFormTests` tesztjei jelzik, ha a framework viselkedése megváltozik.
