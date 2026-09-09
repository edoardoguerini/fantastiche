# Dati e stato

TanStack Query per dati del server, context router per il contesto di navigazione e React per stato UI locale. QueryOptions factories e mutations vivono nelle actions della feature.

Query key private includono lega/stagione/risorsa; al logout o cambio lega isolare cache e sottoscrizioni. Guard UI non sostituiscono autorizzazione backend.

Il client HTTP condiviso traduce ApiResponse ed errori in tipi frontend; messaggi italiani comprensibili. Caricamento, errore, vuoto e successo sono stati espliciti.

SignalR aggiorna la cache autorevole con esiti versionati o invalida le query necessarie. Evitare due copie indipendenti del prezzo corrente. Se si rileva una discontinuità o riconnessione, recuperare lo snapshot prima dei nuovi comandi.

L’offerta resta pending fino alla conferma; request ID stabile in caso di esito incerto. Le impostazioni di retry delle normali query non devono trasformarsi in nuovi rilanci.
