# Piano di implementazione SSR frontend

**Obiettivo:** login già renderizzata nella risposta HTTP e verifica della
sessione sul server, mantenendo l'asta interattiva e la compatibilità PWA.

**Specifica:** [design SSR](../specs/2026-09-12-frontend-ssr-design.md).

**Stack:** TanStack Start, React Query, Identity .NET, Node 24 e nginx.
Esecuzione nella sessione corrente sul branch `feature/fe-ssr`.

## 1. Rendering e sessione

- [x] Aggiungere test di `auth.server.ts` con cookie, 401, errore e concorrenza;
      eseguire `pnpm test src/features/auth/actions/__tests__/auth.server.test.ts`.
- [x] Separare il parsing ApiResponse in `lib/api/response.ts`; aggiungere il
      resolver server della sessione e selezionarlo con `createIsomorphicFn`.
- [x] Rimuovere `spa.enabled` in Vite e integrare Query SSR in `router.tsx`.
- [x] Impostare gli header privati nel root e SSR selettivo per invito/asta.
- [x] Verificare HTML del login, redirect e hydration con API simulate sul server.

## 2. Runtime e regressioni

- [x] Aggiungere entry Node `server/index.mjs` e script start/preview.
- [x] Aggiornare Dockerfile, nginx, supervisione e healthcheck mantenendo 8080,
      API_UPSTREAM, proxy WebSocket e gestione dell'IP client esistenti.
- [x] Adattare fixture Playwright perché anche SSR usi sessioni simulate isolate;
      mantenere i test browser di auth, leghe, catalogo, inviti e asta.
- [x] Eseguire typecheck, lint, format, test unitari, browser e build SSR;
      verificare runtime di produzione e immagine Docker.

## 3. Documentazione e review

- [x] Aggiornare regole condivise, guide FE e infra per SSR e futura PWA.
- [x] Applicare checklist di review FE al diff e verificare che cookie e
      configurazione interna non entrino nel bundle browser o nella cache HTML.
- [x] Riportare verifiche effettive e limiti senza eseguire deploy remoto.

## Esito

71 test unit/component, 120 test browser SSR e 38 su runtime compilato passati;
smoke Docker con API HTTP e WebSocket simulati passato. Typecheck, lint, format
e build superati. Review indipendente: protezioni dei form SSR estese a lega e
catalogo; corretto teardown dei processi test già terminati. Le cache Vite dei
worker sono isolate. Integrazione Query fissata a 1.167.2, compatibile con
Query 5.102.8; la 1.167.1 del riferimento aveva un errore alla fine dello stream.
Nessun deploy remoto.
