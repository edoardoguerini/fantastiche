# infra/ — Infrastruttura Azure Fantastiche

IaC Bicep per l'ambiente `prod` su Azure Container Apps (FE nginx + API + Scheduler + job di migrazione, SQL Basic, Key Vault, storage, ACR condiviso). Design: `docs/superpowers/specs/2026-09-11-azure-deploy-design.md`. Pipeline: `.github/workflows/build-deploy.yml`.

## Target Azure

- Subscription **MPN - Mahiz** `818db21a-4d0b-430a-a42a-a00715f0345f` (tenant `58b8838a-bd0a-4297-94a7-43cbae09891e`).
- Regione **West Europe**. RG: `rg-fantastiche-shared` (ACR, identità CI) + `rg-fantastiche-prod`.
- URL pubblico: `https://fantastiche-fe-prod.<defaultDomain>`; l'API è interna e raggiunta solo dal proxy nginx del FE.

## Layout

- `main.bicep` — foundation subscription-scoped (RG, ACR, identità app e CI, Log Analytics, environment, Key Vault con chiave DataProtection, SQL, storage).
- `apps-be.bicep` — API + Scheduler + job `fantastiche-migrate-prod`; `apps-fe.bicep` — FE. Scope `rg-fantastiche-prod`, deploy Incremental, applicati dalla pipeline.
- `modules/` — moduli riusabili.
- `prod.bicepparam` — parametri senza segreti.
- `justfile` + `scripts/az-secrets.sh` — `just infra az-env`, `just infra az-secrets`, `just infra az-whatif`.

## Provisioning (una tantum, dall'operatore)

1. `just infra az-env` imposta la subscription.
2. `OBJ=$(az ad signed-in-user show --query id -o tsv)`.

Il foundation crea role assignment: va applicato da un utente Owner o User Access Administrator della subscription, mai dall'identità CI.

3. Foundation:

   ```bash
   az deployment sub create --location westeurope \
     --template-file infra/main.bicep --parameters infra/prod.bicepparam \
     --parameters operatorObjectId="$OBJ" sqlAdministratorLoginPassword='<password>'
   ```

   Output utili: `ciClientId`, `envDefaultDomain`, `sqlServerFqdn`, `dataProtectionKeyUri`.
4. In GitHub → Settings → Secrets and variables → Actions → **Variables**: `AZURE_CLIENT_ID` = output `ciClientId`, `AZURE_TENANT_ID` = `58b8838a-bd0a-4297-94a7-43cbae09891e`, `AZURE_SUBSCRIPTION_ID` = `818db21a-4d0b-430a-a42a-a00715f0345f`. Nessun secret.
5. Segreti in Key Vault con `just infra az-secrets`:
   - `ConnectionStrings--Fantastiche`: `Server=tcp:sql-fantastiche-prod.database.windows.net,1433;Database=fantastiche;User Id=fantasticheadmin;Password=<password>;Encrypt=True;TrustServerCertificate=False;`
   - `Mailgun--ApiKey`
   - `Bootstrap--Password` (almeno 12 caratteri, maiuscole/minuscole/numeri/simboli)

   Al primo giro il vault è vuoto: scegliere `2) nuovo segreto` per ciascuno dei tre segreti. Se `az keyvault secret set` risponde 403 subito dopo il foundation, il ruolo Secrets Officer non è ancora propagato: attendere un paio di minuti e riprovare.
6. Nel workflow `build-deploy.yml` sostituire `CHANGE_ME` in `MAILGUN_DOMAIN`, `MAILGUN_FROM`, `BOOTSTRAP_EMAIL` (non sono segreti) e committare.
7. Push su `master` (o `workflow_dispatch`): la pipeline builda le tre immagini; se il job `fantastiche-migrate-prod` esiste già lo avvia con la nuova immagine API e attende l'esito prima di applicare `apps-be.bicep`, altrimenti applica prima `apps-be.bicep` (che lo crea) e lo avvia subito dopo; poi applica `apps-fe.bicep` e crea il tag `vX.Y.Z`.
8. Bootstrap SuperAdmin (una volta): il job gira di default con `--migrate` (avviato dalla pipeline); `--bootstrap-superadmin` è un'esecuzione distinta dello stesso job, con argomenti diversi, e non parte mai da sola.

   ```bash
   az containerapp job start -n fantastiche-migrate-prod -g rg-fantastiche-prod --args "--bootstrap-superadmin"
   ```

9. Card e stemmi, dalle cartelle locali già scaricate (vedi `be/docs/getting-started/local-storage.md`):

   ```bash
   az storage blob upload-batch --account-name stfantasticheprod --auth-mode login \
     --destination player-photos --destination-path fantacalcio \
     --source be/.local/player-photos --pattern "*.png" --content-type image/png --overwrite
   az storage blob upload-batch --account-name stfantasticheprod --auth-mode login \
     --destination club-logos --destination-path clubs \
     --source be/.local/club-logos --pattern "*.png" --content-type image/png --overwrite
   ```

10. Catalogo e metadati media verso il DB Azure: regola firewall per il proprio IP, poi i comandi di importazione con la connection string del DB Azure:

    ```bash
    az sql server firewall-rule create -g rg-fantastiche-prod -s sql-fantastiche-prod -n operatore --start-ip-address <IP> --end-ip-address <IP>
    cd be && ConnectionStrings__Fantastiche='<connection string>' just photos-register
    cd be && ConnectionStrings__Fantastiche='<connection string>' just club-logos-register
    ```

    Il listone si carica dall'interfaccia SuperAdmin (catalogo) una volta entrati.

## Cosa fa e non fa la CI

La pipeline applica **solo** `apps-be.bicep` e `apps-fe.bicep`. Tutto ciò che sta in `main.bicep` (SQL, Key Vault, storage, ACR, environment, identità, ruoli) cambia solo con `az deployment sub create` manuale: dopo un merge che tocca quei moduli, riapplicare a mano e verificare lo stato reale. `just infra az-whatif <objectId>` mostra l'anteprima.

Se il job `fantastiche-migrate-prod` esiste già, la pipeline lo avvia **prima** di aggiornare le app, con la nuova immagine API (`az containerapp job start --image ...`), e attende `Succeeded` (`.github/scripts/wait-job.sh`, fino a 30 minuti): API e Scheduler ricevono la nuova immagine solo a schema già migrato. Al primo deploy il job non esiste ancora: viene creato dal Bicep e avviato subito dopo. Una migrazione fallita ferma la pipeline prima di toccare le app.

## Permessi della CI

`id-fantastiche-ci` ha Contributor su `rg-fantastiche-prod` e sull'ACR. Contributor non assegna ruoli, ma può disattivare l'RBAC del Key Vault e leggere i segreti: chiunque possa pushare su `master` raggiunge i segreti di produzione. Mitigazione minima: branch protection su `master` (PR obbligatoria). Scelta accettata per un progetto interno; ruolo custom ristretto a `Microsoft.App/*` come follow-up.

## Rete e cookie

FE e API su origin diverse romperebbero il cookie `SameSite=Lax`: `azurecontainerapps.io` è un suffisso pubblico. Perciò l'API ha ingress interno (`allowInsecure=true`, HTTP dentro l'environment) e nginx nel FE inoltra `/api/` e `/hubs/` (WebSocket) a `http://fantastiche-api-prod.internal.<defaultDomain>`. `Cors__AllowedOrigins__0` e `Invitations__PublicBaseUrl` valgono l'URL del FE.

I percorsi `/health/live` e `/health/ready` dell'API non passano dal proxy: sono raggiungibili solo dal probe di liveness del Container App (`/health/live`) e dai log. Diagnostica: `az containerapp logs show -n fantastiche-api-prod -g rg-fantastiche-prod --tail 100`.

Il rate limiter del login partiziona per IP client, quindi l'API deve conoscere l'IP reale dietro i proxy. La catena è: ingress del FE, nginx, ingress dell'API. nginx (`fe/nginx/default.conf.template`, modulo `realip`) prende come IP client l'**ultima** voce di `X-Forwarded-For`, cioè quella aggiunta dall'ingress fidato, e riscrive l'header verso l'API con quella sola voce: eventuali voci inviate dal client vengono scartate lì. `apps-be.bicep` imposta sull'API `ReverseProxy__TrustAllProxies=true` (l'ingress è interno, gli header sono affidabili) e `ReverseProxy__ForwardLimit=2`: l'API risale al più due voci partendo da destra, quindi arriva al client sia che l'ingress dell'API aggiunga la propria voce sia che non lo faccia. Senza questo assetto l'API vedrebbe solo l'IP del proxy e 5 login falliti da chiunque bloccherebbero tutti per un minuto. In locale i default restano invariati (solo loopback, un hop).

Precondizione: nessuna CDN o Front Door davanti al FE, altrimenti l'ultima voce di `X-Forwarded-For` sarebbe l'IP della CDN e tutti gli utenti tornerebbero in un unico bucket. Verifica dopo il deploy: il log di accesso di nginx stampa l'IP già risolto, quindi `az containerapp logs show -n fantastiche-fe-prod -g rg-fantastiche-prod --tail 50` deve mostrare IP pubblici diversi per client diversi; se mostra sempre lo stesso IP privato, l'ingress non sta aggiungendo la voce del client.

## DataProtection

API, Scheduler e job condividono l'anello di chiavi su `stfantasticheprod/dataprotection/keys.xml`, cifrato con la chiave `dataprotection` di `kv-fantastiche-prod`. Variabili: `DataProtection__BlobUri`, `DataProtection__KeyVaultKeyId`, `AZURE_CLIENT_ID` (obbligatorio con identità user-assigned, altrimenti l'endpoint MI risponde 400). In locale resta il file system.

## Repliche

API e FE `minReplicas=1` (nessun cold start durante un'asta), Scheduler sempre 1, API `maxReplicas=1` (SignalR in memoria, migrazione singola). Cambiare i parametri `apiMinReplicas`/`feMinReplicas` nel Bicep o nella pipeline, non con `az containerapp update`: ogni deploy riallinea Azure al Bicep.

## Accesso locale al DB

Connection string in `be/.env.prod` (gitignored) e regola firewall per il proprio IP (comando al punto 10). Non usare `docker compose down -v` sul DB locale pensando di agire su Azure: sono due mondi.

## Fuori scope

Staging, custom domain, Private Endpoint, SQL passwordless, identità per app, migrazione separata dall'avvio API, PWA.
