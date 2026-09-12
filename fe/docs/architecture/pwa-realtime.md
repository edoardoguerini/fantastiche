# PWA e realtime

Supporto PWA implementato: manifest, icone PNG 192/512 px dal logo autorizzato, apple-touch-icon 180 px e apertura standalone da `/leghe`. SSR e cookie Identity restano invariati. L’installazione richiede HTTPS oppure localhost; l’IP LAN in HTTP non basta per provarla su un telefono.

## Installazione

La build di produzione mostra «Installa app» fuori da asta e inviti. Su Chromium usa il prompt nativo quando disponibile; altrimenti mostra le istruzioni del browser. Su iPhone/iPad si usa Condividi → Aggiungi alla schermata Home; Safari su Mac offre File → Aggiungi al Dock. Il controllo scompare in modalità standalone e dopo l’installazione. Non ci sono prompt automatici o notifiche push.

`public/manifest.webmanifest` definisce identità stabile `/`, nome, colori, icone e scope `/`. Il root include manifest, icona Apple e metadati iOS. Il browser conserva l’autenticazione secondo le proprie regole: il sito continua a usare esclusivamente il cookie HttpOnly.

## Cache e offline

`pnpm build` genera `dist/client/sw.js` da `pwa/sw.js` con `scripts/build-pwa.mjs`, senza nuove dipendenze. La versione deriva dal contenuto di template, asset e risorse offline. Solo la build di produzione registra il worker: Vite dev ne resta privo per evitare cache del codice durante HMR.

Il worker conserva una pagina offline neutra, logo e font Sora; mette in cache al primo uso soltanto gli URL esatti degli asset pubblici della build. Non conserva pagine SSR, redirect, sessioni, dati lega, prezzi, foto remote o risposte API. Esclude richieste con query dagli asset, risposte private/no-store, HTML e redirect. `/api`, `/hubs` e `/_server` passano direttamente dalla rete; nessuna scrittura viene intercettata o accodata.

Le navigazioni richiedono sempre la rete. Se manca, compare «Torniamo in campo appena sei online», senza dati dell’utente; Riprova richiede nuovamente la pagina corrente. Non si può consultare una copia offline della lega o rilanciare offline. Una pagina mai visitata può mostrare il fallback soltanto dopo la prima installazione online del worker.

## Aggiornamenti

Il nuovo worker rimane in attesa finché tutte le schede e finestre dell’app sono chiuse. Nessun `skipWaiting`, messaggio di attivazione forzata o reload automatico: anche due finestre contemporanee conservano la versione attiva. Il primo worker può prendere controllo della pagina aperta senza ricaricarla.

Fuori dalla sala d’asta compare un avviso con le istruzioni per chiudere e riaprire l’app dopo l’asta. Al ritorno in foreground viene cercato un aggiornamento, al massimo una volta al minuto. Quando la nuova versione si attiva, elimina solo le vecchie cache `fantastiche-static-*`. Nginx e la preview servono worker e manifest con `no-cache`; gli asset con hash conservano la cache lunga.

## Realtime

Il timer UI visualizza la scadenza del server. Ogni offerta accettata resetta la durata del giocatore. Rientro dal background e perdita di rete richiedono recupero dello stato. Cleanup delle sottoscrizioni SignalR su smontaggio/cambio lega. Nessun prezzo in cache è usato come autorità.

Il chiamante configura timer e incrementi prima dell’avvio. Pulsanti grandi su touch, ordine di chiamata modificabile anche senza trascinamento. L’importo libero rappresenta il totale, non un incremento.

## Verifiche

Dopo `pnpm build`, `pnpm test:pwa` controlla l’esclusione di API, mutazioni e URL non autorizzati, poi usa Chrome con un profilo temporaneo e il runtime SSR reale. Verifica manifest e requisiti di installabilità, hydration, cache pubblica, fallback senza dati privati, ritorno online senza replay e aggiornamento con due finestre, con pulizia della vecchia cache solo dopo la chiusura.

`pnpm test:runtime` verifica anche worker/manifest nell’immagine nginx + Node. Le prove automatiche non installano l’app nel profilo personale. Installazione dall’interfaccia del sistema, riapertura da Home e sospensione/background su iOS/Android fisici restano verifiche manuali da eseguire sul dominio HTTPS distribuito.

Riferimenti: [installabilità MDN](https://developer.mozilla.org/en-US/docs/Web/Progressive_web_apps/Guides/Making_PWAs_installable), [ciclo di vita dei service worker](https://web.dev/articles/service-worker-lifecycle).
