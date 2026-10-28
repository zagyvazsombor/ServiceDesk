# ADR-001 – Blazor Web App, globális Interactive Server, MudBlazor, egyetlen alkalmazás

- Státusz: elfogadva
- Dátum: 2026-10-28

## Kontextus

A ServiceDesk egyetemi projekt („Webes alkalmazások”). Kevés felhasználó, két szerepkör (`Reporter`, `Admin`),
űrlap- és listaközpontú felület, valamint LLM-alapú kategóriajavaslat. Egy fejlesztő dolgozik rajta, korlátozott
idővel. A cél egy jól tesztelhető, rétegzett alkalmazás, amelyben az üzleti szabályok a szerveren érvényesülnek,
és nem kell külön API-t és klienst karbantartani.

## Döntés

- Egyetlen ASP.NET Core alkalmazás: .NET 10 Blazor Web App, a MudBlazor.Templates `mudblazor` sablonjából
  (`dotnet new mudblazor -int Server -au Individual -ai -uld`).
- Globális Interactive Server render mód az `App.razor`-ban, prerender nélkül
  (`new InteractiveServerRenderMode(prerender: false)`). Az Identity-oldalak (`Components/Account`)
  az `[ExcludeFromInteractiveRouting]` miatt statikus SSR-ben futnak.
- UI-könyvtár: kizárólag MudBlazor 9.x (a statikus Identity-oldalakon `Extensions.MudBlazor.StaticInput`).
- Nincs REST API és nincs külön frontend: a komponensek a use case-eket az `IUseCaseRunner`-en át,
  folyamaton belül hívják.

## Alternatívák

- **Blazor WebAssembly vagy Auto mód + REST API:** két telepítési egység, API-szerződés, kliensoldali
  hitelesítés és DTO-szerializáció kellene. A projekt méretéhez ez aránytalan, és megkettőzné a
  jogosultságellenőrzés felületét.
- **Oldalankénti render mód (per-page interactivity):** minden oldalon külön `@rendermode` és provider-kezelés
  kellene, ami könnyen következetlenné válik. A globális mód egyszerűbb, és a providereket egyszer, a layoutban
  lehet felvenni.
- **Prerender bekapcsolva:** kétszeri inicializálást (prerender + interaktív) és kétszeri adatbetöltést okoz,
  ráadásul prerender alatt nincs aktív circuit. Belső alkalmazásnál a SEO- és első-render-előny nem számít.
- **MVC / Razor Pages:** az interaktív triázs- és kiosztási felülethez több kézi JavaScript kellene.
- **Más UI-könyvtár (Bootstrap, Fluent UI, Radzen):** a MudBlazor-sablon kész Identity-integrációt ad statikus
  SSR-hez is. Két UI-könyvtár együtt ütköző stílusokat okozna.

## Következmények

- Az állapot szerveroldali circuitben él: a skálázás a SignalR-kapcsolatok számához kötött. Ez a várható
  terhelésnél nem korlát.
- Az interaktív komponensekben nincs `HttpContext`: a felhasználót az `AuthenticationStateProvider` adja,
  a use case-ek pedig az `ICurrentUser`-t használják.
- A DbContext élettartamát nem a circuit, hanem a use case-enkénti DI-scope határozza meg (`UseCaseRunner`).
- Hálózati kiesésnél a sablon `ReconnectModal`-ja jelenik meg; offline működés nincs.
- A Development környezetben a részletes circuit-hibák be vannak kapcsolva (`DetailedErrors`).
