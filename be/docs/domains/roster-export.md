# Esportazione rose per Leghe Fantacalcio

## Uso

Nella sala d’asta, tab **Rose**, «Esporta per Fantacalcio.it» scarica gli acquisti
salvati dell’intera stagione. Sono incluse le assegnazioni delle sessioni
precedenti; sono escluse le offerte ancora aperte e le squadre senza acquisti.
Il download è disponibile agli utenti che possono leggere le rose (membri
attivi della lega e SuperAdmin verificato sul database).

L’admin della lega di destinazione carica il file in **Gestione rose → Importa
rose**, associa le rose alle squadre e controlla gli acquisti prima di salvare.
Stagione, budget e composizione delle rose vanno allineati fra le piattaforme.
Durante l’asta il CSV è una fotografia degli acquisti conclusi, non una
sincronizzazione automatica. Le rose possono essere incomplete anche dopo
la chiusura manuale della sessione.

## Tracciato verificato il 15 settembre 2026

Fonti primarie:

- [Guida ufficiale alla gestione delle rose](https://leghe.fantacalcio.it/guide-leghe-fantacalcio/gestione-mercato/guida-alla-gestione-delle-rose---come-caricare-le-rose-dopo-lasta-102).
- [FantaAsta Live ufficiale](https://www.fantacalcio.it/app-fantaasta).
- [Modulo pubblico FantaAsta con esportatore e importatore](https://fanta-asta-live.fantacalcio.it/chunk-QQVPDJ2F.js), referenziato da `main-D3X2EBDD.js` e dal modulo rose `chunk-IASY5T3X.js`. URL con hash soggetto a cambiare.
- [Helper pubblico di download UTF-8](https://fanta-asta-live.fantacalcio.it/chunk-CLCCBIB4.js).

Formato osservato: UTF-8 **senza BOM**, righe terminate da **LF**, virgola come
separatore, nessuna intestazione. Prima di ciascuna squadra si scrive `$,$,$`.
Ogni acquisto contiene tre campi: nome fantasquadra, ID numerico Fantacalcio,
prezzo pagato. Anche l’ultima riga termina con LF. Esempio sintetico:

```text
$,$,$
Prima,123,7
Prima,789,1
$,$,$
Seconda,456,12
```

Il parser pubblico divide le righe e i campi senza supporto al quoting CSV.
Per questo l’export rifiuta nomi vuoti, virgole, virgolette, caratteri di
controllo e nomi che iniziano, ignorando gli spazi, con `=`, `$`, `+`, `-`, `@`.
Gli ultimi simboli evitano anche l’interpretazione del nome come formula nei
fogli di calcolo. Spazi esterni rimossi; apostrofi interni e accenti preservati.
La squadra resta distinta tramite blocco, non tramite il suo ID interno.

Gli ID provengono da `Players.ExternalId` con `Source = FantacalcioCsv`:
non si esportano GUID applicativi né si associano calciatori per nome.
Sono richiesti ID interi positivi a 32 bit e prezzi interi non negativi.
Una riga incompatibile rifiuta l’intero export, senza file parziali.

## Contratto e consistenza

`GET /api/Auctions/Sessions/{sessionId}/Roster/Export`

Risposta `ApiResponse<AuctionRosterExportView>` con `fileName` e `csv`.
Nome file: `fantastiche-rosters-{leagueSeasonId:N}.csv`.
Header `Cache-Control: private, no-store`. Il client usa il trasporto HTTP
comune, crea un Blob UTF-8 senza BOM e avvia il download senza salvare il CSV
nella cache TanStack Query o nello storage del browser. Richieste duplicate
bloccate durante l’attesa, richiesta annullata all’uscita dalla sala o al
cambio di identità/sessione.

`AuctionReadSession` verifica accesso e stagione e acquisisce lo stesso lock
condiviso delle altre letture d’asta. Una singola query Dapper legge tutti gli
acquisti con scope esplicito di lega e stagione; nessuna paginazione o modifica
al database. La generazione avviene dopo il commit della lettura, senza
trattenere il lock durante la serializzazione. Nessuna migrazione richiesta.

Errori 409: `auction.export_empty`, `auction.export_invalid_player`,
`auction.export_invalid_team_name`, `auction.export_invalid_price`.
Gli errori di autenticazione/autorizzazione mantengono 401/403/404.

## Verifica e limite

Test unitari sul tracciato esatto e sugli input incompatibili; test SQL con
103 acquisti, due sessioni, isolamento tra leghe e claim SuperAdmin non
confermato dal database; test HTTP su autorizzazione, risposta, prezzi e cache;
test browser sul file scaricato, UTF-8 senza BOM, errori e clic duplicati;
test del componente sull’annullamento del trasporto e la risposta tardiva.

Verifiche locali del 15 settembre: 147 test unitari backend, 152 test di
integrazione SQL/HTTP, 144 test frontend e 3 test browser dedicati superati.
Build frontend di produzione, TypeScript, ESLint, Prettier e formatter .NET
superati. Review diretta del diff secondo le checklist backend/frontend:
scope delle letture, ID della fonte, prezzi, formato, download e permessi.

Un campione sintetico coincidente con quello del test unitario è stato letto
anche dal parser estratto dal bundle pubblico ufficiale: due squadre e tre
acquisti riconosciuti con gli stessi ID e prezzi. I bundle scaricati sono
rimasti temporanei, fuori dal repository.

**Non è stato eseguito il caricamento finale in una lega Fantacalcio.it
autenticata.** La verifica del formato non sostituisce i controlli applicati
dalla lega di destinazione. Prima dell’uso reale, importare il CSV nella
schermata di associazione e controllare giocatori, prezzi e squadre.
