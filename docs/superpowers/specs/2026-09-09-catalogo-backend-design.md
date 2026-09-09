# Catalogo backend: importazione e versioni Classic

Incremento sul branch `feature/be-bootstrap`, autorizzato dal proseguimento autonomo del backend. Struttura ACKSD, .NET 10, EF Core per schema/scritture, Dapper per consultazione. Nessuna nuova dipendenza NuGet.

## Scelta

Una versione del listone è uno snapshot immutabile. Il SuperAdmin importa il CSV in bozza, consulta le righe con filtri e paginazione e pubblica esplicitamente. Gli utenti autenticati vedono solo versioni pubblicate; le bozze sono visibili al SuperAdmin. Importare non pubblica e non cambia le leghe.

Alternative considerate: aggiornare direttamente un unico catalogo perderebbe lo storico; introdurre subito provider multipli, matching manuale e statistiche allargherebbe il perimetro oltre il formato verificato. Si conserva invece l’identità tramite coppia fonte/ID esterno e si memorizzano i dati del listone nella versione. Nessuna associazione per solo nome, nessuna modifica dei vecchi snapshot.

## CSV

Formato Fantacalcio osservato: UTF-8, BOM facoltativo, virgola, 19 colonne senza intestazione. Parser CSV reale con virgolette, escape e campi multiriga; limite 1.048.576 caratteri e 5.000 righe. Importazione atomica: una riga errata rifiuta tutto. Errori identificano riga/campo, senza riportare il file.

Mapping: ID esterno numerico positivo, normalizzato e massimo 32 caratteri (1), nome breve (2), completo (3), Classic P/D/C/A (4), club alla pubblicazione (10), piede (13), nazionalità (14), data di nascita `dd/MM/yyyy HH:mm:ss` (15). Nessuna interpretazione di quotazioni, FVM, statistiche e flag non verificati. Nessun download immagini o chiamata al provider. Test con righe sintetiche, CSV originale escluso da Git.

## Modello e invarianti

Players conserva identità e ID esterno univoco per fonte. Clubs conserva i nomi normalizzati della fonte (nessun matching tra provider). ListVersions registra fonte, stagione dichiarata dall’importatore, hash SHA-256 del contenuto, numero righe, autore e date UTC. ListEntries conserva nome, anagrafica, ruolo e club dello snapshot, con PK versione/giocatore e FK restrittive. I dati delle versioni precedenti non sono aggiornati.

Importazioni identiche della stessa fonte/stagione/hash restituiscono la versione esistente, anche con richieste concorrenti. Scritture serializzate da un applock Catalog separato dall’onboarding. Pubblicazione ripetibile senza cambiare la data iniziale. Nessun endpoint di modifica o cancellazione delle versioni.

LeagueSeasons riceve ListVersionId nullable: organizzatore attivo o SuperAdmin può scegliere una versione pubblicata con lo stesso nome stagione, confrontato con la stessa collation SQL usata dalla ricerca e dal vincolo delle versioni. Prima assegnazione e replay della stessa versione consentiti; sostituzione rifiutata finché non esiste un flusso controllato tra sessioni d’asta. Nessuna modifica automatica su nuove pubblicazioni. Controlli di lega negli handler e nelle query Dapper.

## API

Sotto `/api/Catalog`: POST `/Imports` con JSON `{seasonName,csv}`, POST `/Versions/{id}/Publish`, GET `/Versions`, GET `/Versions/{id}/Entries`. Query paginata (1–100 righe, pagina massima 10000), filtri stagione o nome/ruolo/club a seconda dell’endpoint. Autenticazione su ogni route, policy SuperAdmin sulle scritture, antiforgery come nel bootstrap.

Sotto `/api/Leagues/{leagueId}/Seasons/{leagueSeasonId}/Catalog`: GET versione selezionata (null se assente), PUT `{listVersionId}` per prima scelta. Il nome stagione usa il valore già presente in LeagueSeason (es. `2026/27`), senza introdurre un secondo concetto sportivo non concordato.

## Verifica

Test parser (virgolette/BOM, righe invalide, duplicati, limiti); test SQL reale (migrazione, replay e concorrenza, snapshot dopo trasferimenti, visibilità bozze, filtri/pagine, permessi e scope lega, vincolo stagione, selezione immutabile); test HTTP (autenticazione, antiforgery, validazione, importazione/pubblicazione/lettura/adozione, OpenAPI). Suite completa, format, migrazione senza cambi pendenti, avvio Docker e Scalar su 6060.
