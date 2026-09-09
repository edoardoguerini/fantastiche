# Fantastiche Frontend — Regole per assistenti

Applica anche [CLAUDE.md root](../CLAUDE.md). Convenzioni per il frontend da realizzare; nessun package o controllo lint è ancora installato.

## Stack e struttura

TanStack Start, React, TypeScript strict, Vite, TanStack Query, Tailwind e shadcn/ui. Manteniamo da ACKS pnpm, TanStack Form + Zod per i form e TanStack Table quando serve una tabella avanzata. Versioni compatibili da fissare nel bootstrap, non copiate dal lockfile del riferimento.

- Routes sottili in `src/routes/`, logica e UI di dominio in `src/features/`.
- Ogni feature: `components/`, `actions/`, `validations/`, `types/`, `hooks/` e `index.ts`.
- File kebab-case con suffisso del ruolo: `auction.queries.ts`, `auction.mutations.ts`, `auction.types.ts`.
- Primitives shadcn in `components/primitives/`, componenti trasversali in `components/common/`, shell in `components/layout/`.
- Helper generici e trasporti in `lib/`; semantica d’asta nella feature auctions.
- Import tra feature solo tramite la public API `index.ts`; evitare cicli.
- Nessun import dalle routes. Primitives e lib non dipendono dalle feature.
- `routeTree.gen.ts` sarà generato dal router, mai modificato a mano.
- Non copiare font, icone Pro o componenti premium dal riferimento.

Dettaglio: [struttura](docs/architecture/project-structure.md), [confini](docs/architecture/import-boundaries.md).

## Rendering, API e PWA

SPA iniziale proposta con backend .NET separato. Non introdurre il BFF di ACKS, server functions o proxy di autenticazione senza una decisione architetturale specifica. Il meccanismo cookie/token va ancora progettato.

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

Test unitari e componenti co-locati in `__tests__/`; `tests/` per verifiche trasversali ed end-to-end. Strumenti di riferimento: Vitest, Testing Library e Playwright, da installare nel bootstrap.

Verificare errori, loading, disconnessione, doppio clic, cambio lega e layout. Non presentare controlli lint o test come attivi finché non sono configurati. [Checklist review](docs/quality/code-review.md).
