# Checklist review backend

Review in sola lettura del diff e dei file correlati. Distinguere bug dimostrati e aspetti da verificare.

- Dipendenze tra progetti e struttura feature rispettate; boundary senza business logic.
- Identity senza account duplicati; permessi per lega verificati anche negli handler e negli Hub.
- Dapper parametrizzato e filtrato; nessuna fiducia implicita negli interceptor/query filter EF.
- Un solo percorso di scrittura, transazioni complete e nessun evento prima del commit.
- Timer, gara offerta/chiusura, budget, duplicati e assegnazione una sola volta.
- Migration, mapping e SQL Dapper allineati; audit copre le scritture SQL.
- Configurazione per ambiente senza segreti e test adeguati su SQL Server reale.
- Letture con proiezioni/paginazione e indici giustificati.
- Log e risposte senza dati sensibili.

Report in italiano: severità, file/riga, scenario concreto, effetto e correzione suggerita. Se non emergono rilievi, indicare ambito verificato e test non eseguiti. Non inventare esiti di build.
