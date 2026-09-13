# Sicurezza trasversale

- Segreti fuori dal repository; esempi di configurazione con soli placeholder. Credenziali e identificativi dell’ambiente ACKS non vanno riutilizzati.
- Identity è la fonte degli account. Appartenenza alla lega e accesso alla risorsa si verificano sul backend, in HTTP e SignalR.
- Query Dapper parametrizzate con scope esplicito. Un ID fornito dal client non prova il diritto di accesso.
- Il frontend non riceve chiavi SQL, Storage Account o provider; auth con cookie HttpOnly Identity, fetch credentials include e token antiforgery per ogni mutazione. Cache Query in memoria eliminata al cambio account e logout; nessun token in localStorage o nello storage della PWA.
- Il login emette un cookie persistente di 7 giorni, rinnovato dalle richieste autenticate, con limite assoluto di 30 giorni dal login. La data iniziale resta nel ticket protetto e non cambia al rinnovo; anche la scadenza del ticket/cookie è limitata a quella data. La verifica del security stamp Identity resta attiva ogni 5 minuti sulle richieste e può revocare la sessione. Dettagli e compatibilità in [sessioni](../../be/docs/architecture/authentication.md).
- Logging senza password, token, connection string o payload personali completi.
- CORS e origini ammesse configurati per ambiente; accesso autenticato ai gruppi SignalR.
- Validazione client per UX, invarianti e concorrenza sul server.
- Dati/card del listone: conservare la provenienza e verificare il riuso commerciale prima della pubblicazione.
- Review delle licenze delle dipendenze e degli asset effettivamente introdotti, considerando il futuro prodotto commerciale.
- Nessun comando di reset database/volumi o modifica di ambienti esterni è implicito nella preparazione della documentazione.
