# Moduli e trasporti

Pattern implementato nel bootstrap: un `IRegistrableModule` per feature registra un `MapGroup` sotto `/api`. I segmenti risorsa sono PascalCase, ad esempio `/api/Leagues/{leagueId}`. Ogni endpoint documenta summary, description e autorizzazione.

Il modulo riceve il payload, esegue validazione sintattica e costruisce il contesto autenticato; delega tramite IRequestPublisher. Nessuna business logic nei moduli.

Contratto HTTP previsto: `ApiResponse<T>` con esito, dati ed errori codificati, creato tramite `ApiResults`. Gli status HTTP mantengono il loro significato. Health/OpenAPI sono endpoint infrastrutturali separati dal contratto business.

Gli Hub SignalR hanno un contratto di messaggi distinto ma codici di errore coerenti. Gli esiti di offerte includono request ID e versione dello stato. Autorizzare ingresso nel gruppo e ogni comando sulla risorsa. Pubblicazione dopo il commit.
