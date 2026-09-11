# Storage locale

Azurite è incluso in `be/docker-compose.yml`, quindi viene avviato da `just be up` o `just up-all`, come nel riferimento ACKSD. Fantastiche ha account, volume e porta propri: servizio `fantastiche-azurite-1`, volume `fantastiche_azurite-data`, porta loopback `10010`. `--location /data` fa persistere i blob nel volume anche quando il container viene ricreato. La porta è configurabile con `AZURITE_BLOB_PORT` in `be/.env`.

`just be env` genera una chiave casuale `AZURITE_ACCOUNT_KEY` se assente, senza stamparla. Lo storage resta separato dai servizi ACKSD. Non usare `docker compose down -v` per un normale riavvio: elimina anche i volumi dati.

## Container e indirizzi

| Container | Contenuto | URL browser |
| --- | --- | --- |
| `player-photos` | Card PNG originali del CSV, percorso `fantacalcio/<externalId>.png` | `http://localhost:10010/fantastiche/player-photos` |
| `club-logos` | Stemmi PNG verificati, percorso `clubs/<nome>.png` | `http://localhost:10010/fantastiche/club-logos` |

I due container permettono la lettura dei singoli blob; la lista non è anonima. La porta è accessibile soltanto dalla macchina locale. Sono container dedicati alle immagini del catalogo, non a documenti privati o credenziali. CORS non serve per i normali elementi img; un eventuale futuro editor canvas richiederà una configurazione dedicata.

Le API restituiscono URL browser attraverso `Storage__PlayerPhotos__PublicBaseUrl` e `Storage__ClubLogos__PublicBaseUrl`. Il backend in Docker usa i valori espliciti del compose; i comandi host usano i default di `scripts/dev.py`. Il DNS Docker `azurite:10000` è utilizzabile tra container, ma non va restituito al browser.

## Importazione locale ripetibile

Requisiti: Docker, Python 3.13 e SDK Azure Blob nel venv dedicato. Dal root:

```sh
just be env
just be up
just be storage-tools
just be photos-import /percorso/assoluto/Lista-FantaAsta-Fantacalcio.csv
just be photos-register
just be club-logos-import /percorso/assoluto/sources.json
just be club-logos-register
```

Il primo import legge colonna 16 del CSV, valida ID e URL HTTPS di `content.fantacalcio.it`, limita download a 4 MiB e verifica intestazione PNG e dimensioni. Non segue redirect e non modifica il CSV. Concorrenza limitata a 4 download, retry limitati per rete/5xx/429. File originali, hash, URL sorgente e data sono salvati sotto `be/.local/player-photos`, escluso da Git. Il manifest è registrato nel database mediante CLI; una seconda esecuzione conserva file e blob già corrispondenti. `--refresh` sullo script forza la rilettura delle sorgenti.

Per gli stemmi, `sources.json` contiene `{source:"FantacalcioCsv",items:[{clubName,sourceUrl}]}`. Il file locale predisposto è `be/.local/club-logos/sources.json`, ricavato dagli img `itemprop=logo` della pagina ufficiale con title esattamente uguale ai club del CSV. Il comando verifica che gli URL siano PNG sotto `/web/img/team/ico/`. Metadati e originali restano in `be/.local/club-logos`. Non inferire URL da nomi simili: alcuni stemmi hanno nomi/versioni specifici.

Gli importatori .NET accettano anche un percorso assoluto al manifest. I comandi just lo risolvono automaticamente: `dotnet run` cambia cartella nel progetto host, quindi un generico `.local/...` relativo non punta all’area locale backend.

## Verifica del 10 settembre 2026

Scaricate 594 card e 20 stemmi, con zero errori. Card: 64.146.271 byte; stemmi: 190.678 byte. Alcuni URL ufficiali restituiscono la stessa card generica: 190 giocatori condividono 20 segnaposti della fonte; questi file sono conservati fedelmente, senza inventare ritratti. Il database contiene 594 Players, 20 Clubs e 614 metadati media. La demo usa il listone reale del CSV, gli otto account esistenti e una nuova sessione; i 280 giocatori inventati sono stati rimossi su richiesta, dopo backup locale mirato.

Azurite è stato ricreato conservando il volume; un nuovo import ha verificato tutti i 614 blob e aggiornato 0 file. Questa configurazione riguarda lo sviluppo locale; il provisioning Azure tramite Bicep resta un’attività successiva.

Riferimenti: [Azurite Microsoft](https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azurite), [SDK Blob Python](https://learn.microsoft.com/en-us/azure/storage/blobs/storage-blob-container-create-python), [stemmi dalla pagina ufficiale](https://www.fantacalcio.it/serie-a/squadre).


## Logo delle leghe

Il container pubblico a livello blob `league-logos` conserva i loghi associati tramite `Leagues.LogoBlobName`. `Storage__LeagueLogos__PublicBaseUrl` configura l’URL pubblico (locale: `http://localhost:10010/fantastiche/league-logos`); l’elenco e il dettaglio delle leghe restituiscono `logoUrl`, nullo se logo o configurazione mancano. La card frontend usa le iniziali come alternativa se l’immagine non è disponibile. La variante PNG trasparente `fe/public/brand/fantastiche-logo-transparent.png`, scontornata dal logo Fantastiche originale su richiesta dell’utente, è stata caricata e associata alla lega demo «Gli ultimi del bar». Il caricamento è una configurazione locale; non è ancora presente un form o endpoint per modificare il logo.
