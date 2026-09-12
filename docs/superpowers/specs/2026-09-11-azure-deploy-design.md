# Deploy Azure: Bicep e pipeline GitHub Actions

Data: 11 settembre 2026. Stato: approvato in conversazione, da tradurre in piano.

## Obiettivo

Portare Fantastiche su Azure Container Apps replicando la struttura di ACKSD (Bicep foundation + Bicep per stack, pipeline a stage, GitVersion, comandi `just`), adattata alle differenze di Fantastiche: repository su GitHub, frontend SPA senza server Node, backend senza SDK Blob, migrazioni esplicite, DataProtection condivisa tra API e Scheduler. Progetto interno: un solo ambiente, nessun dominio custom, nessun abbonamento.

Nessun identificativo, risorsa o credenziale ACKSD viene riusato. Questo documento non esegue alcun deploy.

## Decisioni prese con l'utente

| Tema | Decisione |
| --- | --- |
| Piattaforma CI/CD | GitHub Actions, login Azure con OIDC federato |
| Subscription | `MPN - Mahiz`, id `818db21a-4d0b-430a-a42a-a00715f0345f`, tenant `58b8838a-bd0a-4297-94a7-43cbae09891e` |
| Regione | West Europe |
| Ambienti | Uno solo, `prod`, rilasciato da `master` |
| Dominio | Nessun custom domain: URL `*.azurecontainerapps.io` |
| DataProtection | Cambio codice minimo: chiavi su Blob, protezione con chiave Key Vault, Managed Identity |
| Importazione immagini | A cura dell'operatore con `az storage blob upload-batch --auth-mode login`, fuori dalla pipeline |

## 1. Risorse Azure e naming

Prefisso `fantastiche`, suffisso ambiente `prod`. Tag comuni `project=fantastiche`, `environment=<env>`, `managedBy=bicep`.

| Risorsa | Nome | Resource group | Note |
| --- | --- | --- | --- |
| Container Registry | `fantasticheacr` | `rg-fantastiche-shared` | SKU Basic, admin user disabilitato |
| Managed Identity app | `id-fantastiche-prod` | `rg-fantastiche-prod` | condivisa da API, Scheduler, FE e job |
| Managed Identity CI | `id-fantastiche-ci` | `rg-fantastiche-shared` | federated credential verso GitHub |
| Log Analytics | `log-fantastiche-prod` | `rg-fantastiche-prod` | retention 30 giorni |
| Container Apps Environment | `cae-fantastiche-prod` | `rg-fantastiche-prod` | Consumption |
| Key Vault | `kv-fantastiche-prod` | `rg-fantastiche-prod` | RBAC, soft delete 7 giorni |
| SQL Server + DB | `sql-fantastiche-prod` / `fantastiche` | `rg-fantastiche-prod` | Basic 5 DTU 2 GB, firewall "Azure services", TLS 1.2 |
| Storage | `stfantasticheprod` | `rg-fantastiche-prod` | Standard LRS, StorageV2, `allowSharedKeyAccess=false` |
| Container App API | `fantastiche-api-prod` | `rg-fantastiche-prod` | ingress interno, porta 8080, min 1 max 1 |
| Container App Scheduler | `fantastiche-scheduler-prod` | `rg-fantastiche-prod` | nessun ingress, min 1 max 1 |
| Container App FE | `fantastiche-fe-prod` | `rg-fantastiche-prod` | ingress esterno, porta 8080, min 1 max 2 |
| Container Apps Job | `fantastiche-migrate-prod` | `rg-fantastiche-prod` | trigger manuale, immagine API |

Storage: tre container sullo stesso account.

- `player-photos` e `club-logos`: `publicAccess=Blob` (lettura anonima del singolo blob, elenco negato). Percorsi `fantacalcio/<externalId>.png` e `clubs/<nome>.png`, gli stessi dello storage locale.
- `dataprotection`: `publicAccess=None`, contiene l'anello di chiavi ASP.NET Core cifrato con la chiave Key Vault.
- `allowBlobPublicAccess=true` a livello di account è necessario per i due container pubblici; il container delle chiavi resta privato per policy e le chiavi sono comunque cifrate a riposo con Key Vault.

Ruoli assegnati dal foundation:

| Principal | Ruolo | Scope |
| --- | --- | --- |
| `id-fantastiche-prod` | AcrPull | ACR |
| `id-fantastiche-prod` | Key Vault Secrets User | Key Vault |
| `id-fantastiche-prod` | Key Vault Crypto User | Key Vault |
| `id-fantastiche-prod` | Storage Blob Data Contributor | storage |
| operatore (`operatorObjectId`) | Key Vault Secrets Officer | Key Vault |
| operatore | Storage Blob Data Contributor | storage |
| `id-fantastiche-ci` | Contributor | `rg-fantastiche-prod` |
| `id-fantastiche-ci` | Contributor | ACR (serve `scheduleRun` per `az acr build`) |

Taglie: ogni container 0.25 vCPU e 0.5 GiB, come ACKSD. API `maxReplicas=1` perché il motore d'asta usa SignalR in memoria e la migrazione deve girare una volta sola.

## 2. Topologia di rete

FE e API su origin diverse romperebbero il cookie `Fantastiche.Auth` con `SameSite=Lax`, perché `azurecontainerapps.io` è un suffisso pubblico e le due app risulterebbero siti diversi. Quindi:

- Solo il FE ha ingress esterno. È l'unico URL pubblico.
- L'API ha ingress interno, solo HTTPS: raggiungibile solo dentro l'environment all'FQDN `fantastiche-api-prod.internal.<defaultDomain>`. nginx vi si collega in TLS con SNI; in HTTP l'ingress riscriverebbe `X-Forwarded-Proto` a `http` e l'antiforgery rifiuterebbe i POST.
- nginx nel container FE inoltra `/api/` e `/hubs/` all'API, con header di upgrade WebSocket per SignalR e `X-Forwarded-Proto=https`. Tutto il resto serve gli asset statici con cache lunga e ricade su `_shell.html`.
- Il FE è buildato con `VITE_API_BASE_URL` vuoto: stessa origin, come già previsto dalla guida frontend.
- `Cors__AllowedOrigins__0` e `Invitations__PublicBaseUrl` valgono l'URL pubblico del FE: il primo serve alla verifica dell'Origin sul WebSocket, il secondo ai link negli inviti.
- Lo Scheduler non ha ingress.

Header forwarded: `ReverseProxy__KnownProxies` accetta solo IP puntuali, mentre nginx e l'ingress hanno IP dinamici. nginx normalizza `X-Forwarded-For` con il modulo `realip`: prende l'ultima voce (aggiunta dall'ingress fidato) come IP client e riscrive l'header verso l'API con quella sola voce, scartando ciò che il client ha inviato. Il backend espone `ReverseProxy__TrustAllProxies` (svuota `KnownProxies` e `KnownIPNetworks`, lecito perché l'ingress dell'API è interno) e `ReverseProxy__ForwardLimit` (voci da risalire da destra; 2 in produzione, così si arriva al client con o senza la voce aggiunta dall'ingress dell'API), impostati da `apps-be.bicep` sull'API. Così il rate limiter del login vede l'IP reale del client. In locale i default (loopback, un hop) non cambiano.

## 3. Modifiche al codice applicativo

### 3.1 Frontend: `fe/Dockerfile` e configurazione nginx

- Stage `builder`: `node:24-alpine`, corepack con la versione pnpm del `package.json`, `pnpm install --frozen-lockfile`, build-arg `VITE_API_BASE_URL` (default vuoto) e `VITE_APP_VERSION`, `pnpm build`. Il prerender della shell avvia un server locale: l'immagine deve consentirlo (nessuna sandbox nel builder).
- Stage `runtime`: `nginxinc/nginx-unprivileged:alpine`, porta 8080, copia di `dist/client` in `/usr/share/nginx/html` e di un template `fe/nginx/default.conf.template` reso a runtime dall'entrypoint ufficiale con la variabile `API_UPSTREAM` (es. `http://fantastiche-api-prod.internal.<defaultDomain>`).
- Regole nginx: `/assets/` e file con hash `Cache-Control: public, max-age=31536000, immutable`; `_shell.html` e `/` `no-cache`; `try_files $uri /_shell.html`; `location /api/` e `location /hubs/` con `proxy_pass $API_UPSTREAM`, `proxy_http_version 1.1`, header `Upgrade`/`Connection`, `proxy_read_timeout` lungo per le connessioni SignalR, `client_max_body_size` adeguato al caricamento CSV del listone.
- `fe/.dockerignore` esclude `node_modules`, `dist`, `test-results`, `.env*`.
- `fe/justfile`: comando `build-image` per costruire e provare l'immagine in locale.

### 3.2 Backend: DataProtection su Azure

In `be/src/Fantastiche.Infrastructure/DependencyInjection.cs`:

- Se `DataProtection:BlobUri` è configurato: `PersistKeysToAzureBlobStorage(new Uri(blobUri), new DefaultAzureCredential())` e `ProtectKeysWithAzureKeyVault(new Uri(keyVaultKeyId), new DefaultAzureCredential())`, con `DataProtection:KeyVaultKeyId` obbligatorio in questa modalità.
- Altrimenti il comportamento attuale resta invariato, compreso l'errore fuori Development quando mancano `KeyPath` e `CertificatePath`.
- Pacchetti `Azure.Extensions.AspNetCore.DataProtection.Blobs`, `Azure.Extensions.AspNetCore.DataProtection.Keys` e `Azure.Identity` in `Directory.Packages.props` e nel csproj di Infrastructure, versioni fissate.
- `AZURE_CLIENT_ID` in ambiente indica a `DefaultAzureCredential` quale identità user-assigned usare; senza, l'endpoint Managed Identity di Container Apps risponde 400 (gotcha ACKSD).
- Test unitario: la modalità Azure si attiva solo con `BlobUri` presente e richiede `KeyVaultKeyId`; la modalità file resta quella di default. Nessuna chiamata reale ad Azure nei test.

### 3.3 Versionamento

- `be/.config/dotnet-tools.json` non cambia: nella pipeline GitVersion gira via `gittools/actions`.
- `GitVersion.yml` a root, workflow `GitFlow/v1`, bump da conventional commits, senza il prefisso `Merged PR` di Azure DevOps. Con lo squash merge di GitHub conta il titolo della PR; il suffisso `(#n)` non interferisce. Branch `master` con label vuota; `develop` con label `alpha`, predisposto per il futuro.

### 3.4 Dockerfile backend

`be/Dockerfile` e `be/Dockerfile.scheduler` sono già pronti con build-arg `VERSION` e `SHA`: nessuna modifica. Serve `be/.dockerignore` se assente, con `bin`, `obj`, `.local`, `.env*`.

## 4. Segreti e configurazione

### Key Vault

| Segreto / chiave | Uso |
| --- | --- |
| `ConnectionStrings--Fantastiche` | connection string SQL con login amministratore |
| `Mailgun--ApiKey` | invio email dallo Scheduler |
| `Bootstrap--Password` | password iniziale del SuperAdmin, usata dal job una tantum |
| chiave RSA 2048 `dataprotection` | cifratura dell'anello di chiavi DataProtection |

Il foundation crea la chiave; i segreti li scrive l'operatore con `just infra az-secrets`. Nessun segreto nel repository né nei `.bicepparam`.

### Variabili d'ambiente iniettate da `apps-be.bicep`

Comuni ad API, Scheduler e job:

- `ASPNETCORE_ENVIRONMENT=Production` (`DOTNET_ENVIRONMENT` per lo Scheduler, che è un Generic Host)
- `ConnectionStrings__Fantastiche` da secret ref Key Vault
- `DataProtection__BlobUri=https://stfantasticheprod.blob.core.windows.net/dataprotection/keys.xml`
- `DataProtection__KeyVaultKeyId=https://kv-fantastiche-prod.vault.azure.net/keys/dataprotection`
- `AZURE_CLIENT_ID` dal clientId dell'identità (`existing`)
- `Email__Provider=Mailgun`, `Email__EnableExternalDelivery=true`, `Mailgun__Domain`, `Mailgun__From`, `Mailgun__Region=EU`, `Mailgun__ApiKey` da secret ref. Anche l'API li riceve: `AddFantasticheInfrastructure` registra il sender Mailgun in ogni host fuori Development e il sender viene costruito con la chiave.
- `Storage__PlayerPhotos__PublicBaseUrl=https://stfantasticheprod.blob.core.windows.net/player-photos` e `Storage__ClubLogos__PublicBaseUrl=.../club-logos`

Solo API:

- `Cors__AllowedOrigins__0=<URL pubblico FE>`
- `Invitations__PublicBaseUrl=<URL pubblico FE>`

Solo job di migrazione: stesse variabili dell'API più `Bootstrap__Password` da secret ref, `Bootstrap__Email` e `Bootstrap__DisplayName` come parametri. Argomento di default `--migrate`; l'operatore lo avvia con `--args --bootstrap-superadmin` per creare il SuperAdmin.

`apps-fe.bicep` inietta solo `API_UPSTREAM`.

Valori Mailgun (`Mailgun__Domain`, `Mailgun__From`) e `Bootstrap__Email`: parametri nella pipeline, da compilare al provisioning. Fino ad allora placeholder espliciti che fanno fallire il deploy se non sostituiti.

## 5. Layout dei file

```
infra/
  README.md                     guida operativa (riscritta)
  justfile                      az-env, az-secrets
  scripts/az-secrets.sh
  main.bicep                    foundation, scope subscription
  prod.bicepparam               parametri prod senza segreti
  apps-be.bicep                 API + Scheduler + job migrazione, scope RG
  apps-fe.bicep                 FE, scope RG
  modules/
    registry.bicep  identity.bicep  ci-identity.bicep  log-analytics.bicep
    container-app-env.bicep  keyvault.bicep  sql.bicep  acr-role.bicep
    rg-contributor.bicep  storage.bicep  container-app.bicep  container-app-job.bicep
.github/workflows/
  build-deploy.yml
GitVersion.yml
fe/Dockerfile  fe/.dockerignore  fe/nginx/default.conf.template
be/.dockerignore
```

`main.bicep` espone in output ACR login server, id dell'environment, default domain, id identità, URI Key Vault, FQDN SQL, endpoint blob. Il justfile root aggiunge `mod infra` e i due alias `az-env` e `az-secrets` come ACKSD.

## 6. Pipeline `build-deploy.yml`

Trigger: `push` su `master` con `paths` `be/**`, `fe/**`, `infra/**`, `.github/workflows/**`, più `workflow_dispatch`. `concurrency: deploy-prod` con `cancel-in-progress: false`, così due push ravvicinati si accodano e l'ultimo vince senza sovrapporsi.

Permessi: `id-token: write` per OIDC, `contents: write` per il tag di release.

Job:

1. `version`: checkout con storia completa, `gittools/actions` setup ed execute, output `semver` e `major-minor-patch`.
2. `build-be` (needs `version`): `azure/login` con OIDC, `az acr build` di `fantastiche-api` e `fantastiche-scheduler`, tag `<semver>`, `<sha7>`, `prod`, build-arg `VERSION` e `SHA`.
3. `build-fe` (needs `version`, parallelo a `build-be`): `az acr build` di `fantastiche-fe` con `VITE_API_BASE_URL=` vuoto e `VITE_APP_VERSION=<semver>`.
4. `deploy-be` (needs `build-be`): `az deployment group create` di `apps-be.bicep` con immagini `:<semver>`; poi `az containerapp job start` su `fantastiche-migrate-prod` e attesa dell'esecuzione con esito `Succeeded`, altrimenti il job fallisce. Se il job esiste già, la migrazione gira prima del deploy delle app con la nuova immagine (`az containerapp job update --image` seguito da `job start`: l'override in `start` perderebbe env e argomenti); al primo deploy il job viene creato dal Bicep e avviato subito dopo. Loop di attesa in `.github/scripts/wait-job.sh`.
5. `deploy-fe` (needs `deploy-be`): `az deployment group create` di `apps-fe.bicep`.
6. `release-tag` (needs `deploy-fe`): crea e pusha `v<major-minor-patch>` se il tag non esiste. Idempotente. I job `deploy-be`, `deploy-fe` e `release-tag` girano solo se `github.ref == 'refs/heads/master'`.

Configurazione GitHub: variabili di repository `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`. Nessun segreto in GitHub.

Gli ambienti sono descritti come `env` nel workflow con i valori di `prod`; un secondo ambiente richiederebbe un job matrix o un reusable workflow, scelta rimandata.

Una PR non deploya: il workflow gira solo su push a `master`. Un `workflow_dispatch` da un altro branch non deploya né tagga (guardia `github.ref`) e in pratica non builda nemmeno: la federated credential accetta solo `refs/heads/master`, quindi `azure/login` fallisce.

## 7. Procedura operativa

Documentata in `infra/README.md`, eseguita dall'operatore con la propria sessione `az`:

1. `just infra az-env` imposta la subscription.
2. Foundation: `az deployment sub create -l westeurope -f infra/main.bicep -p infra/prod.bicepparam --parameters sqlAdministratorLoginPassword=<pw> operatorObjectId=<objectId> githubRepository=edoardoguerini/fantastiche`.
3. Federated credential dell'identità CI verso `repo:edoardoguerini@51255814/fantastiche@1361413706:ref:refs/heads/master` (GitHub include gli id numerici di owner e repository nel subject), creata dal foundation; in GitHub impostare le tre variabili.
4. Segreti in Key Vault con `just infra az-secrets`.
5. Primo run della pipeline (push o `workflow_dispatch`) che builda, deploya e migra.
6. Bootstrap SuperAdmin: `az containerapp job start -n fantastiche-migrate-prod -g rg-fantastiche-prod --args "--bootstrap-superadmin"`.
7. Immagini: `az storage blob upload-batch --account-name stfantasticheprod --destination player-photos --destination-path fantacalcio --source be/.local/player-photos --pattern "*.png" --content-type image/png --auth-mode login` e l'equivalente per `club-logos` con `--destination-path clubs`.
8. Catalogo e metadati media: regola firewall SQL per l'IP dell'operatore, poi i comandi `just be` di importazione con `ConnectionStrings__Fantastiche` puntata al DB Azure, come descritto in `be/docs/getting-started/local-storage.md`.

Il foundation non passa dalla CI, come ACKSD: modifiche a `main.bicep` vanno applicate a mano e verificate sullo stato reale.

## 8. Verifiche

- `az bicep build` su ogni template e `az bicep lint`; `what-if` del foundation prima del provisioning reale.
- `actionlint` sul workflow.
- Build locale dell'immagine FE e smoke: `/` restituisce la shell, una route profonda ricade su `_shell.html`, `/api/health/live` viene inoltrato a un upstream fittizio.
- Test backend esistenti verdi dopo la modifica DataProtection, più il test della selezione di modalità.
- Il primo deploy reale viene fatto insieme all'utente, che possiede la sessione `az`.

## 9. Fuori scope

Staging, custom domain, Private Endpoint, SQL passwordless, identità per app, migrazione separata dall'avvio API, protezione dell'importatore Python per Azure, PWA. Ruolo CI ristretto a `Microsoft.App/*` al posto di Contributor è il primo follow-up.
