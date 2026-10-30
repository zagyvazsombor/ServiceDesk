# ADR-008 – Biztonsági fejlécek és Report-Only CSP

- Státusz: elfogadva
- Dátum: 2026-10-30

## Kontextus

A P-9A átvizsgálás előtt az alkalmazás egyetlen biztonsági fejlécet sem állított be. Két fejlécet a keretrendszer
magától írt, és ezek ellentmondtak egymásnak:

- Az antiforgery minden tokenes oldalra `X-Frame-Options: SAMEORIGIN`-t ír.
- Az Interactive Server végpontokra a keretrendszer kikényszerített `Content-Security-Policy: frame-ancestors 'self'`
  fejlécet ír (`ServerComponentsEndpointOptions.ContentSecurityFrameAncestorsPolicy`).
- A modern böngészők a CSP `frame-ancestors`-t előnyben részesítik az `X-Frame-Options`-szel szemben. Egy
  `X-Frame-Options: DENY` ezért önmagában hatástalan lenne.

A Content Security Policy-t a MudBlazor és a Blazor-kapcsolat működése szabja meg:

- Az `<ImportMap />` (`App.razor`) inline `<script type="importmap">`-et ír. A tartalma a fingerprintelt asset-URL-ek
  miatt buildenként változhat.
- A MudBlazor `<style>` elemet (`MudThemeProvider`) és `style` attribútumokat renderel.
- A circuit WebSocketen fut, ugyanarra az originre.
- Az `App.razor` a Google Fonts-ból tölt betűtípust.

## Döntés

- Kis middleware (`Web/Security/SecurityHeadersMiddleware`) a pipeline elején, a `Response.OnStarting`-ban állítja
  be a fejléceket:
  - `X-Content-Type-Options: nosniff`, `Referrer-Policy: strict-origin-when-cross-origin`, `X-Frame-Options: DENY`.
  - Az `OnStarting` miatt a fejlécek a kivételkezelő oldalán (amely előtte törli a fejléceket), a státuszkód-oldalakon
    és a statikus fájlokon is megjelennek. Az antiforgery `SAMEORIGIN` értékét is felülírják.
- A keretrendszer saját `frame-ancestors` fejléce `'none'` értéket kap
  (`AddInteractiveServerRenderMode(o => o.ContentSecurityFrameAncestorsPolicy = "'none'")`). Ez ugyanazt jelenti,
  mint az `X-Frame-Options: DENY`. Ez az egyetlen kikényszerített CSP.
- `Content-Security-Policy-Report-Only`:
  `default-src 'self'; script-src 'self' 'sha256-<importmap>'; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; font-src 'self' https://fonts.gstatic.com; img-src 'self' data:; connect-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'`
  - Az importmap hash-e futásidőben készül, ugyanabból az `ImportMapDefinition`-ből (a végpont metaadata), amelyet az
    `<ImportMap />` kiír. A hash nincs beégetve.
  - A böngésző a feldolgozott script szövegének hash-ét veszi. A HTML-feldolgozás a CR LF-et LF-re cseréli, a JSON
    pedig Windows-on CR LF-fel van tagolva, ezért a middleware ugyanígy normalizál.
  - A policy definíciónként egyszer készül el (`ConditionalWeakTable`).
  - Nincs `report-uri`: az új végpont volna. A jelentések a böngészőkonzolban látszanak.
- Ellenőrzés:
  - `SecurityHeadersTests`: minden választípus, a hash egyezése az oldal importmapjével, a keretrendszer
    `frame-ancestors` értéke.
  - Kézi böngészős próba: CSP-jelentés nélkül működő oldalak és circuit.

## Alternatívák

- **Beégetett hash:** buildenként elavulna. Elvetve (a felhasználó is kizárta).
- **Nonce az `<ImportMap nonce=…>`-on:** kérésenkénti nonce és az `App.razor` sablonfájl módosítása kellene hozzá.
  Elvetve, mert a hash sablonmódosítás nélkül is pontos.
- **`script-src 'self'` hash nélkül:** minden oldalbetöltés egy Report-Only jelentést adna az importmapre, ami
  elfedné a valódi jelentéseket. Elvetve.
- **Kikényszerített CSP:** a feladat Report-Only módot kért. A `'unsafe-inline'` stílus miatt előbb megfigyelési
  időszak kell.
- **Kész csomag a fejlécekhez (pl. NetEscapades.AspNetCore.SecurityHeaders):** nincs az engedélyezett csomagok
  között (§3).

## Következmények

- Minden válasz megkapja a három fejlécet. Az alkalmazás iframe-be sehonnan nem ágyazható be.
- A CSP nem véd, amíg nincs kikényszerítve; a `'unsafe-inline'` stílus marad (MudBlazor).
- A hash a keretrendszer kiírására épül (`ImportMapDefinition.ToString()`). Ha egy frissítés megváltoztatja, a
  `SecurityHeadersTests` jelzi; ekkor a nonce-megoldás a tartalék.
- Fejlesztéskor a `dotnet watch` böngészőfrissítő scriptje Report-Only jelentést adhat; ez nem hiba.
