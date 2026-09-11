# Dati e stato

TanStack Query per dati del server, context router per il contesto di navigazione e React per stato UI locale. QueryOptions factories e mutations vivono nelle actions della feature.

Query key private includono lega/stagione/risorsa; al logout o cambio lega isolare cache e sottoscrizioni. Guard UI non sostituiscono autorizzazione backend.

Il client HTTP condiviso traduce ApiResponse ed errori in tipi frontend; messaggi italiani comprensibili. Caricamento, errore, vuoto e successo sono stati espliciti.

SignalR aggiorna la cache autorevole con esiti versionati o invalida le query necessarie. Evitare due copie indipendenti del prezzo corrente. Se si rileva una discontinuità o riconnessione, recuperare lo snapshot prima dei nuovi comandi.

L’offerta resta pending fino alla conferma; request ID stabile in caso di esito incerto. Le impostazioni di retry delle normali query non devono trasformarsi in nuovi rilanci.

## Card dell’elenco leghe

`GET /api/Leagues` arricchisce ogni lega con `logoUrl`, `myTeamName` e `auctionStatus`. La squadra è quella dell’utente autenticato nella stagione corrente, con membership attiva; organizzatori o spettatori senza squadra non mostrano un nome fittizio. Lo stato è `NotStarted`, `Active`, `Paused` o `Completed`: la sessione attiva/in pausa ha precedenza, altrimenti si usa l’ultima conclusa. Questi ultimi due campi sono un riepilogo dell’elenco e non sono richiesti dal dettaglio o dalla risposta di creazione lega.

L’elenco si aggiorna ogni 15 secondi mentre la pagina è attiva, con query separata per utente e pagina. La card mostra logo (o iniziale), stagione, stato, nome della lega, squadra e impostazioni «Budget iniziale»/«Rosa da». Il pulsante apre direttamente la sala; per un’asta conclusa è etichettato «Rivedi l’asta». «Dettagli lega» apre la configurazione. Su mobile le azioni occupano tutta la larghezza della card. Il fondo riprende il gradiente viola della sala.
