# Piano primo frontend

**Obiettivo:** login centrato e Le mie leghe funzionanti con API reali.
**Architettura:** SPA con feature auth/leagues, trasporto cookie/CSRF condiviso, endpoint elenco Dapper.
**Specifica:** [design](../specs/2026-09-09-frontend-bootstrap-design.md).

## Vincoli globali

Solo dark mode; form login centrato. Struttura ACKSD, TypeScript strict e import fra feature via index.ts. Codice inglese, UX/docs italiani. Nessun BFF, dato demo, segreto, asset ACKSD, invio email, push o deploy. Porta frontend 6061 e backend 6060. Cookie e antiforgery esistenti, nessuna migrazione. Si lavora nel checkout condiviso sul nuovo branch feature/fe-bootstrap per mantenere il logo già preparato.

## Attività

- [x] 1. Backend: aggiungere test prima dell’endpoint GET /api/Leagues, query/handler e payload in Infrastructure/Leagues, boundary in Application/Modules/Leagues. Provare isolamento, membership, amministratore e paginazione su database temporanei. Confermare il contratto al frontend.
- [x] 2. Frontend: creare package/config e test API/login; implementare router SPA, primitives, tema, auth e leghe. Testare CSRF, errori e invio concorrente. Build, typecheck e lint con import boundaries.
- [x] 3. Integrazione: comandi just, docs aggiornati, screenshot e verifiche desktop/mobile, flusso browser con API reali in ambiente di test. Review finale del diff e correzione problemi concreti.

## Esecuzione

Skill subagent-driven-development per l’attività backend indipendente mentre il controller implementa il frontend. Review sul diff finale senza commit automatici, preservando la possibilità di revisione locale dell’utente.
