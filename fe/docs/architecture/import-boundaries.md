# Import boundaries

- lib e components/primitives non importano feature, routes o layout.
- common compone primitives e helper generici.
- feature accedono alle altre feature solo tramite index.ts e senza dipendenze cicliche.
- routes sono il livello di composizione; nessun modulo le importa.
- Codice con segreti o API Node non entra nel bundle browser.
- Eventuali moduli server Start futuri devono essere separati esplicitamente.

Si riprende da ACKS la convenzione components/primitives, non il vecchio nome components/ui. Le regole diventeranno controlli ESLint nel bootstrap; oggi sono solo documentate.
