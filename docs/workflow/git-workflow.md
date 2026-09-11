# Workflow Git

## Stato attuale

Il backend è stato integrato in `master` tramite PR #1. Il frontend, la sala d’asta, gli inviti, la gestione catalogo e lo storage locale sono sviluppati su `feature/fe-bootstrap`. Dopo le verifiche l’utente ha autorizzato commit, push e apertura della PR verso `master`. Il merge resta un passaggio separato.

## Incremento backend precedente

Il lavoro backend prosegue sul branch `feature/be-bootstrap`, mantenuto su richiesta dell’utente. Il remote `origin` punta a `https://github.com/edoardoguerini/fantastiche.git`. Dopo la verifica del backend, l’utente ha autorizzato commit e push di questo incremento sullo stesso branch. Questa attività non configura Boards, branch protetti o pipeline e non esegue merge. Non creare work item nel progetto ACKS.

## Modello di riferimento da attivare

Come ACKS: branch di produzione e `develop`, feature/fix verso develop, hotfix da produzione con rientro in entrambi. Nome definitivo del branch produzione e progetto DevOps saranno fissati con il collegamento del repository.

Quando Boards sarà collegato, usare work item → branch `feature/<id>-<descrizione>` o `fix/<id>-<descrizione>` → PR associata. PR di feature in squash; merge tra branch permanenti con merge commit. Tag e versionamento automatico richiedono pipeline dedicate.

## Rilascio

Oggi `master` è l'unico branch di rilascio: ogni push che tocca `be/`, `fe/`, `infra/`, `.github/workflows/` o `GitVersion.yml` avvia `.github/workflows/build-deploy.yml`, che builda le immagini, esegue la migrazione con la nuova immagine e poi applica i Bicep delle app (al primo deploy la migrazione segue il deploy, perché il job non esiste ancora), e crea il tag `vX.Y.Z` calcolato da GitVersion sui conventional commits. Con lo squash merge conta il titolo della PR. Il foundation Bicep non passa dalla pipeline: vedi [infra](../../infra/README.md).

## Convenzioni attuali

Conventional commits: `docs: documenta le convenzioni`, `feat(be): aggiunge il rilancio`, `fix(fe): ripristina lo stato alla riconnessione`. Codice in inglese, descrizione in italiano. Nessuna firma AI.

Esaminare il diff e preservare le modifiche dell’utente. Non committare dati importati, immagini, credenziali o artefatti generati. Push, merge e deployment seguono la richiesta effettiva dell’utente e la destinazione configurata, senza assumere quella del riferimento.
