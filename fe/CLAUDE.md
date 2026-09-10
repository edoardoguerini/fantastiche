# Fantastiche Frontend — Regole per assistenti

Applica anche [CLAUDE.md root](../CLAUDE.md). Implementati login centrato solo dark mode, sessione cookie, elenco, dettaglio e creazione leghe per SuperAdmin, sala d’asta realtime e preparazione sessione, attivazione inviti e gestione partecipanti, importazione e pubblicazione listoni per SuperAdmin e scelta listone della lega. [Guida sala d’asta](docs/architecture/auction-room.md). Build, typecheck, ESLint, Prettier, Vitest e Playwright configurati; avvio nel setup.

## Stack e struttura

Palette attuale: Forest Green fornita dall’utente (#406D61, #224B40, #133129, #081F1C, #021010), con testi e CTA crema dal logo. Login senza box, radiale centrato piccolo e campi con etichette visibili. Il radiale contiene la foto di erba fornita dall’utente, compressa in WebP 640×480 (`src/assets/pitch-grass.webp`), statica e sfumata tramite maschera CSS; non deve interferire con il form. Colore attenuato via CSS per restare coerente con la palette.

Linee e bordi sempre minimali: token condiviso `--border`/`--input` verde desaturato al 10% di opacità, hover discreto. Separare le sezioni soprattutto con gli spazi, evitare contorni marcati. Conservare indicatori di focus e di errore riconoscibili.

Tipografia: Sora per i titoli e il nome del marchio, Geist per testi, form e controlli. Font variabili self-hosted tramite Fontsource; nessuna richiesta a Google Fonts.

TanStack Start, React, TypeScript strict, Vite, TanStack Query, Tailwind e shadcn/ui. Manteniamo da ACKS pnpm, TanStack Form + Zod per i form e TanStack Table quando serve una tabella avanzata. Versioni compatibili fissate in package.json e pnpm-lock.yaml; non aggiornare automaticamente alle versioni del riferimento.

- Routes sottili in `src/routes/`, logica e UI di dominio in `src/features/`.
- Ogni feature: `components/`, `actions/`, `validations/`, `types/`, `hooks/` e `index.ts`.
- File kebab-case con suffisso del ruolo: `auction.queries.ts`, `auction.mutations.ts`, `auction.types.ts`.
- Primitives shadcn in `components/primitives/`, componenti trasversali in `components/common/`, shell in `components/layout/`.
- Helper generici e trasporti in `lib/`; semantica d’asta nella feature auctions.
- Import tra feature solo tramite la public API `index.ts`; evitare cicli.
- Nessun import dalle routes. Primitives e lib non dipendono dalle feature.
- `routeTree.gen.ts` sarà generato dal router, mai modificato a mano.
- Font Awesome Pro self-hosted autorizzato dall’utente come ACKSD: CSS, webfont referenziati e licenza in `src/assets/fontawesome`, import globale nel root, componente condiviso `components/common/icon.tsx`. Default Classic Light; icone decorative nascoste agli screen reader, etichetta per icone autonome. Nessun kit CDN o token npm. Le altre risorse premium del riferimento non sono incluse in questa autorizzazione.

Dettaglio: [struttura](docs/architecture/project-structure.md), [confini](docs/architecture/import-boundaries.md).

## Rendering, API e PWA

SPA iniziale con backend .NET separato, frontend porta 6061 e API 6060. Non introdurre il BFF di ACKS, server functions o proxy di autenticazione senza una decisione architetturale specifica. Auth tramite cookie HttpOnly Identity e antiforgery X-XSRF-TOKEN richiesto prima di ogni mutazione; fetch credentials include. Nessun token in localStorage. VITE_API_BASE_URL configura l’origin pubblico API; default localhost:6060 in sviluppo e stessa origin in build.

- API HTTP attraverso un client condiviso, errori tipizzati e messaggi UX in italiano.
- TanStack Query per server state; query key includono lega e stagione quando pertinenti.
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
