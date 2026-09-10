# Primo frontend Fantastiche

Approvato dall’utente: avvio frontend come ACKSD, solo dark mode e form login centrato. Primo incremento concordato: login reale e Le mie leghe. Logo fornito dall’utente convertito in PNG, autorizzato come asset del prodotto.

## Esperienza

Aggiornamento visivo richiesto dall’utente: form senza box, bordo o superficie di contenimento; gradiente radiale verde morbido al centro della pagina su fondo scuro. Il gradiente è statico e dà profondità senza animazioni decorative. Su richiesta del 10 settembre, il radiale è molto più raccolto: raggi 220 × 200 px.

Login a colonna singola centrato orizzontalmente e verticalmente, contenitore da 420 px con form interno. Logo sopra il titolo «Bentornato in campo», email e password con etichette persistenti, mostra/nascondi password, CTA «Accedi». Indicazione che gli account vengono attivati tramite invito; niente registrazione o recupero password fittizi. Errori italiani, invio singolo durante pending, focus visibile e autocomplete corretto.

Palette forest green fornita dall’utente: fondo #021010, superficie #081F1C, secondario #133129, accento #224B40, linee e bordi condivisi #a8bcb5 al 10% di opacità, hover al 18%. Radiale al centro sui verdi della palette; crema #f4e8ca per il testo e #f0dfb4 per il pulsante, testo secondario #a8bcb5, errore #f2a59b. Tipografia aggiornata su richiesta dell’utente: Sora per titoli e marchio, Geist per testo e controlli, font variabili self-hosted via Fontsource. Il marchio è il punto caratteristico, nessun hero o pannello laterale nel login. Il bianco esterno del PNG viene nascosto nella presentazione con un ritaglio CSS del solo margine; asset originale preservato.

Pagina autenticata «Le mie leghe»: identità e logout nella testata, lista stagioni delle leghe accessibili, dettaglio con configurazione già disponibile sul backend. Stati caricamento, errore con riprova, vuoto e sessione scaduta. Nessun dato dimostrativo presentato come reale. Mobile da 320 px.

## Architettura

TanStack Start in SPA, React, TypeScript strict, Vite porta 6061, pnpm, Query, Form/Zod, Tailwind e primitives in stile shadcn. Routes sottili, feature auth/leagues con public API, trasporto HTTP condiviso. Cookie HttpOnly del backend, credentials include, token antiforgery richiesto per ogni mutazione e mai salvato in localStorage; rinnovo naturale dopo login/logout. Nessun BFF. API origin configurabile, localhost:6060 in sviluppo; stesso sito con TLS in produzione e CORS esplicito già disponibile sul backend.

GET /api/Leagues restituisce la stagione corrente di ogni lega con membership Active dell’utente, filtrate lato SQL. Stagione corrente selezionata con LeagueSeasons.Id DESC, come nel dettaglio esistente; totalCount conta le leghe. SuperAdmin vede tutte le leghe come nel dettaglio esistente; ruoli verificati sul database. Risposta ApiResponse con elenco paginato (page/pageSize, limite massimo 100), ordinamento stabile. Elementi: id, name, leagueSeasonId, seasonName, budget, goalkeepers, defenders, midfielders, forwards. Nessuna migrazione.

Cache privata eliminata al cambio account/logout; route protette e 401 gestiti senza mascherare errori di rete come logout. Mutazioni senza retry automatico. Le API rimangono autorità per i permessi.

## Verifica e limiti

Test SQL/HTTP elenco: anonimo, membro Active/Pending, isolamento leghe, amministratore e paginazione. Test frontend di login, errori, pending, sessione, cache e trasporto CSRF. Build, typecheck, lint e controllo browser desktop/mobile. Comandi just frontend e avvio documentati. PWA offline, inviti, gestione amministrativa e sala asta sono incrementi successivi; nessun service worker introdotto.

Aggiornamento del 10 settembre: il piccolo radiale contiene la foto di erba fornita dall’utente, convertita in WebP 640×480 a qualità 50 (`fe/src/assets/pitch-grass.webp`). Sfondo centrato 560×420 px senza ripetizione, opacità 40%, saturazione ridotta e maschera ellittica 220×200 px. Sostituisce la prima trama SVG. Effetto statico, decorativo e senza intercettare click; originale sul Desktop preservato.
