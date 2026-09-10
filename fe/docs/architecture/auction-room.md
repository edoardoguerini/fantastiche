# Sala d’asta

Implementata nella feature `auctions`, esposta da `/leghe/$leagueId/asta`. Il dettaglio lega contiene il collegamento alla sala. Tema Forest Green/crema, Sora/Geist e Font Awesome locali; bordi condivisi discreti. La pagina distingue preparazione, attesa del proprio turno, chiamata aperta, aggiudicazione, pausa e conclusione.

## Dati e permessi

`GET /Leagues/{leagueId}/Seasons/{seasonId}/AuctionRoom` fornisce squadra personale, permesso di gestione, squadre disponibili e sessione attiva o ultima completata. `myTeamId` è indipendente da `canManage`: l’organizzatore senza squadra osserva e gestisce, senza rilanciare. Il backend verifica tutti i permessi a ogni operazione.

Lo snapshot `/Auctions/Sessions/{id}` contiene prezzo, vincitore provvisorio, turno, budget e conteggi. Catalogo, rose e rilanci sono letture paginate della stessa sessione; il catalogo disponibile esclude acquisti della stagione e giocatore già chiamato. Lo storico mostra acquisti persistiti, mentre Ultimi rilanci mostra offerte accettate per il giocatore corrente. Il tabellone riassume gli ultimi acquisti presenti nella prima pagina di 100 risultati; le rose complete e lo storico restano consultabili con paginazione.

Le query sono separate per utente e risorsa. Il logout cancella richieste e cache; il cambio lega/sessione smonta hook e sottoscrizione. `ApiError.data` conserva anche la ricevuta di una mutazione rifiutata dal motore.

## Sincronizzazione e invii

`use-auction-live` gestisce SignalR con cookie, `WatchSession`, `UnwatchSession` e `AuctionChanged`. La notifica invalida lo snapshot; il client accetta solo versioni successive o, a parità di versione, un tempo server più recente. La stessa regola protegge la cache dalle risposte HTTP tardive. Il polling HTTP ogni 5 secondi e una nuova sottoscrizione ogni 15 secondi recuperano eventuali notifiche perse.

Offline, background e riconnessione bloccano i comandi. Una generazione di sincronizzazione impedisce a un’operazione già in corso di riportare la UI online dopo una disconnessione. Al rientro si recuperano snapshot e contesto prima di riabilitare i controlli. Il countdown usa `serverTime`, `deadline` e `performance.now()`; a zero si attende la chiusura confermata dal server.

Ogni chiamata, rilancio e controllo conserva UUID e payload in `sessionStorage`, con chiave separata per utente/sessione. Il salvataggio precede l’invio; se non è disponibile, il comando non parte. Nessun token è conservato nello storage. I pulsanti restano bloccati fino a ricevuta o rifiuto definitivo.

In caso di risposta persa, il recupero automatico legge solo `/Commands/{requestId}`. L’utente può verificare di nuovo o reinviare esplicitamente lo stesso UUID e importo. Un errore durante recupero/reinvio conserva la richiesta originaria. Nessun rilancio nuovo viene generato automaticamente e nessuna offerta viene accodata offline. Il registro dura quanto la scheda del browser: chiudere definitivamente la scheda elimina `sessionStorage`; le offerte già accettate restano sul server.

## Interazioni

- L’organizzatore apre la sessione scegliendo l’ordine con pulsanti su/giù; tra due giocatori può mettere in pausa, riprendere, saltare turno, riordinare o concludere con conferma.
- Nel proprio turno il partecipante sceglie dal listone, imposta timer da 5 a 30 secondi e incrementi, e chiama a 1 credito. I default sono 15 secondi e 1/5/10.
- I rilanci rapidi e l’offerta libera inviano un totale assoluto. Il massimo riserva un credito per ogni altro posto libero; ruolo pieno, budget, offerta già in testa e scadenza disabilitano le azioni pertinenti.
- Mobile: tabellone orizzontale, tab Listone/La mia rosa/Storico e controlli fissi in basso durante una chiamata, con nome, prezzo e timer. Desktop: rilanci e propria squadra nella colonna laterale. Le tab funzionano anche con frecce, Home ed End.

## Verifiche e limiti dello scope

Vitest copre budget/countdown, recupero della ricevuta, UUID stabile, errori di recupero, concorrenza tra sincronizzazione e disconnessione e risposte HTTP obsolete. Playwright simula HTTP e protocollo WebSocket SignalR, senza modificare il database. È stato inoltre eseguito uno smoke con due account reali su contesti browser distinti fino all’aggiudicazione e all’aggiornamento di budget e rosa.

La lega demo contiene otto squadre fantasy e il catalogo reale fornito dall’utente: 594 giocatori e 20 club. Le card e gli stemmi sono serviti da Azurite, con segnaposto se mancanti o non caricabili. Alcuni PNG della fonte sono card generiche. I giocatori inventati del primo smoke sono stati sostituiti su richiesta. Nessun dato statistico, presenza online o strategia è simulato nell’interfaccia. Inviti, partecipanti e caricamento/pubblicazione del listone sono disponibili dal dettaglio lega e dalla gestione catalogo. Manifest/service worker e deploy sono incrementi successivi.
