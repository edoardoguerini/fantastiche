# Quotazioni e ceduti — piano di implementazione

> Esecuzione nella sessione corrente, conservando le modifiche UI già approvate. Verifiche prima dell’aggiornamento locale.

**Obiettivo:** recuperare dal CSV confrontato con l’Excel le quotazioni Classic/Mantra, FVM e stato ceduto; impedire nuove chiamate dei ceduti e mostrare i valori Classic nell’asta.

**Architettura:** campi nullable su ListEntries per distinguere dati storici non importati da zero. Il reimport dello stesso identico CSV (hash già presente) completa soltanto questi campi mancanti; non modifica anagrafica, identificativi, pubblicazione, sessioni, acquisti o budget. Il listone della lega resta quello fissato dalla sessione. I valori di media voto/fantamedia non rientrano nel mapping confermato con questo Excel.

**Stack:** .NET 10, EF Core per schema/importazione; Dapper per asta; React/Zod.

**Specifica:** confronto approvato in conversazione: 594 ID, 532 attivi, 62 ceduti, uguaglianza delle quotazioni e dei FVM tra CSV ed Excel.

## Vincoli
- Colonne CSV (base uno): ruolo Mantra 5, quotazioni 6–9, FVM Classic/Mantra 11–12, ceduto 17 (0/1).
- Stato ceduto non rimuove lo storico degli acquisti; il server lo verifica nella chiamata.
- Import admin, transazione catalogo e hash originali; completamento una tantum dei campi nulli.
- Nessuna offerta inviata nei controlli del browser reale.

## Attività
- [x] Parser, ListEntry e migrazione nullable: test dei valori zero, numeri invalidi, flag 0/1 e campi nuovi; adeguare i CSV sintetici al formato confermato.
- [x] CatalogWorkflow: import completo e recupero dei campi mancanti sullo stesso hash; test di reimport su versione pubblicata, mantenimento ID e idempotenza.
- [x] GetAuctionCatalog e AuctionEngine.Start: filtrare IsTransferred true e rifiutare la chiamata diretta; test su SQL reale, inclusa visibilità nello storico. Esportare valori Classic da query catalogo/stato e tutti i valori dalla consultazione catalogo globale.
- [x] UI: componente PlayerValuation condiviso tra listone, anteprima e giocatore corrente (quotazioni escluse dall’ultimo acquisto su richiesta successiva); distinguere quotazione/FVM dal prezzo d’asta, omettere valori nulli, conservare zero.
- [x] Migrare il database locale e reimportare lo stesso CSV dopo confronto hash. Verificare 62 ceduti, 532 attivi, budget e acquisti invariati, dati Carnesecchi con Qt 17 / iniziale 16 / FVM 57. Aggiornare documentazione e verificare browser/build e test backend pertinenti.

## Esito verificato

Recupero locale sulla medesima versione: 594 righe, 532 attivi e 62 ceduti; 531 disponibili perché Carnesecchi è già acquistato. Confronto prima/dopo conferma squadre, budget, rosa, versione e turno invariati. Backend: 75 test unitari e 98 di integrazione superati; formattazione e allineamento migrazione verificati. Frontend: 44 test unitari e 25 browser superati, typecheck/lint e build completati. Controllo sul browser reale desktop/mobile senza errori JS né overflow: quotazioni nel catalogo, colori pieni sui ruoli, ultimo acquisto senza quotazioni e tab Live senza crediti, secondo le richieste successive. Il recupero amministrativo richiede ricaricare le sale già aperte, come documentato.
