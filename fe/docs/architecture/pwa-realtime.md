# PWA e realtime

Supporto PWA implementato: manifest, icone PNG 192/512 px, apple-touch-icon 180 px e apertura standalone da `/leghe`. Le icone in `public/pwa/` derivano da `logo-pwa.png` fornito dall’utente, con sfondo a gradiente; la favicon dedicata è `public/brand/fantastiche-logo-favicon.png`, anch’essa fornita dall’utente. SSR e cookie Identity restano invariati. L’installazione richiede HTTPS oppure localhost; l’IP LAN in HTTP non basta per provarla su un telefono.

## Installazione

Lo stato PWA vive in `src/lib/pwa/pwa-store.ts`, avviato dal root solo nella build di produzione: cattura il prompt nativo (`beforeinstallprompt`), riconosce la modalità standalone e osserva il worker. Il menu account offre «Installa app» finché l’app non è installata: su Chromium apre il prompt nativo; dove manca, mostra nel menu le istruzioni del browser (iPhone/iPad: Condividi → Aggiungi alla schermata Home; Safari su Mac: File → Aggiungi al Dock).

Nella login compare la card «Porta Fantastiche sulla Home», in alto a destra su desktop e come banner a tutta larghezza in cima allo schermo su mobile, con ingresso ritardato di 2,5 s. «Installa» usa il prompt nativo o apre le istruzioni nella card; «Non ora» la rimanda di 30 giorni sul dispositivo tramite localStorage. La card non compare nella finestra PWA, dopo un’installazione rilevata nella sessione corrente o nelle pagine private. Nelle schede browser un aggiornamento in attesa non nasconde l’invito a installare. Non ci sono prompt automatici o notifiche push.

`public/manifest.webmanifest` definisce identità stabile `/`, nome, colori, icone e scope `/`. Il root include manifest, icona Apple e metadati iOS. Il browser conserva l’autenticazione secondo le proprie regole: il sito continua a usare esclusivamente il cookie HttpOnly.

Con `viewport-fit=cover` e la barra di stato iOS `black-translucent`, l’header delle pagine ordinarie aggiunge `env(safe-area-inset-top, 0px)` sia al padding superiore sia all’altezza: logo e menu restano sotto orologio e batteria, mantenendo lo spazio utile di 76 px su mobile e 88 px su desktop. La sala d’asta conserva la propria gestione della safe area. Nei browser senza inset l’header mantiene le dimensioni ordinarie.

## Cache e offline

`pnpm build` genera `dist/client/sw.js` da `pwa/sw.js` con `scripts/build-pwa.mjs`, senza nuove dipendenze. La versione deriva dal contenuto di template, asset e risorse offline. Solo la build di produzione registra il worker: Vite dev ne resta privo per evitare cache del codice durante HMR.

Il worker conserva una pagina offline neutra, logo e font Sora; mette in cache al primo uso soltanto gli URL esatti degli asset pubblici della build. Non conserva pagine SSR, redirect, sessioni, dati lega, prezzi, foto remote o risposte API. Esclude richieste con query dagli asset, risposte private/no-store, HTML e redirect. `/api`, `/hubs` e `/_server` passano direttamente dalla rete; nessuna scrittura viene intercettata o accodata.

Le navigazioni richiedono sempre la rete. Se manca, compare «Torniamo in campo appena sei online», senza dati dell’utente; Riprova richiede nuovamente la pagina corrente. Non si può consultare una copia offline della lega o rilanciare offline. Una pagina mai visitata può mostrare il fallback soltanto dopo la prima installazione online del worker.

## Aggiornamenti

Il nuovo worker rimane in attesa finché tutte le schede e finestre dell’app sono chiuse. Nessun `skipWaiting`, messaggio di attivazione forzata o reload automatico: anche due finestre contemporanee conservano la versione attiva. Il primo worker può prendere controllo della pagina aperta senza ricaricarla.

Solo quando Fantastiche è aperta come PWA, fuori dalla sala d’asta e dagli inviti, compare un toast «Nuova versione pronta» in basso, centrato su desktop e a tutta larghezza su mobile, con le istruzioni per chiudere e riaprire l’app dopo l’asta; la X lo nasconde fino al prossimo caricamento. Nelle normali schede browser il toast non compare, anche se l’app è stata appena installata: lo stato di installazione è distinto dalla modalità della finestra corrente. Al ritorno in foreground viene cercato un aggiornamento, al massimo una volta al minuto. Quando la nuova versione si attiva, elimina solo le vecchie cache `fantastiche-static-*`. Nginx e la preview servono worker e manifest con `no-cache`; gli asset con hash conservano la cache lunga.

## Realtime

Il timer UI visualizza la scadenza del server. Ogni offerta accettata resetta la durata del giocatore. Rientro dal background e perdita di rete richiedono recupero dello stato. Cleanup delle sottoscrizioni SignalR su smontaggio/cambio lega. Nessun prezzo in cache è usato come autorità.

Il chiamante configura timer e incrementi prima dell’avvio. Pulsanti grandi su touch, ordine di chiamata modificabile anche senza trascinamento. L’importo libero rappresenta il totale, non un incremento.

## Verifiche

Dopo `pnpm build`, `pnpm test:pwa` controlla l’esclusione di API, mutazioni e URL non autorizzati, poi usa Chrome con un profilo temporaneo e il runtime SSR reale. Verifica manifest e requisiti di installabilità, hydration, cache pubblica, fallback senza dati privati, ritorno online senza replay e aggiornamento con due finestre, con pulizia della vecchia cache solo dopo la chiusura.

`pnpm test:runtime` verifica anche worker/manifest nell’immagine nginx + Node. Le prove automatiche non installano l’app nel profilo personale. Installazione dall’interfaccia del sistema, riapertura da Home e sospensione/background su iOS/Android fisici restano verifiche manuali da eseguire sul dominio HTTPS distribuito.

Riferimenti: [installabilità MDN](https://developer.mozilla.org/en-US/docs/Web/Progressive_web_apps/Guides/Making_PWAs_installable), [ciclo di vita dei service worker](https://web.dev/articles/service-worker-lifecycle).
