# Fantastiche Frontend — Regole per assistenti

Applica anche [CLAUDE.md root](../CLAUDE.md). Implementati login centrato solo dark mode, sessione cookie, elenco, dettaglio e creazione leghe per SuperAdmin, sala d’asta realtime e preparazione sessione, attivazione inviti e gestione partecipanti, importazione e pubblicazione listoni per SuperAdmin e scelta listone della lega. [Guida sala d’asta](docs/architecture/auction-room.md). Build, typecheck, ESLint, Prettier, Vitest e Playwright configurati; avvio nel setup.

## Stack e struttura

Palette globale approvata dall’utente: nero (#0C0C0E), superfici antracite (#18181B, #202023, #242427), testi neutri (#F4F4F5, #AAAAB3) e accento lilla (#C4A1FF) per azioni, selezioni e focus. I token condivisi sono in `src/styles/globals.css` e si applicano a login, inviti, leghe, catalogo e sala d’asta.

Login centrato senza box, campi con etichette visibili e sfondo CSS a gradiente radiale viola (#30243E, #1A1125) che sfuma nel nero. Lo stesso sfondo è condiviso dalla pagina degli inviti, che usa un pannello da 460 px con carta della lega, passi solo per l’account esistente e checklist password: [guida inviti](docs/architecture/invitations.md). La texture del campo non è più utilizzata. La sala d’asta eredita la palette globale; i colori dei ruoli e degli stati conservano il significato.

Linee e bordi sempre minimali: token condiviso `--border`/`--input` bianco all’8% di opacità, hover discreto al 14%. Separare le sezioni soprattutto con gli spazi, evitare contorni marcati. Nei campi e nei select il focus usa un solo bordo lilla, senza anello esterno aggiuntivo. Conservare indicatori di focus da tastiera e di errore riconoscibili.

Tipografia: Sora per tutta l’interfaccia, inclusi titoli, marchio, testi, form e controlli. Font variabile self-hosted tramite Fontsource; nessuna richiesta a Google Fonts.

Header e contenuti ordinari condividono larghezza utile di 1100 px e margini laterali minimi di 24 px (20 px su mobile), definiti dai token della shell. Il titolo dell’elenco leghe misura 26–32 px e dista 4 px dal sottotitolo.

TanStack Start, React, TypeScript strict, Vite, TanStack Query, Tailwind e shadcn/ui. Manteniamo da ACKS pnpm, TanStack Form + Zod per i form e TanStack Table quando serve una tabella avanzata. Versioni compatibili fissate in package.json e pnpm-lock.yaml; non aggiornare automaticamente alle versioni del riferimento.

- Routes sottili in `src/routes/`, logica e UI di dominio in `src/features/`.
- Ogni feature: `components/`, `actions/`, `validations/`, `types/`, `hooks/`, `utils/` per helper puri della feature e `index.ts`.
- File kebab-case con suffisso del ruolo: `auction.queries.ts`, `auction.mutations.ts`, `auction.types.ts`.
- Primitives shadcn in `components/primitives/`, componenti trasversali in `components/common/`, shell in `components/layout/`.
- Helper generici e trasporti in `lib/`; semantica d’asta nella feature auctions.
- Import tra feature solo tramite la public API `index.ts`; evitare cicli.
- Nessun import dalle routes. Primitives e lib non dipendono dalle feature.
- `routeTree.gen.ts` sarà generato dal router, mai modificato a mano.
- Font Awesome Pro self-hosted autorizzato dall’utente come ACKSD: CSS, webfont referenziati e licenza in `src/assets/fontawesome`, import globale nel root, componente condiviso `components/common/icon.tsx`. Default Classic Light; icone decorative nascoste agli screen reader, etichetta per icone autonome. Nessun kit CDN o token npm. Le altre risorse premium del riferimento non sono incluse in questa autorizzazione.

Dettaglio: [struttura](docs/architecture/project-structure.md), [confini](docs/architecture/import-boundaries.md).

## Rendering, API e PWA

PWA installabile nella build di produzione, con pagina offline neutra e cache dei soli asset pubblici. Nessuna cache di HTML privato o API e nessun aggiornamento forzato: tutte le finestre devono chiudersi prima che il nuovo worker si attivi. Vite dev non registra service worker. Guida e verifiche in [PWA](docs/architecture/pwa-realtime.md).

SSR TanStack Start con backend .NET separato, frontend porta 6061 e API 6060. Passaggio approvato il 12 settembre 2026 come ACKSD, mantenendo il cookie Identity. La login e i guard sono eseguiti anche sul server; inviti (token nel fragment) e UI della sala usano `ssr: false`, senza disabilitare il guard del layout privato. Nessun BFF JWT o nuovo percorso di scrittura. Auth tramite cookie HttpOnly Identity e antiforgery X-XSRF-TOKEN richiesto prima di ogni mutazione; fetch credentials include. Nessun token in localStorage. VITE_API_BASE_URL configura l’origin pubblico API; default localhost:6060 in sviluppo e stessa origin in build. API_UPSTREAM è server-only, configura il backend della verifica SSR (default http://localhost:6060). Il cookie Identity deve raggiungere anche il frontend: in produzione nginx espone API e frontend sulla stessa origin.

- API HTTP attraverso un client condiviso, errori tipizzati e messaggi UX in italiano. Il resolver `features/auth/actions/auth.server.ts` usa lo stesso parser delle risposte, inoltra solo i cookie Identity e non condivide stato tra richieste. `start.ts` preserva gli errori UX nella serializzazione e applica `private, no-store` anche ai redirect.
- TanStack Query per server state; un QueryClient per richiesta SSR e hydration tramite l’integrazione ufficiale del router. Query key includono lega e stagione quando pertinenti.
- Pulire/separare cache e sottoscrizioni al cambio account o lega.
- Stato UI locale con React; form tramite TanStack Form e Zod.
- Il client SignalR gestisce connessione e sottoscrizioni; gli eventi hanno versione per ignorare notifiche obsolete.
- Non applicare retry ciechi delle offerte. Un duplicato mantiene lo stesso ID richiesta e payload.
- Offerta in attesa fino alla conferma; al rientro recuperare lo stato prima di riabilitare i rilanci.
- Niente accodamento offline; nessun reload automatico per aggiornamento PWA durante l’asta.
- Layout touch per telefono e tablet, vista desktop; drag & drop con alternativa accessibile.

Guide: [dati](docs/architecture/data-fetching.md), [PWA](docs/architecture/pwa-realtime.md).

## Qualità

Test unitari e componenti co-locati in `__tests__/`; `tests/` per verifiche trasversali ed end-to-end. Strumenti di riferimento: Vitest, Testing Library e Playwright, installati nel bootstrap.

Verificare errori, loading, disconnessione, doppio clic, cambio lega e layout. Non presentare controlli lint o test come attivi finché non sono configurati. [Checklist review](docs/quality/code-review.md).
