# Import boundaries

- lib e components/primitives non importano feature, routes o layout.
- common compone primitives e helper generici.
- feature accedono alle altre feature solo tramite index.ts e senza dipendenze cicliche.
- routes sono il livello di composizione; nessun modulo le importa.
- Codice con segreti o API Node non entra nel bundle browser.
- Eventuali moduli server Start futuri devono essere separati esplicitamente.

Si riprende da ACKS la convenzione components/primitives, non il vecchio nome components/ui. Le regole sono controllate da ESLint con eslint-plugin-boundaries e resolver TypeScript. Gli import interni alla stessa feature sono ammessi; gli altri passano da index.ts.
