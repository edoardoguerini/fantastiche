# Catalogo calciatori e database: proposta da discutere

Ricerca documentale del 9 settembre 2026. Nessun provider selezionato o account creato; nessuna API autenticata testata. Questo documento non modifica le decisioni già approvate sull’asta.

## Fonti candidate

| Fonte | Evidenza documentale | Valutazione |
| --- | --- | --- |
| Fantacalcio.it | Pagina ufficiale delle quotazioni con ruoli Classic/Mantra, QI, QA, FVM e comando di download | Candidato per il listone di riferimento; formato effettivo del download e autorizzazione all’importazione/redistribuzione da verificare. Non è stata identificata in questa ricerca un’API pubblica documentata. |
| API-Football | Piano gratuito con 100 richieste/giorno e stagioni limitate; Pro a 19 USD/mese con 7.500 richieste/giorno; endpoint per giocatori, squadre e statistiche | Primo candidato per un test di arricchimento. Copertura effettiva Serie A 2026/27, completezza delle immagini e limiti del piano gratuito devono essere verificati su risposte reali. |
| Sportmonks | Profili giocatore con image_path; copertura Serie A dichiarata; Starter da 29 EUR/mese per 5 campionati | Alternativa da confrontare mediante un campione degli stessi giocatori. Condizioni per immagini, conservazione e utilizzo da verificare prima dell’integrazione. |

Fonti:

- https://www.fantacalcio.it/quotazioni-fantacalcio
- https://www.api-football.com/pricing
- https://www.api-football.com/terms
- https://www.api-football.com/documentation-v3
- https://www.sportmonks.com/football-api/plans-pricing/
- https://www.sportmonks.com/football-api/
- https://docs.sportmonks.com/v3/tutorials-and-guides/tutorials/teams-players-coaches-and-referees/players
- https://www.fantacalcio.it/regolamenti/sistema-mantra

Prezzi pubblicati, non preventivi; imposte, copertura e condizioni vanno ricontrollate al momento della scelta.

API-Football consente nei propri termini la creazione di applicazioni e giochi fantasy e vieta la rivendita diretta dei dati. Specifica separatamente di non detenere i diritti sulle immagini e sui loghi e che possono servire ulteriori autorizzazioni. La disponibilità di un URL immagine non viene quindi considerata una licenza completa. Un segnaposto è previsto per immagini assenti o non utilizzabili.

## Proposta di importazione

Usare un listone scelto dalla lega per disponibilità, ruoli fantacalcio e quotazioni. Arricchirlo con un provider sportivo per anagrafica e statistiche, ed eventualmente foto con condizioni d’uso verificate. Non convertire automaticamente le posizioni sportive in ruoli editoriali del fantacalcio.

Importazione nel backend: file o API → area di verifica → associazione delle identità → anteprima delle differenze → pubblicazione di una versione del listone.

Le chiavi API rimangono sul server. La sincronizzazione avviene fuori dal percorso dei rilanci: una chiamata d’asta legge il nostro database e non dipende dalla disponibilità del provider.

Associare prima gli identificativi esterni già noti. Per nuovi abbinamenti usare nome normalizzato, data di nascita e squadra come indizi; presentare le ambiguità alla revisione umana. Il nome non è una chiave univoca.

Una lega stagionale usa una versione esplicita del listone. Proposta: aggiornarla solo con un’operazione controllata tra sessioni, preservando i ruoli e le informazioni storiche delle aggiudicazioni. Un aggiornamento del provider non deve alterare automaticamente l’asta in corso.

## Schema logico iniziale

Nomi indicativi; non è ancora una migration né uno schema fisico definitivo.

| Entità | Responsabilità e relazioni principali |
| --- | --- |
| AspNetUsers (ASP.NET Core Identity) | Identità dell’utente, distinta dalla squadra fantacalcio; sostituisce l’entità logica User senza duplicare gli account. |
| League | Lega persistente nel tempo, con riferimento al logo su Blob Storage. |
| LeagueInvitation | Invito personale associato a utente/lega/email, hash del token casuale, scadenza, mittente e stato. |
| EmailMessage | Coda persistita per invio Mailgun tramite Scheduler; tentativi, esito e prossimo tentativo. |
| LeagueMember | Appartenenza AspNetUsers–League, stato Pending/Active e permessi, incluso organizzatore. Pending non concede accesso alla lega. |
| Season | Stagione sportiva. |
| LeagueSeason | League + Season, regole, budget iniziale e versione del listone adottata. |
| Team (tabella Teams) | Squadra della LeagueSeason e stato del budget. |
| TeamMember (tabella TeamMembers) | Collegamento AspNetUsers–Team per partecipare con una squadra; distinto dal permesso di organizzatore nella lega. |
| Club | Squadra di calcio reale, distinta dalla Team. |
| Player | Identità stabile del calciatore: nome, data di nascita e anagrafica disponibile. |
| PlayerRegistration | Player, Club, Season e intervallo di validità, per conservare i trasferimenti. |
| ExternalPlayerId | Provider + identificativo esterno → Player; vincolo univoco sulla coppia provider/ID. |
| PlayerMedia | Riferimento alla foto, fonte, condizioni d’uso verificate e data di aggiornamento. |
| ListVersion | Fonte, stagione, sistema di ruoli e momento di pubblicazione/importazione. |
| ListEntry | Player nella ListVersion, squadra alla pubblicazione, disponibilità e quotazioni. Unico per versione/giocatore. |
| ListEntryRole | Ruoli della voce di listone: consente più ruoli senza fissare ora la scelta Classic/Mantra. |
| PlayerSeasonStats | Player, Season, competizione, provider e statistiche selezionate; eventuale dettaglio per Club per distinguere trasferimenti. |
| ImportRun | Fonte, stato, errori e riepilogo delle modifiche di un’importazione. |
| AuctionSession | Sessione della LeagueSeason, stato e posizione corrente nel giro. |
| CallOrderEntry | Partecipante/squadra e posizione nella sessione. |
| PlayerAuction | Sessione, giocatore, chiamante, durata, incrementi, scadenza, stato e offerta corrente. |
| Bid | Offerta accettata: PlayerAuction, Team, User autore, totale e sequenza server. |
| CommandReceipt | Identificativo della richiesta e risultato persistito per gestire duplicati, anche quando rifiutati. |
| RosterEntry | Proprietà corrente del Player nella LeagueSeason, Team, prezzo e riferimento all’aggiudicazione. |
| BudgetMovement | Registro dei movimenti in crediti con causa e riferimento all’operazione. |

## Vincoli da progettare nel database

- Un solo proprietario attivo per giocatore nella stessa LeagueSeason; le altre leghe sono indipendenti.
- Una sola asta di giocatore aperta per sessione. Proposta iniziale: una sola sessione attiva per LeagueSeason, per evitare assegnazioni concorrenti tra sessioni.
- Ordine di chiamata senza partecipanti o posizioni duplicate nella sessione.
- Importi in crediti interi positivi; scadenze UTC, durata limitata ai sei valori concordati.
- Chiave univoca delle richieste entro il relativo ambito utente/sessione; riuso con payload diverso rifiutato.
- Aggiudicazione e movimento di budget univoci per PlayerAuction.
- Foreign key coerenti con la stessa LeagueSeason: una squadra non può offrire in una lega estranea.
- Aggiornamento atomico di stato dell’asta, offerta e scadenza; aggiudicazione atomica con rosa e budget.
- Versionamento dello stato e transazioni per la concorrenza; una colonna rowversion da sola non sostituisce la procedura transazionale.

## Decisioni confermate: identità e accesso ai dati

ASP.NET Core Identity gestisce gli account. Le sue tabelle includono `AspNetUsers`, `AspNetRoles`, `AspNetUserRoles` e le tabelle di supporto per claim, login esterni e token. `SuperAdmin` è il ruolo globale che può creare leghe nella prima versione. LeagueMember registra appartenenza alla lega e permesso di organizzatore; TeamMember abilita la partecipazione tramite una squadra. Non sono ruoli mutuamente esclusivi: lo stesso account può organizzare e giocare nella stessa lega, o organizzare senza giocare. Il chiamante è uno stato del turno.

Naming confermato: `Teams` per le squadre della lega, `TeamMembers` per i loro utenti, `Clubs` per le squadre reali. Il budget e i limiti di rosa appartengono alla LeagueSeason e sono configurabili: default 500 crediti, 3 P, 8 D, 8 C e 6 A (25 giocatori). Abbonamenti e pagamenti restano fuori dalla prima versione.

EF Core gestisce migrazioni, persistenza di Identity e configurazione ordinaria. `__EFMigrationsHistory` registra le migrazioni applicate. Dapper usa lo stesso schema per le query delle schermate e le operazioni critiche dell’asta: non introduce uno schema o un sistema di migrazioni parallelo.

Avvio, rilanci e aggiudicazioni passano da un unico percorso di scrittura con Dapper, SQL parametrizzato e transazioni esplicite. Validazione sullo stato corrente e aggiornamenti della stessa operazione condividono connessione e transazione. Le notifiche vengono pubblicate dopo il commit. Evitare salvataggi successivi di entità EF tracciate con uno stato precedente alle modifiche Dapper.

Per le letture: selezionare solo i campi necessari, paginare gli storici ed evitare query ripetute per ogni riga. Gli eventuali percorsi di sola lettura EF usano query senza tracking quando non necessario. Gli indici e le query vengono valutati con piani di esecuzione e carico realistico, misurando anche blocchi e consumo DTU.

## Appartenenza a lega e stagione

LeagueSeason collega League e Season. Team e AuctionSession vi fanno riferimento direttamente; PlayerAuction appartiene alla sessione e Bid all’asta del giocatore. Lega e stagione sono quindi sempre determinabili per i dati privati, senza duplicare necessariamente entrambe le colonne in ogni tabella.

Players e Clubs sono condivisi; il listone ha una stagione ma può essere adottato da più leghe. Ogni query privata, incluse quelle Dapper, applica i controlli di accesso alla lega. Eventuali riferimenti duplicati introdotti per filtraggio o vincoli devono essere mantenuti coerenti tramite chiavi esterne composte; la squadra offerente e la sessione devono appartenere alla stessa LeagueSeason.

## Decisioni ancora necessarie

1. Classic confermato dall’utente, con ruoli P/D/C/A; Mantra escluso dalla prima versione. Serie A resta il perimetro iniziale proposto.
2. Redazione/listone di riferimento e possibilità di caricare un listone personalizzato.
3. Dati da mostrare nella scheda: anagrafica, squadra, foto e quali statistiche; voto editoriale e fantamedia non equivalgono al rating generico di un provider sportivo.
4. Operatività delle squadre con più utenti: inviti, permessi e scelta di chi effettua la chiamata. Il modello TeamMembers è definito; budget e composizione predefiniti sono confermati sopra.
5. Dettagli di autenticazione, scadenza e reinvio: il flusso confermato predispone account senza password (se nuovo), membership Pending e invito per nome/email inseriti dall’organizzatore. Nel form l’invitato attiva l’account o accede con quello esistente e sceglie il nome squadra. Creazione e coda email sono atomiche; lo Scheduler invia tramite Mailgun. Vedi [guida](../../../be/docs/domains/invitations-email.md). La monetizzazione è fuori scope.
6. Comportamento con trasferimenti, svincoli e aggiornamenti del listone dopo acquisti già conclusi.

Prossimo test proposto: confrontare un campione con giocatori trasferiti, omonimi e giovani della stagione corrente; misurare abbinamenti, completezza foto, aggiornamento rose e richieste necessarie a una sincronizzazione. Nessuna integrazione viene considerata verificata senza questo test.

## Endpoint di esportazione segnalato dall’utente

Il 9 settembre 2026 l’utente ha proposto https://www.fantacalcio.it/api/v1/Excel/fantaasta/1 come possibile fonte aggiornata. Il tentativo tramite strumento web ha restituito HTTP 401 Unauthorized. Anche il collegamento «Scarica» della pagina ufficiale quotazioni, risolto in https://www.fantacalcio.it/api/v1/Excel/prices/21/1, ha restituito 401. Il tentativo locale tramite curl non ha raggiunto il servizio per un errore di risoluzione DNS nell’ambiente.

Non sono quindi stati verificati contenuto, formato effettivo, colonne, immagini, stagione o data di aggiornamento del file fantaasta. Non si presume il significato dei parametri numerici. Il 401 può dipendere dall’autenticazione o da restrizioni sulle richieste automatiche; questa verifica non ne identifica la causa.

Proposta: ispezionare un file scaricato dall’utente con il normale accesso al sito, quindi definire l’importatore sul formato osservato. Download manuale/importazione e sincronizzazione automatica sono capacità distinte; quest’ultima richiede un accesso documentato e consentito dal fornitore.

## Ispezione del CSV fornito dall’utente

File letto il 9 settembre 2026: `/Users/edoardoguerini/Desktop/Lista-FantaAsta-Fantacalcio.csv`. Il file originale non è stato modificato né copiato nel repository.

- 104.615 byte; SHA-256 `0f9ac0d13617fc6556b02fac05f99688458b8f9ff2d87b57a3cd39a2b5b81455`.
- UTF-8 con BOM, separatore virgola, nessuna riga di intestazione.
- 594 righe, tutte con 19 colonne; nessuna cella vuota o ID duplicato.
- 20 nomi di club distinti. Ruoli Classic: 74 P, 213 D, 203 C, 104 A.

| Colonna (da 1) | Contenuto osservato / interpretazione |
| --- | --- |
| 1 | Identificativo Fantacalcio del giocatore; da conservare come ID esterno. |
| 2 | Nome breve per il listone. |
| 3 | Nome completo. |
| 4 | Ruolo Classic P/D/C/A. |
| 5 | Ruoli Mantra, anche multipli separati da punto e virgola. |
| 6–9 | Quattro valori numerici, verosimilmente quotazioni Classic/Mantra iniziali/attuali: ordine esatto non ancora verificato. |
| 10 | Nome del club. |
| 11–12 | Due valori numerici, verosimilmente FVM Classic/Mantra: mapping da confermare. |
| 13 | Piede. |
| 14 | Nazionalità, anche multiple separate da punto e virgola. |
| 15 | Data di nascita nel formato giorno/mese/anno con componente oraria. |
| 16 | URL PNG della card/campioncino; tutti i record contengono un valore, disponibilità e natura fotografica non verificate. |
| 17 | Flag 0/1 (532 zeri, 62 uni); significato non verificato, non trattarlo come disponibilità o stato infortunio. |
| 18–19 | Valori coerenti con media voto e fantamedia nel campione Carnesecchi; semantica da confermare prima dell’import definitivo. |

La scheda ufficiale di Carnesecchi per il 2026/27 mostra MV 6,83 e FM 6,17, corrispondenti alle ultime due colonne del suo record. Tuttavia i valori FVM visualizzati nella scheda consultata (55) differiscono dai due valori 57 del CSV. Questo confronto non certifica la freschezza dell’intero listone: possono incidere aggiornamenti e cache della fonte.

Fonte del confronto: https://www.fantacalcio.it/squadre/giocatore/Carnesecchi/4431

Il CSV non contiene un campo esplicito stagione né una data di generazione. La data locale del file è quella del download e il parametro `v=817` degli URL immagini non è interpretato come data di aggiornamento del listone.

Note di normalizzazione: sono presenti spazi finali in alcuni nomi e apostrofi raddoppiati in testi come `Costa d''Avorio`. Usare un parser CSV reale; mantenere distinti delimitatore delle colonne e separatori interni multivalore. Conservare l’originale nell’area di importazione, secondo le condizioni d’uso da verificare.

Conclusione tecnica: questo formato contiene già il nucleo del catalogo Classic e riferimenti alle immagini. Un provider sportivo aggiuntivo può essere rimandato fino a quando servano dati non presenti. Rimangono da verificare mapping delle colonne senza intestazione, freschezza, accesso automatico e utilizzo commerciale del listone e delle card.

## Decisione: immagini in Azure Blob Storage

L’utente ha scelto di scaricare le immagini referenziate nel CSV e caricarle in Azure Blob Storage. Con la successiva indicazione «Useremo poi i bicep. Intanto segnalo», l’esecuzione è rimandata: in questa fase si registra la decisione, senza download massivo, caricamento o creazione di risorse.

- L’infrastruttura Azure verrà definita tramite Bicep, inclusi Storage Account, container e autorizzazioni necessarie.
- L’importatore collegherà ogni immagine al giocatore attraverso l’ID esterno Fantacalcio, conservando URL sorgente e riferimento al blob in PlayerMedia.
- Il trasferimento delle immagini sarà un’operazione applicativa separata dal provisioning Bicep.
- Download non riusciti o immagini mancanti non impediranno l’importazione del giocatore; l’interfaccia userà un segnaposto.
- Naming, accesso ai blob, identità dell’importatore e aggiornamento delle immagini saranno definiti insieme alla configurazione degli ambienti.
- La scelta tecnica non risolve la verifica delle condizioni di riuso commerciale di dati e immagini, già annotata sopra.
