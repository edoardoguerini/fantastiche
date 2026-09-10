# Catalogo e listoni Classic

Il catalogo è condiviso tra le leghe. Ogni listone è una versione con stagione dichiarata, fonte e hash del CSV; una lega stagionale sceglie esplicitamente la versione da utilizzare. Le successive importazioni non cambiano il listone della lega.

## Flusso

1. Il SuperAdmin invia `POST /api/Catalog/Imports` con JSON `{ "seasonName": "2026/27", "csv": "contenuto del file" }`.
2. La risposta contiene l’ID della versione in stato `Draft`, il numero di righe e l’hash. Il file viene validato integralmente prima della scrittura; un errore annulla l’importazione. Un file identico per la stessa stagione restituisce la versione esistente e completa soltanto i nuovi campi di mercato ancora nulli, anche se la versione è pubblicata.
3. `GET /api/Catalog/Versions/{id}/Entries` consente di controllare le righe della bozza. Sono disponibili `search`, `role`, `club`, `page` e `pageSize`; ruolo Classic P/D/C/A, massimo 100 righe per pagina.
4. `POST /api/Catalog/Versions/{id}/Publish` rende la versione consultabile agli utenti autenticati. Ripetere la pubblicazione conserva la data originale.
5. L’organizzatore attivo o il SuperAdmin sceglie il listone con `PUT /api/Leagues/{leagueId}/Seasons/{leagueSeasonId}/Catalog`, corpo `{ "listVersionId": "UUID" }`. La versione deve essere pubblicata e avere la stessa stagione della lega; il confronto segue la collation SQL anche durante la selezione, come durante l’importazione.

`GET /api/Catalog/Versions?seasonName=2026%2F27&page=1&pageSize=50` elenca le versioni: tutti gli utenti autenticati vedono quelle pubblicate, solo il SuperAdmin vede anche le bozze. `GET /api/Leagues/{leagueId}/Seasons/{leagueSeasonId}/Catalog` restituisce la versione scelta oppure `data: null`. Questo dettaglio richiede appartenenza attiva alla lega o SuperAdmin.

I privilegi SuperAdmin sono verificati nel database anche con cookie già emesso: una claim rimasta dopo la revoca non permette importazione, pubblicazione, consultazione delle bozze né bypass dei permessi della lega. Restano validi gli eventuali permessi ordinari di membro/organizzatore attivo e la lettura dei listoni pubblicati.

La prima scelta è consentita; ripetere lo stesso ID è idempotente. La sostituzione con un’altra versione restituisce 409: richiede il futuro flusso controllato tra sessioni d’asta. Il reimport dello stesso hash può completare i campi di mercato mancanti; non modifica valori già presenti, anagrafica, identificativi o data di pubblicazione e non elimina gli snapshot. La consultazione paginata della bozza è disponibile; un confronto automatico delle differenze tra versioni non è ancora implementato.

## Formato importato

CSV Fantacalcio osservato: 19 colonne, separatore virgola, UTF-8 con BOM facoltativo, senza intestazione. Virgolette, escape e campi multiriga sono gestiti dal parser CSV della libreria standard. Limiti applicativi: 1.048.576 caratteri e 5.000 righe.

| Colonna | Campo |
| --- | --- |
| 1 | ID esterno del giocatore |
| 2–3 | Nome breve e completo |
| 4 | Ruolo Classic |
| 5 | Ruolo Mantra |
| 6–7 | Quotazione Classic attuale e iniziale |
| 8–9 | Quotazione Mantra attuale e iniziale |
| 10 | Club reale nello snapshot |
| 11–12 | FVM Classic e Mantra |
| 13–14 | Piede e nazionalità |
| 15 | Data di nascita, formato `dd/MM/yyyy HH:mm:ss` |

| 17 | Ceduto: 0 = no, 1 = sì |

Quotazioni e FVM sono interi non negativi, zero compreso. Il mapping è stato confrontato con il file Excel della stessa stagione. Le colonne 18–19 non vengono interpretate: media voto e fantamedia non sono esposte senza un mapping verificato. Il catalogo globale conserva e segnala i ceduti; il catalogo d’asta disponibile li esclude e il server rifiuta nuove chiamate con `auction.player_transferred`. Gli acquisti preesistenti restano nello storico. Le righe del listone costituiscono il suo perimetro: l’assenza in un nuovo CSV non cancella il giocatore dagli snapshot precedenti.

Il catalogo associa le identità attraverso fonte/ID esterno, mai attraverso il nome. Un trasferimento cambia il club nello snapshot nuovo, preservando PlayerId e la versione precedente. Clubs indica i club reali, Teams continua a indicare le squadre della lega. Il modello è circoscritto alla fonte CSV attuale: non introduce matching automatico tra fornitori.

Il contenuto originale non viene conservato nel database: viene calcolato l’hash e vengono salvati i campi mappati. Conservare il file originale fuori Git se servirà rielaborarlo con un mapping esteso. La stagione è fornita dall’importatore perché il CSV non la contiene.

## Recupero di importazioni precedenti

Applicare la migrazione `AddCatalogMarketData`, poi reimportare il CSV originale con la stessa stagione: il controllo di hash recupera la versione esistente. Le otto proprietà aggiunte sono nullable per distinguere dati non acquisiti da valori zero. Il completamento avviene in transazione, controllando numero e identità delle righe, senza cambiare sessioni, acquisti o budget. Un file diverso crea una nuova bozza e non modifica automaticamente il listone della lega.

Dopo il recupero ricaricare le sale e le anteprime catalogo già aperte: questa operazione amministrativa non incrementa la versione della sessione e non emette una notifica realtime di catalogo. Il controllo server sui ceduti è efficace subito, anche per un client con dati precedenti in cache.

## Uso locale

Avviare con `just up-all`, applicare nuove migrazioni con `just be migrate`. Scalar è su [localhost:6060/scalar](http://localhost:6060/scalar). Il bootstrap SuperAdmin è esplicito: seguire il [setup](../getting-started/development-setup.md).

Le API usano i cookie Identity. Recuperare `/api/Auth/Antiforgery` e inviare il valore `data.token` come header `X-XSRF-TOKEN` su POST/PUT; dopo il login recuperare un token aggiornato. Anche importazione e pubblicazione richiedono questa protezione.

Per preparare un payload dal proprio file, senza inserirlo nel repository:

```sh
mkdir -p be/.local
python3 - <<'PYTHON'
import json
from pathlib import Path
source = Path.home() / "Desktop/Lista-FantaAsta-Fantacalcio.csv"
payload = {"seasonName": "2026/27", "csv": source.read_text(encoding="utf-8-sig")}
Path("be/.local/catalog-import.json").write_text(json.dumps(payload), encoding="utf-8")
PYTHON
```

Inviare quel JSON all’endpoint di importazione tramite un client autenticato. Questa preparazione non importa né pubblica dati. I test automatici usano righe sintetiche e database temporanei dedicati, senza download e senza modificare il CSV originale.

## Persistenza e verifiche

EF Core definisce Players, Clubs, ListVersions e ListEntries e il riferimento nullable in LeagueSeasons. Chiavi univoche impediscono doppie identità e doppie importazioni; PK composta versione/giocatore e FK restrittive preservano gli snapshot.

Le scritture del catalogo usano una transazione e un applock dedicato, distinto dall’onboarding. L’invio HTTP arriva dopo il commit. Le letture Dapper applicano parametri, visibilità e scope della lega esplicitamente; la paginazione ha un ordinamento stabile.

Test pertinenti: `FantacalcioCsvParserTests`, `CatalogTests`, scenari catalogo in `HttpFlowTests`. Eseguire `just be test`, `just be migrate-check` e `just be lint`. Gli esiti della sessione sono nel [documento di passaggio](../../../docs/workflow/backend-handoff.md).

## Card dei calciatori e loghi dei club

`PlayerMedia` conserva i metadati delle card PNG con chiave naturale `Source` + `ExternalId`. `ClubMedia` conserva quelli dei loghi PNG con chiave `Source` + `NormalizedClubName`; la normalizzazione è la stessa del catalogo (`Trim` e maiuscolo invariant). Nessuna FK richiede che giocatori o club siano già importati: le immagini possono precedere il listone. Le card sono immagini illustrate, non fotografie ritratto.

Importazione solo mediante CLI locale, usando la normale configurazione SQL. Dal root del repository, dopo le migrazioni:

```sh
just be photos-register
just be club-logos-register
```

Le ricette passano percorsi assoluti dei manifest a `--import-player-media` e `--import-club-media`. Anche invocando direttamente la CLI, usare un percorso assoluto: `dotnet run` imposta come directory corrente il progetto Application.

Manifest JSON: `{source: "FantacalcioCsv", items: [...]}`. Ogni card contiene `externalId`, `sourceUrl`, `blobName`, `contentType`, `contentLength`, `sha256`, `downloadedAt`; per i loghi `clubName` sostituisce `externalId`. Esempio di elemento card:

```json
{
  "externalId": "4431",
  "sourceUrl": "https://content.fantacalcio.it/web/campioncini/21/card/4431.png?v=817",
  "blobName": "fantacalcio/4431.png",
  "contentType": "image/png",
  "contentLength": 123,
  "sha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
  "downloadedAt": "2026-09-10T00:00:00Z"
}
```

Il manifest è letto e validato prima della transazione SQL. Sono ammessi metadati PNG da URL HTTPS di `content.fantacalcio.it`, nomi blob relativi senza traversal, lunghezza 1 byte–10 MiB e hash SHA-256 esadecimale. File oltre 10 MiB, envelope errato o JSON malformato interrompono il comando con exit code 1. Elementi invalidi e identità duplicate sono scartati e contati; per i duplicati rimane il primo valido. L’upsert con transazione serializable e lock dedicato è ripetibile e non elimina media assenti. Il riepilogo distingue inseriti, aggiornati, invariati e scartati. La CLI verifica i metadati; download, verifica del contenuto PNG e upload spettano all’importatore locale. Nessun endpoint pubblico di upload.

Gli URL per il browser hanno configurazione separata, vuota per default:

| Chiave | Valore locale |
| --- | --- |
| `Storage:PlayerPhotos:PublicBaseUrl` | `http://localhost:10010/fantastiche/player-photos` |
| `Storage:ClubLogos:PublicBaseUrl` | `http://localhost:10010/fantastiche/club-logos` |

Variabili d’ambiente: `Storage__PlayerPhotos__PublicBaseUrl` e `Storage__ClubLogos__PublicBaseUrl`. I base URL non contengono credenziali, query o frammenti; devono essere raggiungibili dal browser, quindi non usare il DNS interno Docker.

Catalogo globale, catalogo d’asta, `currentAuction` e rosa includono `photoUrl` e `clubLogoUrl`, entrambi nullable. L’URL è base pubblico + `/` + nome blob e richiede configurazione e metadati presenti. Le card sono associate tramite Players.Source/ExternalId, mai tramite nome del calciatore. Il logo passa dal ClubId di ListEntries e da Clubs.Source/NormalizedName. La rosa segue il listone originale dell’acquisto e conserva il club di quello snapshot anche dopo un trasferimento.

Test: parser dei due manifest, URL opzionali/separati, import prima del catalogo, replay concorrente, aggiornamento senza cancellazione degli assenti, corrispondenza fonte/identità, logo dello snapshot originale, fallback null e contratti HTTP. Sincronizzazione periodica del fornitore e deploy Blob Azure restano incrementi successivi.
