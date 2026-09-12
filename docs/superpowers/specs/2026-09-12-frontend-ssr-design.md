# Rendering SSR del frontend

Decisione approvata il 12 settembre 2026: adottare il rendering server di
TanStack Start come ACKSD, preservando la compatibilità con la futura PWA.

## Comportamento

La risposta HTTP a `/login` contiene il form completo. In assenza del cookie
Identity non serve interrogare l'API. Con cookie, il server verifica `/api/Auth/Me`
prima del rendering e reindirizza alle leghe quando la sessione è valida.
Le route private verificano la sessione anche al primo accesso HTTP. Errori di
rete non diventano logout. Le autorizzazioni restano applicate dal backend .NET.

## Architettura

Disabilitare SPA mode e integrare Query con SSR: un QueryClient per richiesta,
trasferimento al browser dei dati risolti, nessuna cache globale di sessione.
La query di autenticazione è isomorfa: browser verso API pubblica; server verso
`API_UPSTREAM`, mai verso un host ricavato dagli header della richiesta.
Il cookie `Fantastiche.Auth`, inclusi eventuali chunk Identity, viene inoltrato
solo al backend configurato. I cookie di risposta Identity vengono propagati.
L'HTML autenticato e i redirect non sono memorizzabili in cache condivise.

Manteniamo cookie HttpOnly Identity e antiforgery esistenti, senza introdurre
token JWT, nuove server function di login o un secondo percorso di scrittura.
L'invito legge il token dal fragment nel browser; la sua route usa SSR selettivo
disabilitato. Anche la sala d'asta mantiene la UI client, dopo il guard server
del layout privato, perché dipende da storage e connessione realtime.

## Runtime

L'immagine FE mantiene nginx su 8080 per statici, API e WebSocket e aggiunge
Node/TanStack Start su loopback per il rendering. Entrambi i processi sono
supervisionati; gli header fidati e l'IP client verso .NET restano gestiti da
nginx. Asset con hash immutabili, documenti SSR `private, no-store`.
In sviluppo il frontend resta su 6061; API pubblica e upstream SSR locale su 6060.

## PWA e ambito

Manifest e service worker restano un incremento successivo. SSR non impedisce
installazione o cache degli asset; HTML privato, sessioni, API e offerte non
devono essere serviti da cache offline. Nessun aggiornamento automatico durante
l'asta. Nessun provisioning o deploy remoto in questo intervento.

## Verifica

Test del resolver server: anonimo senza chiamata, cookie valido/scaduto, chunk,
errori backend/rete, propagazione cookie e isolamento tra richieste concorrenti.
Test HTTP del documento senza JavaScript e redirect; browser per hydration,
login/logout, ruoli, inviti e asta. Typecheck, ESLint, Prettier, Vitest, build
e smoke dell'immagine di produzione quando Docker è disponibile.
