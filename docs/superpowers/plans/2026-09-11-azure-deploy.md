# Deploy Azure (Bicep + GitHub Actions) — Piano di implementazione

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Predisporre Bicep, immagini container, workflow GitHub Actions e procedura operativa per deployare Fantastiche su Azure Container Apps in un unico ambiente `prod`, senza eseguire alcun deploy in questo piano.

**Architecture:** Foundation Bicep a scope subscription (RG, ACR, identità, Log Analytics, Container Apps Environment, Key Vault, SQL, storage) applicato a mano; due Bicep per stack (`apps-be`, `apps-fe`) applicati dalla pipeline con le immagini appena buildate. Il FE (nginx) è l'unico ingresso pubblico e inoltra `/api` e `/hubs` all'API con ingress interno. Le migrazioni girano in un Container Apps Job avviato dalla pipeline. DataProtection su Blob + Key Vault via Managed Identity è l'unica modifica al backend.

**Tech Stack:** Bicep, Azure Container Apps, Azure SQL Basic, Key Vault RBAC, Azure Storage, GitHub Actions con OIDC (`azure/login@v3.1.0`, `gittools/actions@v4.7.0`, `actions/checkout@v7.0.1`), nginx unprivileged `1.31-alpine`, .NET 10, pacchetti `Azure.Extensions.AspNetCore.DataProtection.Blobs 1.5.4`, `Azure.Extensions.AspNetCore.DataProtection.Keys 1.6.4`, `Azure.Identity 1.21.0`.

**Spec:** `docs/superpowers/specs/2026-09-11-azure-deploy-design.md`

## Global Constraints

- Subscription `MPN - Mahiz` id `818db21a-4d0b-430a-a42a-a00715f0345f`, tenant `58b8838a-bd0a-4297-94a7-43cbae09891e`, regione `westeurope`.
- Un solo ambiente `prod`; `ASPNETCORE_ENVIRONMENT=Production`.
- Nessun identificativo, risorsa, credenziale o URL di ACKSD nei file: prefisso risorse `fantastiche`.
- Nessun segreto nel repository: `.bicepparam` con placeholder, Key Vault per i valori, GitHub con sole variabili non segrete.
- Documentazione, commenti e commit in italiano; codice e nomi risorse in inglese. Conventional commits con scope `be`, `fe`, `infra` o omesso. Nessuna firma AI né `Co-Authored-By` nei commit.
- Nessun deploy eseguito: solo `az bicep build`, `az bicep lint`, `actionlint`, build Docker locale e test .NET.
- Il backend gira con `python3 scripts/dev.py dotnet ...` da `be/` (vedi `be/justfile`): usare `just be test`, `just be build`.
- Le modifiche dell'altro agente nel working tree (file `fe/src/**`, `fe/tests/**`, `CLAUDE.md`, `fe/CLAUDE.md`, `fe/docs/**`) non vanno toccate né committate: ogni commit aggiunge solo i file elencati nel task (`git add <path>` espliciti, mai `git add -A`).

---

## Mappa dei file

| File | Responsabilità |
| --- | --- |
| `be/src/Fantastiche.Infrastructure/Common/DataProtectionSettings.cs` | Sceglie la modalità DataProtection (Azure o file) dalla configurazione |
| `be/src/Fantastiche.Infrastructure/DependencyInjection.cs` | Usa la modalità scelta |
| `be/tests/Fantastiche.UnitTests/Common/DataProtectionSettingsTests.cs` | Test della scelta |
| `be/Directory.Packages.props`, `be/src/Fantastiche.Infrastructure/Fantastiche.Infrastructure.csproj` | Pacchetti Azure |
| `be/.dockerignore` | Contesto build immagini backend |
| `fe/Dockerfile`, `fe/.dockerignore`, `fe/nginx/default.conf.template` | Immagine FE nginx con proxy |
| `fe/justfile` | `build-image`, `run-image` |
| `infra/modules/*.bicep` | Moduli riusabili |
| `infra/main.bicep`, `infra/prod.bicepparam` | Foundation |
| `infra/apps-be.bicep`, `infra/apps-fe.bicep` | Container App e job per stack |
| `infra/justfile`, `infra/scripts/az-secrets.sh`, `justfile` | Comandi operatore |
| `GitVersion.yml`, `.github/workflows/build-deploy.yml` | Versione e pipeline |
| `infra/README.md`, `CLAUDE.md`, `docs/workflow/git-workflow.md`, `fe/docs/getting-started/development-setup.md` | Documentazione |

---

### Task 1: DataProtection su Azure Blob + Key Vault (backend)

**Files:**
- Create: `be/src/Fantastiche.Infrastructure/Common/DataProtectionSettings.cs`
- Create: `be/tests/Fantastiche.UnitTests/Common/DataProtectionSettingsTests.cs`
- Modify: `be/src/Fantastiche.Infrastructure/DependencyInjection.cs:35-43`
- Modify: `be/Directory.Packages.props`
- Modify: `be/src/Fantastiche.Infrastructure/Fantastiche.Infrastructure.csproj`

**Interfaces:**
- Produces: `DataProtectionSettings.Resolve(IConfiguration configuration, string environment)` che restituisce `DataProtectionSettings.AzureBlob(Uri BlobUri, Uri KeyVaultKeyId)` oppure `DataProtectionSettings.FileSystem(string KeyPath, string? CertificatePath, string? CertificatePassword)`.
- Le variabili d'ambiente lette sono `DataProtection__BlobUri`, `DataProtection__KeyVaultKeyId`, `DataProtection__KeyPath`, `DataProtection__CertificatePath`, `DataProtection__CertificatePassword`; i Bicep del Task 5 le iniettano con questi nomi.

- [ ] **Step 1: Scrivere il test che fallisce**

`be/tests/Fantastiche.UnitTests/Common/DataProtectionSettingsTests.cs`:

```csharp
using Fantastiche.Infrastructure.Common;
using Microsoft.Extensions.Configuration;

namespace Fantastiche.UnitTests.Common;

public sealed class DataProtectionSettingsTests
{
    private static IConfiguration Build(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(v => v.Key, v => (string?)v.Value))
            .Build();

    [Fact]
    public void Resolve_ConBlobUriEKeyId_SceglieAzure()
    {
        var configuration = Build(
            ("DataProtection:BlobUri", "https://stfantasticheprod.blob.core.windows.net/dataprotection/keys.xml"),
            ("DataProtection:KeyVaultKeyId", "https://kv-fantastiche-prod.vault.azure.net/keys/dataprotection"));

        var settings = DataProtectionSettings.Resolve(configuration, "Production");

        var azure = Assert.IsType<DataProtectionSettings.AzureBlob>(settings);
        Assert.Equal("https://stfantasticheprod.blob.core.windows.net/dataprotection/keys.xml", azure.BlobUri.ToString());
        Assert.Equal("https://kv-fantastiche-prod.vault.azure.net/keys/dataprotection", azure.KeyVaultKeyId.ToString());
    }

    [Fact]
    public void Resolve_ConBlobUriSenzaKeyId_Fallisce()
    {
        var configuration = Build(("DataProtection:BlobUri", "https://stfantasticheprod.blob.core.windows.net/dataprotection/keys.xml"));

        var exception = Assert.Throws<InvalidOperationException>(() => DataProtectionSettings.Resolve(configuration, "Production"));

        Assert.Contains("DataProtection__KeyVaultKeyId", exception.Message);
    }

    [Fact]
    public void Resolve_InProductionSenzaPercorsi_Fallisce()
    {
        var configuration = Build();

        Assert.Throws<InvalidOperationException>(() => DataProtectionSettings.Resolve(configuration, "Production"));
    }

    [Fact]
    public void Resolve_InDevelopmentSenzaConfigurazione_UsaFileSystemDiDefault()
    {
        var configuration = Build();

        var settings = DataProtectionSettings.Resolve(configuration, "Development");

        var file = Assert.IsType<DataProtectionSettings.FileSystem>(settings);
        Assert.Equal(".local/keys", file.KeyPath);
        Assert.Null(file.CertificatePath);
    }

    [Fact]
    public void Resolve_ConPercorsiEspliciti_UsaFileSystemConCertificato()
    {
        var configuration = Build(
            ("DataProtection:KeyPath", "/app/.local/keys"),
            ("DataProtection:CertificatePath", "/app/.local/cert.pfx"),
            ("DataProtection:CertificatePassword", "segreto"));

        var settings = DataProtectionSettings.Resolve(configuration, "Production");

        var file = Assert.IsType<DataProtectionSettings.FileSystem>(settings);
        Assert.Equal("/app/.local/keys", file.KeyPath);
        Assert.Equal("/app/.local/cert.pfx", file.CertificatePath);
        Assert.Equal("segreto", file.CertificatePassword);
    }
}
```

- [ ] **Step 2: Eseguire il test e verificare che fallisca**

Run: `cd be && python3 scripts/dev.py dotnet test tests/Fantastiche.UnitTests --filter "FullyQualifiedName~DataProtectionSettingsTests"`
Expected: errore di compilazione, `DataProtectionSettings` non esiste.

- [ ] **Step 3: Implementare `DataProtectionSettings`**

`be/src/Fantastiche.Infrastructure/Common/DataProtectionSettings.cs`:

```csharp
using Microsoft.Extensions.Configuration;

namespace Fantastiche.Infrastructure.Common;

/// <summary>
/// Modalità di persistenza delle chiavi DataProtection, condivise da API e Scheduler.
/// Su Azure le chiavi stanno su Blob e sono cifrate con una chiave Key Vault;
/// in locale restano su file system, con certificato PFX fuori Development.
/// </summary>
public abstract record DataProtectionSettings
{
    public sealed record AzureBlob(Uri BlobUri, Uri KeyVaultKeyId) : DataProtectionSettings;

    public sealed record FileSystem(string KeyPath, string? CertificatePath, string? CertificatePassword) : DataProtectionSettings;

    public static DataProtectionSettings Resolve(IConfiguration configuration, string environment)
    {
        var blobUri = configuration["DataProtection:BlobUri"];
        if (!string.IsNullOrWhiteSpace(blobUri))
        {
            var keyId = configuration["DataProtection:KeyVaultKeyId"];
            if (string.IsNullOrWhiteSpace(keyId))
                throw new InvalidOperationException("DataProtection__BlobUri richiede anche DataProtection__KeyVaultKeyId.");
            return new AzureBlob(new Uri(blobUri, UriKind.Absolute), new Uri(keyId, UriKind.Absolute));
        }

        var keyPath = configuration["DataProtection:KeyPath"];
        var certificatePath = configuration["DataProtection:CertificatePath"];
        if (environment != "Development" && (string.IsNullOrWhiteSpace(keyPath) || string.IsNullOrWhiteSpace(certificatePath)))
            throw new InvalidOperationException(
                "Fuori Development servono DataProtection__KeyPath e DataProtection__CertificatePath persistenti, oppure DataProtection__BlobUri e DataProtection__KeyVaultKeyId.");
        return new FileSystem(keyPath ?? ".local/keys", certificatePath, configuration["DataProtection:CertificatePassword"]);
    }
}
```

- [ ] **Step 4: Aggiungere i pacchetti**

In `be/Directory.Packages.props`, nuovo ItemGroup prima di `<ItemGroup Label="Test">`:

```xml
  <ItemGroup Label="Azure">
    <PackageVersion Include="Azure.Identity" Version="1.21.0" />
    <PackageVersion Include="Azure.Extensions.AspNetCore.DataProtection.Blobs" Version="1.5.4" />
    <PackageVersion Include="Azure.Extensions.AspNetCore.DataProtection.Keys" Version="1.6.4" />
  </ItemGroup>
```

In `be/src/Fantastiche.Infrastructure/Fantastiche.Infrastructure.csproj`, nell'ItemGroup dei PackageReference, in ordine alfabetico:

```xml
    <PackageReference Include="Azure.Extensions.AspNetCore.DataProtection.Blobs" />
    <PackageReference Include="Azure.Extensions.AspNetCore.DataProtection.Keys" />
    <PackageReference Include="Azure.Identity" />
```

- [ ] **Step 5: Usare la modalità in `DependencyInjection.cs`**

Aggiungere `using Azure.Identity;` tra gli using. Sostituire le righe da `var keyPath = configuration["DataProtection:KeyPath"];` fino alla riga `protection.ProtectKeysWithCertificate(...)` inclusa (righe 36-43 attuali) con:

```csharp
        var protection = services.AddDataProtection().SetApplicationName("Fantastiche");
        switch (DataProtectionSettings.Resolve(configuration, environment))
        {
            case DataProtectionSettings.AzureBlob azure:
                // AZURE_CLIENT_ID in ambiente indica quale identità user-assigned usare.
                var credential = new DefaultAzureCredential();
                protection.PersistKeysToAzureBlobStorage(azure.BlobUri, credential)
                    .ProtectKeysWithAzureKeyVault(azure.KeyVaultKeyId, credential);
                break;
            case DataProtectionSettings.FileSystem file:
                protection.PersistKeysToFileSystem(new DirectoryInfo(file.KeyPath));
                if (!string.IsNullOrWhiteSpace(file.CertificatePath))
                    protection.ProtectKeysWithCertificate(X509CertificateLoader.LoadPkcs12FromFile(file.CertificatePath, file.CertificatePassword));
                break;
        }
```

La variabile `environment` è già definita alla riga precedente (`var environment = configuration["ASPNETCORE_ENVIRONMENT"] ?? ...`) e resta invariata.

- [ ] **Step 6: Eseguire test e build**

Run: `cd be && python3 scripts/dev.py dotnet restore Fantastiche.slnx && python3 scripts/dev.py dotnet test tests/Fantastiche.UnitTests`
Expected: tutti i test verdi, compresi i 5 nuovi.

Run: `cd be && python3 scripts/dev.py dotnet build Fantastiche.slnx --no-restore`
Expected: build senza warning nuovi.

- [ ] **Step 7: Commit**

```bash
git add be/src/Fantastiche.Infrastructure/Common/DataProtectionSettings.cs be/src/Fantastiche.Infrastructure/DependencyInjection.cs be/tests/Fantastiche.UnitTests/Common/DataProtectionSettingsTests.cs be/Directory.Packages.props be/src/Fantastiche.Infrastructure/Fantastiche.Infrastructure.csproj
git commit -m "feat(be): persiste le chiavi DataProtection su Blob e Key Vault"
```

---

### Task 2: Immagine frontend nginx con proxy verso l'API

**Files:**
- Create: `fe/Dockerfile`
- Create: `fe/.dockerignore`
- Create: `fe/nginx/default.conf.template`
- Create: `be/.dockerignore`
- Modify: `fe/justfile`

**Interfaces:**
- Produces: immagine che ascolta su `8080`, legge `API_UPSTREAM` (es. `http://fantastiche-api-prod.internal.<defaultDomain>`), build-arg `VITE_API_BASE_URL` (default vuoto) e `VITE_APP_VERSION`. Il Bicep `apps-fe.bicep` (Task 5) usa `targetPort: 8080` ed env `API_UPSTREAM`.

- [ ] **Step 1: `fe/.dockerignore`**

```
node_modules
dist
.tanstack
.vite
test-results
playwright-report
coverage
.env
.env.*
!.env.example
.git
.DS_Store
*.log
Dockerfile
.dockerignore
```

- [ ] **Step 2: `be/.dockerignore`**

```
**/bin/
**/obj/
**/.vs/
.local/
.git/
.gitignore
*.user
.env
.env.*
docker-compose*.yml
Dockerfile*
**/appsettings.Development.json
```

- [ ] **Step 3: template nginx**

`fe/nginx/default.conf.template`:

```nginx
# Reso a runtime dall'entrypoint nginx: solo ${API_UPSTREAM} viene sostituito
# (NGINX_ENVSUBST_FILTER nel Dockerfile). Le variabili nginx ($uri, ...) restano.
map $http_upgrade $connection_upgrade {
    default upgrade;
    ''      close;
}

server {
    listen 8080;
    server_name _;
    root /usr/share/nginx/html;

    # Caricamento CSV del listone dal browser.
    client_max_body_size 20m;

    gzip on;
    gzip_types text/plain text/css application/javascript application/json image/svg+xml;

    # API REST: stessa origin del FE, cookie SameSite=Lax intatti.
    location /api/ {
        proxy_pass ${API_UPSTREAM};
        proxy_http_version 1.1;
        proxy_set_header Host $proxy_host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto https;
        proxy_read_timeout 120s;
    }

    # SignalR: WebSocket con connessioni lunghe.
    location /hubs/ {
        proxy_pass ${API_UPSTREAM};
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection $connection_upgrade;
        proxy_set_header Host $proxy_host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto https;
        proxy_read_timeout 3600s;
        proxy_send_timeout 3600s;
    }

    # Asset con hash nel nome: cache lunga.
    location /assets/ {
        add_header Cache-Control "public, max-age=31536000, immutable";
        try_files $uri =404;
    }

    location = /_shell.html {
        add_header Cache-Control "no-cache";
    }

    # Route SPA: asset esistente, altrimenti la shell.
    location / {
        add_header Cache-Control "no-cache";
        try_files $uri /_shell.html;
    }
}
```

- [ ] **Step 4: `fe/Dockerfile`**

```dockerfile
# Immagine di produzione del frontend Fantastiche: build SPA + nginx.
# nginx serve gli asset statici e inoltra /api e /hubs all'API (stessa origin).

FROM node:24-alpine AS builder
WORKDIR /app
RUN corepack enable && corepack prepare pnpm@10.33.1 --activate
COPY package.json pnpm-lock.yaml ./
RUN pnpm install --frozen-lockfile
COPY . .
# Vuoto = stessa origin del sito (nginx fa da proxy). Build-time: inlineato da Vite.
ARG VITE_API_BASE_URL=
ENV VITE_API_BASE_URL=${VITE_API_BASE_URL}
ARG VITE_APP_VERSION=0.0.0
ENV VITE_APP_VERSION=${VITE_APP_VERSION}
RUN pnpm build

FROM nginxinc/nginx-unprivileged:1.31-alpine AS runtime
# L'entrypoint ufficiale rende /etc/nginx/templates/*.template in /etc/nginx/conf.d.
ENV NGINX_ENVSUBST_FILTER=^API_UPSTREAM$
ENV API_UPSTREAM=http://localhost:6060
COPY nginx/default.conf.template /etc/nginx/templates/default.conf.template
COPY --from=builder /app/dist/client /usr/share/nginx/html
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=5s --retries=3 \
    CMD wget -qO- http://127.0.0.1:8080/_shell.html > /dev/null || exit 1
```

- [ ] **Step 5: comandi `just` per l'immagine**

Aggiungere in coda a `fe/justfile`:

```just
# Costruisce l'immagine di produzione (nginx + SPA) con tag locale
build-image:
    docker build -t fantastiche-fe:local --build-arg VITE_APP_VERSION=local .

# Avvia l'immagine su http://localhost:8080 inoltrando /api all'API locale 6060
run-image:
    docker run --rm -p 8080:8080 -e API_UPSTREAM=http://host.docker.internal:6060 fantastiche-fe:local
```

- [ ] **Step 6: build e smoke**

Run: `cd fe && just build-image`
Expected: build completata; il prerender della shell nel builder termina senza errori.

Run:

```bash
cd fe && docker run -d --rm --name fe-smoke -p 8080:8080 -e API_UPSTREAM=http://host.docker.internal:6060 fantastiche-fe:local
sleep 2
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:8080/            # atteso 200
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:8080/leagues/xyz # atteso 200 (fallback shell)
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:8080/api/health/live # atteso 200 con API locale avviata, 502 senza
docker exec fe-smoke cat /etc/nginx/conf.d/default.conf | grep proxy_pass     # atteso host.docker.internal:6060, nessun ${API_UPSTREAM}
docker stop fe-smoke
```

Expected: codici come indicato; `proxy_pass http://host.docker.internal:6060;` reso nel file.

- [ ] **Step 7: Commit**

```bash
git add fe/Dockerfile fe/.dockerignore fe/nginx/default.conf.template fe/justfile be/.dockerignore
git commit -m "feat(fe): aggiunge l'immagine nginx con proxy verso l'API"
```

---

### Task 3: Moduli Bicep della foundation

**Files:**
- Create: `infra/modules/registry.bicep`
- Create: `infra/modules/identity.bicep`
- Create: `infra/modules/ci-identity.bicep`
- Create: `infra/modules/log-analytics.bicep`
- Create: `infra/modules/container-app-env.bicep`
- Create: `infra/modules/keyvault.bicep`
- Create: `infra/modules/sql.bicep`
- Create: `infra/modules/acr-role.bicep`
- Create: `infra/modules/rg-contributor.bicep`
- Create: `infra/modules/storage.bicep`

**Interfaces:**
- Produces gli output usati da `main.bicep` (Task 4): `registry.outputs.{name,loginServer}`, `identity.outputs.{id,principalId,clientId}`, `ciIdentity.outputs.{principalId,clientId}`, `logs.outputs.name`, `caEnv.outputs.{id,defaultDomain}`, `keyvault.outputs.{uri,dataProtectionKeyUri}`, `sql.outputs.serverFqdn`, `storage.outputs.blobEndpoint`.

- [ ] **Step 1: `infra/modules/registry.bicep`**

```bicep
@description('Azure Container Registry condiviso (Basic), admin user disabilitato: pull e push solo via RBAC.')
param name string
param location string
param tags object = {}

resource acr 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: name
  location: location
  tags: tags
  sku: { name: 'Basic' }
  properties: {
    adminUserEnabled: false
  }
}

output id string = acr.id
output loginServer string = acr.properties.loginServer
output name string = acr.name
```

- [ ] **Step 2: `infra/modules/identity.bicep`**

```bicep
@description('User-assigned managed identity condivisa da API, Scheduler, FE e job di migrazione.')
param name string
param location string
param tags object = {}

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: name
  location: location
  tags: tags
}

output id string = identity.id
output principalId string = identity.properties.principalId
output clientId string = identity.properties.clientId
```

- [ ] **Step 3: `infra/modules/ci-identity.bicep`**

```bicep
@description('Identità della pipeline GitHub Actions con federated credential OIDC: nessun segreto in GitHub.')
param name string
param location string
param tags object = {}
@description('Repository GitHub nel formato owner/repo.')
param githubRepository string
@description('Branch autorizzato a ottenere token (subject ref:refs/heads/<branch>).')
param githubBranch string = 'master'

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: name
  location: location
  tags: tags
}

resource federated 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = {
  parent: identity
  name: 'github-${replace(githubBranch, '/', '-')}'
  properties: {
    issuer: 'https://token.actions.githubusercontent.com'
    subject: 'repo:${githubRepository}:ref:refs/heads/${githubBranch}'
    audiences: [ 'api://AzureADTokenExchange' ]
  }
}

output id string = identity.id
output principalId string = identity.properties.principalId
output clientId string = identity.properties.clientId
```

- [ ] **Step 4: `infra/modules/log-analytics.bicep`**

```bicep
@description('Log Analytics workspace per i log dei Container App.')
param name string
param location string
param tags object = {}

resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
    features: { searchVersion: 1 }
  }
}

output id string = workspace.id
output name string = workspace.name
```

- [ ] **Step 5: `infra/modules/container-app-env.bicep`**

```bicep
@description('Container Apps Environment (Consumption) collegato a Log Analytics.')
param name string
param location string
param tags object = {}
param logAnalyticsWorkspaceName string

resource law 'Microsoft.OperationalInsights/workspaces@2023-09-01' existing = {
  name: logAnalyticsWorkspaceName
}

resource env 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: law.properties.customerId
        sharedKey: law.listKeys().primarySharedKey
      }
    }
  }
}

output id string = env.id
output defaultDomain string = env.properties.defaultDomain
```

- [ ] **Step 6: `infra/modules/keyvault.bicep`**

```bicep
@description('Key Vault RBAC con la chiave RSA per DataProtection. La MI legge i segreti e usa la chiave; l operatore scrive i segreti.')
param name string
param location string
param tags object = {}
param tenantId string
@description('principalId della managed identity dell app (Secrets User + Crypto User).')
param appPrincipalId string
@description('objectId dell operatore che scrive i segreti (Secrets Officer).')
param operatorObjectId string

var secretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'   // Key Vault Secrets User
var secretsOfficerRoleId = 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7' // Key Vault Secrets Officer
var cryptoUserRoleId = '12338af0-0e69-4776-bea7-57ae8d297424'     // Key Vault Crypto User

resource kv 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    tenantId: tenantId
    sku: { family: 'A', name: 'standard' }
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
    publicNetworkAccess: 'Enabled'
  }
}

// Chiave che cifra l anello di chiavi DataProtection salvato su Blob.
resource dataProtectionKey 'Microsoft.KeyVault/vaults/keys@2023-07-01' = {
  parent: kv
  name: 'dataprotection'
  properties: {
    kty: 'RSA'
    keySize: 2048
    keyOps: [ 'wrapKey', 'unwrapKey' ]
  }
}

resource appSecretsUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(kv.id, appPrincipalId, secretsUserRoleId)
  scope: kv
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', secretsUserRoleId)
    principalId: appPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource appCryptoUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(kv.id, appPrincipalId, cryptoUserRoleId)
  scope: kv
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', cryptoUserRoleId)
    principalId: appPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource operatorSecretsOfficer 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(kv.id, operatorObjectId, secretsOfficerRoleId)
  scope: kv
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', secretsOfficerRoleId)
    principalId: operatorObjectId
    principalType: 'User'
  }
}

output id string = kv.id
output name string = kv.name
output uri string = kv.properties.vaultUri
@description('Identificativo della chiave senza versione: DataProtection ruota da sé quando la chiave cambia.')
output dataProtectionKeyUri string = dataProtectionKey.properties.keyUri
```

- [ ] **Step 7: `infra/modules/sql.bicep`**

```bicep
@description('SQL logical server + database DTU + firewall per i servizi Azure.')
param serverName string
param databaseName string
param location string
param tags object = {}
param administratorLogin string
@secure()
param administratorLoginPassword string
@description('SKU DTU: Basic (5 DTU, 2 GB). Niente serverless: costo fisso e nessun cold start.')
param skuName string = 'Basic'
param skuTier string = 'Basic'
param maxSizeBytes int = 2147483648

resource sqlServer 'Microsoft.Sql/servers@2023-08-01' = {
  name: serverName
  location: location
  tags: tags
  properties: {
    administratorLogin: administratorLogin
    administratorLoginPassword: administratorLoginPassword
    version: '12.0'
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

resource database 'Microsoft.Sql/servers/databases@2023-08-01' = {
  parent: sqlServer
  name: databaseName
  location: location
  tags: tags
  sku: {
    name: skuName
    tier: skuTier
  }
  properties: {
    maxSizeBytes: maxSizeBytes
    zoneRedundant: false
  }
}

// Consente ai Container App (IP dinamici) di raggiungere il server.
resource allowAzure 'Microsoft.Sql/servers/firewallRules@2023-08-01' = {
  parent: sqlServer
  name: 'AllowAllAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

output serverFqdn string = sqlServer.properties.fullyQualifiedDomainName
output serverName string = sqlServer.name
output databaseName string = database.name
```

- [ ] **Step 8: `infra/modules/acr-role.bicep` e `infra/modules/rg-contributor.bicep`**

`infra/modules/acr-role.bicep`:

```bicep
@description('Role assignment sull ACR condiviso: AcrPull per la MI dell app, Contributor per la CI (az acr build richiede scheduleRun).')
param acrName string
param principalId string
@description('Id del ruolo: 7f951dda-4ed3-4680-a7ca-43fe172d538d = AcrPull, b24988ac-6180-42a0-ab88-20f7382dd24c = Contributor.')
param roleId string

resource acr 'Microsoft.ContainerRegistry/registries@2023-07-01' existing = {
  name: acrName
}

resource assignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(acr.id, principalId, roleId)
  scope: acr
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleId)
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}
```

`infra/modules/rg-contributor.bicep`:

```bicep
@description('Contributor sul resource group corrente: consente alla CI di applicare apps-be/apps-fe e avviare il job di migrazione.')
param principalId string

var contributorRoleId = 'b24988ac-6180-42a0-ab88-20f7382dd24c'

resource assignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, principalId, contributorRoleId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', contributorRoleId)
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}
```

- [ ] **Step 9: `infra/modules/storage.bicep`**

```bicep
@description('Storage account unico: card e stemmi a lettura anonima per-blob, anello DataProtection privato. Scrittura solo RBAC.')
param name string
param location string
param tags object = {}
@description('principalId della MI dell app: scrive l anello DataProtection.')
param appPrincipalId string
@description('objectId dell operatore che carica card e stemmi con az storage blob upload-batch --auth-mode login.')
param operatorObjectId string

var blobDataContributorRoleId = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe' // Storage Blob Data Contributor

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: name
  location: location
  tags: tags
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    // Necessario per i due container pubblici; il container dataprotection resta None.
    allowBlobPublicAccess: true
    // Solo RBAC: niente account key.
    allowSharedKeyAccess: false
    publicNetworkAccess: 'Enabled'
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storage
  name: 'default'
  properties: {
    deleteRetentionPolicy: {
      enabled: true
      days: 7
    }
  }
}

// 'Blob' = singolo blob leggibile via URL, elenco del container negato.
resource playerPhotos 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: 'player-photos'
  properties: { publicAccess: 'Blob' }
}

resource clubLogos 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: 'club-logos'
  properties: { publicAccess: 'Blob' }
}

resource dataProtection 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: 'dataprotection'
  properties: { publicAccess: 'None' }
}

resource appWrite 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storage.id, appPrincipalId, blobDataContributorRoleId)
  scope: storage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', blobDataContributorRoleId)
    principalId: appPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource operatorWrite 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storage.id, operatorObjectId, blobDataContributorRoleId)
  scope: storage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', blobDataContributorRoleId)
    principalId: operatorObjectId
    principalType: 'User'
  }
}

output id string = storage.id
output name string = storage.name
output blobEndpoint string = storage.properties.primaryEndpoints.blob
```

- [ ] **Step 10: Verifica sintassi**

Run: `for f in infra/modules/*.bicep; do az bicep build --file "$f" --stdout > /dev/null && echo "ok $f"; done`
Expected: `ok` per tutti i 10 moduli, nessun errore. Warning accettabili solo se documentati nel commit.

- [ ] **Step 11: Commit**

```bash
git add infra/modules
git commit -m "feat(infra): aggiunge i moduli Bicep della foundation"
```

---

### Task 4: Foundation `main.bicep` e parametri

**Files:**
- Create: `infra/main.bicep`
- Create: `infra/prod.bicepparam`

**Interfaces:**
- Consumes: i moduli del Task 3.
- Produces: risorse con i nomi fissati dalla specifica; la pipeline (Task 7) ricostruisce gli id dal naming `rg-fantastiche-<env>`, `cae-fantastiche-<env>`, `id-fantastiche-<env>`, `kv-fantastiche-<env>`, `stfantastiche<env>`.

- [ ] **Step 1: `infra/main.bicep`**

```bicep
targetScope = 'subscription'

// Vincolato: entra nei nomi delle risorse. Oggi esiste solo prod.
@description('Nome ambiente.')
@allowed([
  'prod'
])
param environmentName string = 'prod'
param location string = 'westeurope'
param sqlAdministratorLogin string
@secure()
param sqlAdministratorLoginPassword string
@description('objectId dell operatore che scrive i segreti in Key Vault e carica le immagini.')
param operatorObjectId string
@description('Repository GitHub (owner/repo) autorizzato via OIDC a deployare.')
param githubRepository string
@description('Branch che rilascia l ambiente.')
param githubBranch string = 'master'

var tags = {
  project: 'fantastiche'
  environment: environmentName
  managedBy: 'bicep'
}
var sharedRgName = 'rg-fantastiche-shared'
var envRgName = 'rg-fantastiche-${environmentName}'
var acrPullRoleId = '7f951dda-4ed3-4680-a7ca-43fe172d538d'
var contributorRoleId = 'b24988ac-6180-42a0-ab88-20f7382dd24c'

resource sharedRg 'Microsoft.Resources/resourceGroups@2023-07-01' = {
  name: sharedRgName
  location: location
  tags: tags
}

resource envRg 'Microsoft.Resources/resourceGroups@2023-07-01' = {
  name: envRgName
  location: location
  tags: tags
}

module registry 'modules/registry.bicep' = {
  name: 'registry'
  scope: sharedRg
  params: { name: 'fantasticheacr', location: location, tags: tags }
}

module ciIdentity 'modules/ci-identity.bicep' = {
  name: 'ciIdentity'
  scope: sharedRg
  params: {
    name: 'id-fantastiche-ci'
    location: location
    tags: tags
    githubRepository: githubRepository
    githubBranch: githubBranch
  }
}

module identity 'modules/identity.bicep' = {
  name: 'identity'
  scope: envRg
  params: { name: 'id-fantastiche-${environmentName}', location: location, tags: tags }
}

module logs 'modules/log-analytics.bicep' = {
  name: 'logs'
  scope: envRg
  params: { name: 'log-fantastiche-${environmentName}', location: location, tags: tags }
}

module caEnv 'modules/container-app-env.bicep' = {
  name: 'caEnv'
  scope: envRg
  params: {
    name: 'cae-fantastiche-${environmentName}'
    location: location
    tags: tags
    logAnalyticsWorkspaceName: logs.outputs.name
  }
}

module keyvault 'modules/keyvault.bicep' = {
  name: 'keyvault'
  scope: envRg
  params: {
    name: 'kv-fantastiche-${environmentName}'
    location: location
    tags: tags
    tenantId: subscription().tenantId
    appPrincipalId: identity.outputs.principalId
    operatorObjectId: operatorObjectId
  }
}

module sql 'modules/sql.bicep' = {
  name: 'sql'
  scope: envRg
  params: {
    serverName: 'sql-fantastiche-${environmentName}'
    databaseName: 'fantastiche'
    location: location
    tags: tags
    administratorLogin: sqlAdministratorLogin
    administratorLoginPassword: sqlAdministratorLoginPassword
  }
}

module storage 'modules/storage.bicep' = {
  name: 'storage'
  scope: envRg
  params: {
    name: 'stfantastiche${environmentName}'
    location: location
    tags: tags
    appPrincipalId: identity.outputs.principalId
    operatorObjectId: operatorObjectId
  }
}

module appAcrPull 'modules/acr-role.bicep' = {
  name: 'appAcrPull'
  scope: sharedRg
  params: {
    acrName: registry.outputs.name
    principalId: identity.outputs.principalId
    roleId: acrPullRoleId
  }
}

module ciAcrContributor 'modules/acr-role.bicep' = {
  name: 'ciAcrContributor'
  scope: sharedRg
  params: {
    acrName: registry.outputs.name
    principalId: ciIdentity.outputs.principalId
    roleId: contributorRoleId
  }
}

module ciRgContributor 'modules/rg-contributor.bicep' = {
  name: 'ciRgContributor'
  scope: envRg
  params: { principalId: ciIdentity.outputs.principalId }
}

output acrLoginServer string = registry.outputs.loginServer
output acrName string = registry.outputs.name
output ciClientId string = ciIdentity.outputs.clientId
output envId string = caEnv.outputs.id
output envDefaultDomain string = caEnv.outputs.defaultDomain
output identityId string = identity.outputs.id
output kvUri string = keyvault.outputs.uri
output dataProtectionKeyUri string = keyvault.outputs.dataProtectionKeyUri
output sqlServerFqdn string = sql.outputs.serverFqdn
output blobEndpoint string = storage.outputs.blobEndpoint
```

- [ ] **Step 2: `infra/prod.bicepparam`**

```bicep
using 'main.bicep'

param environmentName = 'prod'
param location = 'westeurope'
param sqlAdministratorLogin = 'fantasticheadmin'
param githubRepository = 'edoardoguerini/fantastiche'
param githubBranch = 'master'
// Valori reali passati da CLI a runtime (NON committare segreti):
//   --parameters sqlAdministratorLoginPassword=... operatorObjectId=...
param sqlAdministratorLoginPassword = ''
param operatorObjectId = ''
```

- [ ] **Step 3: Verifica**

Run: `az bicep build --file infra/main.bicep --stdout > /dev/null && az bicep build-params --file infra/prod.bicepparam --stdout > /dev/null && echo ok`
Expected: `ok`, nessun errore.

Run: `az bicep lint --file infra/main.bicep`
Expected: nessun errore; eventuali warning riportati nel commit.

- [ ] **Step 4: Commit**

```bash
git add infra/main.bicep infra/prod.bicepparam
git commit -m "feat(infra): definisce la foundation Azure di Fantastiche"
```

---

### Task 5: Bicep delle app: API, Scheduler, job di migrazione, FE

**Files:**
- Create: `infra/modules/container-app.bicep`
- Create: `infra/modules/container-app-job.bicep`
- Create: `infra/apps-be.bicep`
- Create: `infra/apps-fe.bicep`

**Interfaces:**
- Consumes: variabili d'ambiente definite in Task 1 (`DataProtection__BlobUri`, `DataProtection__KeyVaultKeyId`), porta `8080` ed env `API_UPSTREAM` dell'immagine FE (Task 2).
- Produces: parametri che la pipeline (Task 7) passa a `apps-be.bicep`: `environmentName, environmentId, identityId, acrLoginServer, kvUri, apiImage, schedulerImage, frontendBaseUrl, blobEndpoint, dataProtectionKeyId, mailgunDomain, mailgunFrom, bootstrapEmail`; a `apps-fe.bicep`: `environmentName, environmentId, identityId, acrLoginServer, feImage, apiUpstream`. Nomi delle app: `fantastiche-api-<env>`, `fantastiche-scheduler-<env>`, `fantastiche-migrate-<env>`, `fantastiche-fe-<env>`.

- [ ] **Step 1: `infra/modules/container-app.bicep`**

```bicep
@description('Container App riusabile (fe/api/scheduler).')
param name string
param location string
param tags object = {}
param environmentId string
param userAssignedIdentityId string
param acrLoginServer string
param image string
param ingressEnabled bool = true
@description('true = ingress pubblico; false = raggiungibile solo dentro l environment.')
param external bool = true
@description('true consente HTTP sull ingress: usato dall API interna, cosi nginx la raggiunge senza TLS.')
param allowInsecure bool = false
param targetPort int = 8080
param minReplicas int = 1
param maxReplicas int = 1
@description('Env var: array di { name, value } oppure { name, secretRef }.')
param envVars array = []
@description('Secrets: array di { name, keyVaultUrl, identity }.')
param secrets array = []

resource app 'Microsoft.App/containerApps@2024-03-01' = {
  name: name
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${userAssignedIdentityId}': {}
    }
  }
  properties: {
    managedEnvironmentId: environmentId
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: ingressEnabled ? {
        external: external
        targetPort: targetPort
        transport: 'auto'
        allowInsecure: allowInsecure
      } : null
      registries: [
        {
          server: acrLoginServer
          identity: userAssignedIdentityId
        }
      ]
      secrets: secrets
    }
    template: {
      containers: [
        {
          name: name
          image: image
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: envVars
        }
      ]
      scale: {
        minReplicas: minReplicas
        maxReplicas: maxReplicas
      }
    }
  }
}

output fqdn string = ingressEnabled ? app.properties.configuration.ingress.fqdn : ''
output name string = app.name
```

- [ ] **Step 2: `infra/modules/container-app-job.bicep`**

```bicep
@description('Container Apps Job a trigger manuale: migrazioni EF e bootstrap SuperAdmin con l immagine API.')
param name string
param location string
param tags object = {}
param environmentId string
param userAssignedIdentityId string
param acrLoginServer string
param image string
@description('Argomenti di default del container (la CI usa --migrate; l operatore puo sovrascriverli con az containerapp job start --args).')
param args array = [ '--migrate' ]
param envVars array = []
param secrets array = []

resource job 'Microsoft.App/jobs@2024-03-01' = {
  name: name
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${userAssignedIdentityId}': {}
    }
  }
  properties: {
    environmentId: environmentId
    configuration: {
      triggerType: 'Manual'
      replicaTimeout: 1800
      replicaRetryLimit: 0
      manualTriggerConfig: {
        parallelism: 1
        replicaCompletionCount: 1
      }
      registries: [
        {
          server: acrLoginServer
          identity: userAssignedIdentityId
        }
      ]
      secrets: secrets
    }
    template: {
      containers: [
        {
          name: name
          image: image
          args: args
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: envVars
        }
      ]
    }
  }
}

output name string = job.name
```

- [ ] **Step 3: `infra/apps-be.bicep`**

```bicep
@description('Deploy di API, Scheduler e job di migrazione nel RG dell ambiente. Deploy Incremental: non tocca il FE.')
param location string = resourceGroup().location
param environmentName string = 'prod'
param environmentId string
param identityId string
param acrLoginServer string
@description('Vault URI, es. https://kv-fantastiche-prod.vault.azure.net/ (termina con /).')
param kvUri string
param apiImage string
param schedulerImage string
@description('URL pubblico del FE: origine ammessa per il WebSocket e base dei link negli inviti.')
param frontendBaseUrl string
@description('Blob endpoint dello storage, es. https://stfantasticheprod.blob.core.windows.net/ (termina con /).')
param blobEndpoint string
@description('Identificativo della chiave Key Vault per DataProtection (output dataProtectionKeyUri della foundation).')
param dataProtectionKeyId string
@description('Dominio Mailgun (non segreto).')
param mailgunDomain string
@description('Mittente delle email (non segreto).')
param mailgunFrom string
param mailgunRegion string = 'EU'
@description('Email del SuperAdmin creato dal job con --bootstrap-superadmin. La password sta in Key Vault.')
param bootstrapEmail string
param bootstrapDisplayName string = 'SuperAdmin'
@description('Repliche minime dell API: 1 = sempre accesa (asta senza cold start); 0 = scale-to-zero.')
@minValue(0)
@maxValue(1)
param apiMinReplicas int = 1

var tags = {
  project: 'fantastiche'
  environment: environmentName
  managedBy: 'bicep'
}

// AZURE_CLIENT_ID: con la sola identità user-assigned DefaultAzureCredential deve
// sapere quale chiedere, altrimenti l endpoint MI risponde 400.
resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = {
  name: last(split(identityId, '/'))
}

var connSecret = {
  name: 'connectionstrings-fantastiche'
  keyVaultUrl: '${kvUri}secrets/ConnectionStrings--Fantastiche'
  identity: identityId
}
var mailgunSecret = {
  name: 'mailgun-apikey'
  keyVaultUrl: '${kvUri}secrets/Mailgun--ApiKey'
  identity: identityId
}
var bootstrapSecret = {
  name: 'bootstrap-password'
  keyVaultUrl: '${kvUri}secrets/Bootstrap--Password'
  identity: identityId
}

// Comuni ad API, Scheduler e job: DB, DataProtection condivisa, email, URL immagini.
var sharedEnv = [
  { name: 'ConnectionStrings__Fantastiche', secretRef: 'connectionstrings-fantastiche' }
  { name: 'DataProtection__BlobUri', value: '${blobEndpoint}dataprotection/keys.xml' }
  { name: 'DataProtection__KeyVaultKeyId', value: dataProtectionKeyId }
  { name: 'AZURE_CLIENT_ID', value: identity.properties.clientId }
  // AddFantasticheInfrastructure registra il sender Mailgun in ogni host fuori Development.
  { name: 'Email__Provider', value: 'Mailgun' }
  { name: 'Email__EnableExternalDelivery', value: 'true' }
  { name: 'Mailgun__Domain', value: mailgunDomain }
  { name: 'Mailgun__From', value: mailgunFrom }
  { name: 'Mailgun__Region', value: mailgunRegion }
  { name: 'Mailgun__ApiKey', secretRef: 'mailgun-apikey' }
  { name: 'Storage__PlayerPhotos__PublicBaseUrl', value: '${blobEndpoint}player-photos' }
  { name: 'Storage__ClubLogos__PublicBaseUrl', value: '${blobEndpoint}club-logos' }
]

var apiEnv = concat(sharedEnv, [
  { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
  { name: 'Cors__AllowedOrigins__0', value: frontendBaseUrl }
  { name: 'Invitations__PublicBaseUrl', value: frontendBaseUrl }
])

module api 'modules/container-app.bicep' = {
  name: 'api'
  params: {
    name: 'fantastiche-api-${environmentName}'
    location: location
    tags: tags
    environmentId: environmentId
    userAssignedIdentityId: identityId
    acrLoginServer: acrLoginServer
    image: apiImage
    ingressEnabled: true
    // Solo il FE e pubblico: nginx inoltra /api e /hubs all FQDN interno in HTTP.
    external: false
    allowInsecure: true
    targetPort: 8080
    minReplicas: apiMinReplicas
    // max 1: SignalR in memoria e nessuna migrazione concorrente.
    maxReplicas: 1
    secrets: [ connSecret, mailgunSecret ]
    envVars: apiEnv
  }
}

module scheduler 'modules/container-app.bicep' = {
  name: 'scheduler'
  params: {
    name: 'fantastiche-scheduler-${environmentName}'
    location: location
    tags: tags
    environmentId: environmentId
    userAssignedIdentityId: identityId
    acrLoginServer: acrLoginServer
    image: schedulerImage
    ingressEnabled: false
    minReplicas: 1
    maxReplicas: 1
    secrets: [ connSecret, mailgunSecret ]
    envVars: concat(sharedEnv, [
      // Generic Host: legge DOTNET_ENVIRONMENT; AddFantasticheInfrastructure legge anche ASPNETCORE_ENVIRONMENT.
      { name: 'DOTNET_ENVIRONMENT', value: 'Production' }
      { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
    ])
  }
}

module migrate 'modules/container-app-job.bicep' = {
  name: 'migrate'
  params: {
    name: 'fantastiche-migrate-${environmentName}'
    location: location
    tags: tags
    environmentId: environmentId
    userAssignedIdentityId: identityId
    acrLoginServer: acrLoginServer
    image: apiImage
    args: [ '--migrate' ]
    secrets: [ connSecret, mailgunSecret, bootstrapSecret ]
    envVars: concat(apiEnv, [
      { name: 'Bootstrap__Email', value: bootstrapEmail }
      { name: 'Bootstrap__DisplayName', value: bootstrapDisplayName }
      { name: 'Bootstrap__Password', secretRef: 'bootstrap-password' }
    ])
  }
}

output apiInternalFqdn string = api.outputs.fqdn
output migrateJobName string = migrate.outputs.name
```

- [ ] **Step 4: `infra/apps-fe.bicep`**

```bicep
@description('Deploy del Container App frontend (nginx + SPA) nel RG dell ambiente. Deploy Incremental: non tocca BE/Scheduler.')
param location string = resourceGroup().location
param environmentName string = 'prod'
param environmentId string
param identityId string
param acrLoginServer string
param feImage string
@description('URL interno dell API usato dal proxy nginx, es. http://fantastiche-api-prod.internal.<defaultDomain>.')
param apiUpstream string
@description('Repliche minime del FE: 1 = sempre acceso; 0 = scale-to-zero.')
@minValue(0)
param feMinReplicas int = 1

var tags = {
  project: 'fantastiche'
  environment: environmentName
  managedBy: 'bicep'
}

module fe 'modules/container-app.bicep' = {
  name: 'fe'
  params: {
    name: 'fantastiche-fe-${environmentName}'
    location: location
    tags: tags
    environmentId: environmentId
    userAssignedIdentityId: identityId
    acrLoginServer: acrLoginServer
    image: feImage
    ingressEnabled: true
    external: true
    targetPort: 8080
    minReplicas: feMinReplicas
    maxReplicas: 2
    secrets: []
    envVars: [
      { name: 'API_UPSTREAM', value: apiUpstream }
    ]
  }
}

output feFqdn string = fe.outputs.fqdn
```

- [ ] **Step 5: Verifica**

Run: `for f in infra/modules/container-app.bicep infra/modules/container-app-job.bicep infra/apps-be.bicep infra/apps-fe.bicep; do az bicep build --file "$f" --stdout > /dev/null && echo "ok $f"; done; az bicep lint --file infra/apps-be.bicep; az bicep lint --file infra/apps-fe.bicep`
Expected: `ok` per tutti, nessun errore.

- [ ] **Step 6: Commit**

```bash
git add infra/modules/container-app.bicep infra/modules/container-app-job.bicep infra/apps-be.bicep infra/apps-fe.bicep
git commit -m "feat(infra): definisce i Container App e il job di migrazione"
```

---

### Task 6: Comandi `just` per l'operatore

**Files:**
- Create: `infra/justfile`
- Create: `infra/scripts/az-secrets.sh`
- Modify: `justfile` (root)

- [ ] **Step 1: `infra/justfile`**

```just
# =====================================================================
# Fantastiche — justfile infra (operazioni Azure).
# Uso: `just infra` (dal root) o `just --list` (dentro infra/).
# =====================================================================

set shell := ["bash", "-uc"]

# Subscription MPN - Mahiz (tenant mahiz.it).
sub-prod := "818db21a-4d0b-430a-a42a-a00715f0345f"

[private]
default: help

# Elenco dei comandi disponibili
help:
    @just --list

alias list := help

# Imposta la subscription Azure di Fantastiche e mostra il contesto
[group('azure')]
az-env:
    #!/usr/bin/env bash
    set -e
    echo ""
    echo "  → az account set --subscription {{sub-prod}}"
    az account set --subscription "{{sub-prod}}"
    echo ""
    echo "  Contesto attivo:"
    az account show --query "{subscription:name, id:id, tenant:tenantDefaultDomain, user:user.name}" -o yaml | sed 's/^/    /'
    echo "  Risorse prod:"
    echo "    Resource group : rg-fantastiche-prod"
    echo "    Key Vault      : kv-fantastiche-prod"
    echo "    Storage        : stfantasticheprod"
    echo "    ACR (condiviso): fantasticheacr.azurecr.io"
    echo ""

# Scrive/aggiorna un segreto in Key Vault (a domande, valore nascosto)
[group('azure')]
az-secrets:
    bash scripts/az-secrets.sh

# Anteprima delle modifiche della foundation (what-if), senza applicare
[group('azure')]
az-whatif operator-object-id:
    #!/usr/bin/env bash
    set -e
    read -rsp "  Password SQL admin (input nascosto, serve solo alla validazione): " pw
    echo ""
    az deployment sub what-if --location westeurope \
      --template-file main.bicep --parameters prod.bicepparam \
      --parameters operatorObjectId="{{operator-object-id}}" sqlAdministratorLoginPassword="$pw"
```

- [ ] **Step 2: `infra/scripts/az-secrets.sh`**

```bash
#!/usr/bin/env bash
# =====================================================================
# Scrive/aggiorna un segreto nel Key Vault Fantastiche, a domande.
#   - "modifica": elenca i segreti esistenti, scegli, inserisci il valore
#   - "nuovo":    inserisci nome + valore
# Il valore è inserito in input NASCOSTO e non viene mai stampato.
# Richiede di essere già sulla subscription giusta (vedi `just infra az-env`).
# Segreti attesi: ConnectionStrings--Fantastiche, Mailgun--ApiKey, Bootstrap--Password.
# =====================================================================
set -uo pipefail

VAULT="kv-fantastiche-prod"
echo "  Vault: ${VAULT}"
echo ""

echo "  Azione:"
echo "    1) modifica un segreto esistente"
echo "    2) nuovo segreto"
read -rp "  Scelta [1]: " action
action="${action:-1}"

name=""
case "${action}" in
  1)
    echo ""
    echo "  Segreti esistenti in ${VAULT}:"
    secrets=()
    while IFS= read -r line; do
      [ -n "${line}" ] && secrets+=("${line}")
    done < <(az keyvault secret list --vault-name "${VAULT}" --query "[].name" -o tsv)
    if [ "${#secrets[@]}" -eq 0 ]; then
      echo "  (nessun segreto trovato, o nessun accesso al vault)"
      exit 1
    fi
    i=1
    for s in "${secrets[@]}"; do
      echo "    ${i}) ${s}"
      i=$((i + 1))
    done
    read -rp "  Quale (numero o nome): " pick
    if [[ "${pick}" =~ ^[0-9]+$ ]]; then
      idx=$((pick - 1))
      name="${secrets[${idx}]:-}"
    else
      name="${pick}"
    fi
    ;;
  2)
    read -rp "  Nome segreto (es. Mailgun--ApiKey): " name
    ;;
  *)
    echo "  Scelta non valida: ${action}"
    exit 1
    ;;
esac

if [ -z "${name}" ]; then
  echo "  Nome segreto vuoto, annullo."
  exit 1
fi

read -rsp "  Valore per ${name} (input nascosto): " value
echo ""
if [ -z "${value}" ]; then
  echo "  Valore vuoto, annullo."
  exit 1
fi

if az keyvault secret set --vault-name "${VAULT}" --name "${name}" --value "${value}" -o none 2>/dev/null; then
  echo "  ✓ '${name}' impostato in ${VAULT}"
else
  echo "  ✗ errore su ${VAULT} (esiste? permessi? subscription giusta?)"
  exit 1
fi
```

Run: `chmod +x infra/scripts/az-secrets.sh`

- [ ] **Step 3: root `justfile`**

Sostituire le righe `mod be` / `mod fe` con:

```just
mod be
mod fe
mod infra
```

e aggiungere in coda, prima degli alias:

```just
# Imposta la subscription Azure di Fantastiche
az-env:
    @just infra az-env

# Scrive/aggiorna un segreto in Key Vault
az-secrets:
    @just infra az-secrets
```

- [ ] **Step 4: Verifica**

Run: `just --list && just infra --list && bash -n infra/scripts/az-secrets.sh && echo ok`
Expected: i comandi `az-env`, `az-secrets` compaiono nel root e in `infra`; `ok`.

- [ ] **Step 5: Commit**

```bash
git add infra/justfile infra/scripts/az-secrets.sh justfile
git commit -m "feat(infra): aggiunge i comandi just per subscription e segreti"
```

---

### Task 7: GitVersion e workflow GitHub Actions

**Files:**
- Create: `GitVersion.yml`
- Create: `.github/workflows/build-deploy.yml`

**Interfaces:**
- Consumes: parametri di `apps-be.bicep` e `apps-fe.bicep` (Task 5), immagini `be/Dockerfile`, `be/Dockerfile.scheduler`, `fe/Dockerfile` (Task 2).
- Produces: variabili di repository GitHub attese: `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` (documentate nel Task 8).

- [ ] **Step 1: `GitVersion.yml`**

```yaml
# Configurazione GitVersion per Fantastiche (monorepo, versione unica repo-wide).
# GitFlow: master = stabile, develop = -alpha (predisposto). Bump da conventional commits.
# Con lo squash merge di GitHub conta il titolo della PR; il suffisso "(#n)" non interferisce.
# I marcatori `+semver:` e `BREAKING CHANGE:` valgono solo a inizio riga.
workflow: GitFlow/v1
next-version: 0.1.0
major-version-bump-message: '^(\w+)(\([^)]*\))?!:|(?m:^BREAKING CHANGE:)|(?m:^\+semver:\s?(breaking|major))'
minor-version-bump-message: '^feat(\([^)]*\))?:|(?m:^\+semver:\s?(feature|minor))'
patch-version-bump-message: '^(fix|perf|refactor|chore|docs|test|build|ci|style)(\([^)]*\))?:|(?m:^\+semver:\s?(fix|patch))'
branches:
  main:
    label: ''
  develop:
    label: alpha
```

- [ ] **Step 2: `.github/workflows/build-deploy.yml`**

```yaml
# Pipeline unica di build & deploy su Azure Container Apps (ambiente prod).
#   version → build-be + build-fe (paralleli) → deploy-be (Bicep + job migrazione) → deploy-fe → release-tag
# Il foundation (infra/main.bicep) NON passa da qui: si applica a mano (vedi infra/README.md).
# Login Azure via OIDC: in GitHub solo variabili non segrete, nessun secret.
name: build-deploy

on:
  push:
    branches: [master]
    paths:
      - 'be/**'
      - 'fe/**'
      - 'infra/**'
      - '.github/workflows/**'
      - 'GitVersion.yml'
  workflow_dispatch:

# Due push ravvicinati si accodano: mai due deploy sovrapposti.
concurrency:
  group: deploy-prod
  cancel-in-progress: false

permissions:
  contents: write   # tag di release
  id-token: write   # OIDC verso Azure

env:
  ENV_NAME: prod
  RG: rg-fantastiche-prod
  ACR_NAME: fantasticheacr
  ACR_LOGIN_SERVER: fantasticheacr.azurecr.io
  KV_URI: https://kv-fantastiche-prod.vault.azure.net/
  BLOB_ENDPOINT: https://stfantasticheprod.blob.core.windows.net/
  DATAPROTECTION_KEY_ID: https://kv-fantastiche-prod.vault.azure.net/keys/dataprotection
  # Da compilare al provisioning: il deploy fallisce finché restano CHANGE_ME.
  MAILGUN_DOMAIN: CHANGE_ME
  MAILGUN_FROM: CHANGE_ME
  BOOTSTRAP_EMAIL: CHANGE_ME

jobs:
  version:
    name: compute version
    runs-on: ubuntu-latest
    outputs:
      semver: ${{ steps.gitversion.outputs.semVer }}
      mmp: ${{ steps.gitversion.outputs.majorMinorPatch }}
      sha7: ${{ steps.sha.outputs.sha7 }}
    steps:
      - uses: actions/checkout@v7.0.1
        with:
          fetch-depth: 0
      - uses: gittools/actions/gitversion/setup@v4.7.0
        with:
          versionSpec: '6.x'
      - id: gitversion
        uses: gittools/actions/gitversion/execute@v4.7.0
        with:
          useConfigFile: true
          configFilePath: GitVersion.yml
      - id: sha
        run: echo "sha7=${GITHUB_SHA::7}" >> "$GITHUB_OUTPUT"
      - run: echo "SemVer ${{ steps.gitversion.outputs.semVer }}"

  build-be:
    name: build api + scheduler
    needs: version
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v7.0.1
      - uses: azure/login@v3.1.0
        with:
          client-id: ${{ vars.AZURE_CLIENT_ID }}
          tenant-id: ${{ vars.AZURE_TENANT_ID }}
          subscription-id: ${{ vars.AZURE_SUBSCRIPTION_ID }}
      - name: api → ACR
        env:
          SEMVER: ${{ needs.version.outputs.semver }}
          SHA7: ${{ needs.version.outputs.sha7 }}
        run: |
          set -eo pipefail
          [ -n "$SEMVER" ] || { echo "semver vuota"; exit 1; }
          az acr build --registry "$ACR_NAME" \
            --image "fantastiche-api:$SEMVER" --image "fantastiche-api:$SHA7" --image "fantastiche-api:$ENV_NAME" \
            --file be/Dockerfile --build-arg "VERSION=$SEMVER" --build-arg "SHA=$SHA7" be
      - name: scheduler → ACR
        env:
          SEMVER: ${{ needs.version.outputs.semver }}
          SHA7: ${{ needs.version.outputs.sha7 }}
        run: |
          set -eo pipefail
          az acr build --registry "$ACR_NAME" \
            --image "fantastiche-scheduler:$SEMVER" --image "fantastiche-scheduler:$SHA7" --image "fantastiche-scheduler:$ENV_NAME" \
            --file be/Dockerfile.scheduler --build-arg "VERSION=$SEMVER" --build-arg "SHA=$SHA7" be

  build-fe:
    name: build frontend
    needs: version
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v7.0.1
      - uses: azure/login@v3.1.0
        with:
          client-id: ${{ vars.AZURE_CLIENT_ID }}
          tenant-id: ${{ vars.AZURE_TENANT_ID }}
          subscription-id: ${{ vars.AZURE_SUBSCRIPTION_ID }}
      - name: fe → ACR
        env:
          SEMVER: ${{ needs.version.outputs.semver }}
          SHA7: ${{ needs.version.outputs.sha7 }}
        run: |
          set -eo pipefail
          [ -n "$SEMVER" ] || { echo "semver vuota"; exit 1; }
          # VITE_API_BASE_URL vuoto: stessa origin, nginx inoltra /api e /hubs.
          az acr build --registry "$ACR_NAME" \
            --image "fantastiche-fe:$SEMVER" --image "fantastiche-fe:$SHA7" --image "fantastiche-fe:$ENV_NAME" \
            --file fe/Dockerfile --build-arg "VITE_API_BASE_URL=" --build-arg "VITE_APP_VERSION=$SEMVER" fe

  deploy-be:
    name: deploy backend
    needs: [version, build-be]
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v7.0.1
      - uses: azure/login@v3.1.0
        with:
          client-id: ${{ vars.AZURE_CLIENT_ID }}
          tenant-id: ${{ vars.AZURE_TENANT_ID }}
          subscription-id: ${{ vars.AZURE_SUBSCRIPTION_ID }}
      - name: apps-be.bicep
        env:
          SEMVER: ${{ needs.version.outputs.semver }}
        run: |
          set -eo pipefail
          for v in MAILGUN_DOMAIN MAILGUN_FROM BOOTSTRAP_EMAIL; do
            [ "${!v}" != "CHANGE_ME" ] || { echo "$v non configurato nel workflow"; exit 1; }
          done
          SUB=$(az account show --query id -o tsv)
          ENVID="/subscriptions/$SUB/resourceGroups/$RG/providers/Microsoft.App/managedEnvironments/cae-fantastiche-$ENV_NAME"
          MIID="/subscriptions/$SUB/resourceGroups/$RG/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id-fantastiche-$ENV_NAME"
          DOMAIN=$(az containerapp env show -n "cae-fantastiche-$ENV_NAME" -g "$RG" --query properties.defaultDomain -o tsv)
          FE_URL="https://fantastiche-fe-$ENV_NAME.$DOMAIN"
          az deployment group create \
            --name "apps-be-${GITHUB_RUN_ID}" \
            --resource-group "$RG" \
            --template-file infra/apps-be.bicep \
            --parameters environmentName="$ENV_NAME" environmentId="$ENVID" identityId="$MIID" \
                         acrLoginServer="$ACR_LOGIN_SERVER" kvUri="$KV_URI" \
                         apiImage="$ACR_LOGIN_SERVER/fantastiche-api:$SEMVER" \
                         schedulerImage="$ACR_LOGIN_SERVER/fantastiche-scheduler:$SEMVER" \
                         frontendBaseUrl="$FE_URL" blobEndpoint="$BLOB_ENDPOINT" \
                         dataProtectionKeyId="$DATAPROTECTION_KEY_ID" \
                         mailgunDomain="$MAILGUN_DOMAIN" mailgunFrom="$MAILGUN_FROM" \
                         bootstrapEmail="$BOOTSTRAP_EMAIL"
      - name: migrate database
        run: |
          set -eo pipefail
          JOB="fantastiche-migrate-$ENV_NAME"
          EXEC=$(az containerapp job start -n "$JOB" -g "$RG" --query name -o tsv)
          echo "Esecuzione $EXEC"
          for _ in $(seq 1 90); do
            STATUS=$(az containerapp job execution show -n "$JOB" -g "$RG" --job-execution-name "$EXEC" --query properties.status -o tsv)
            case "$STATUS" in
              Succeeded) echo "Migrazione completata"; exit 0 ;;
              Failed|Stopped) echo "Migrazione $STATUS"; az containerapp job logs show -n "$JOB" -g "$RG" --execution "$EXEC" --container "$JOB" || true; exit 1 ;;
            esac
            sleep 10
          done
          echo "Timeout in attesa della migrazione"; exit 1

  deploy-fe:
    name: deploy frontend
    needs: [version, deploy-be]
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v7.0.1
      - uses: azure/login@v3.1.0
        with:
          client-id: ${{ vars.AZURE_CLIENT_ID }}
          tenant-id: ${{ vars.AZURE_TENANT_ID }}
          subscription-id: ${{ vars.AZURE_SUBSCRIPTION_ID }}
      - name: apps-fe.bicep
        env:
          SEMVER: ${{ needs.version.outputs.semver }}
        run: |
          set -eo pipefail
          SUB=$(az account show --query id -o tsv)
          ENVID="/subscriptions/$SUB/resourceGroups/$RG/providers/Microsoft.App/managedEnvironments/cae-fantastiche-$ENV_NAME"
          MIID="/subscriptions/$SUB/resourceGroups/$RG/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id-fantastiche-$ENV_NAME"
          DOMAIN=$(az containerapp env show -n "cae-fantastiche-$ENV_NAME" -g "$RG" --query properties.defaultDomain -o tsv)
          az deployment group create \
            --name "apps-fe-${GITHUB_RUN_ID}" \
            --resource-group "$RG" \
            --template-file infra/apps-fe.bicep \
            --parameters environmentName="$ENV_NAME" environmentId="$ENVID" identityId="$MIID" \
                         acrLoginServer="$ACR_LOGIN_SERVER" \
                         feImage="$ACR_LOGIN_SERVER/fantastiche-fe:$SEMVER" \
                         apiUpstream="http://fantastiche-api-$ENV_NAME.internal.$DOMAIN"
          echo "FE: https://fantastiche-fe-$ENV_NAME.$DOMAIN"

  release-tag:
    name: release tag
    needs: [version, deploy-fe]
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v7.0.1
        with:
          fetch-depth: 0
      - name: tag v<major.minor.patch>
        env:
          MMP: ${{ needs.version.outputs.mmp }}
        run: |
          set -eo pipefail
          TAG="v$MMP"
          if git rev-parse -q --verify "refs/tags/$TAG" >/dev/null; then
            echo "Tag $TAG già esistente, skip."; exit 0
          fi
          git config user.email "build@fantastiche.local"
          git config user.name "GitHub Actions"
          git tag -a "$TAG" -m "Release $TAG"
          git push origin "$TAG"
          echo "Creato $TAG"
```

- [ ] **Step 3: Verifica**

Run: `command -v actionlint >/dev/null || brew install actionlint; actionlint .github/workflows/build-deploy.yml && echo ok`
Expected: `ok`, nessun errore. Se `shellcheck` segnala `${!v}` (indirezione bash), è voluto: la shell dei runner è bash.

- [ ] **Step 4: Commit**

```bash
git add GitVersion.yml .github/workflows/build-deploy.yml
git commit -m "ci: aggiunge la pipeline di build e deploy su Azure"
```

---

### Task 8: Documentazione operativa

**Files:**
- Modify: `infra/README.md` (riscrittura completa)
- Modify: `CLAUDE.md:9,23,35`
- Modify: `docs/workflow/git-workflow.md`
- Modify: `fe/docs/getting-started/development-setup.md`

Attenzione: `CLAUDE.md` e `fe/docs/getting-started/development-setup.md` hanno modifiche non committate dell'altro agente. Applicare solo le righe indicate con `Edit` puntuali e committare con `git add -p` selezionando soltanto gli hunk di questo task, oppure attendere che l'altro agente abbia committato. Non committare hunk altrui.

- [ ] **Step 1: `infra/README.md`**

Contenuto completo:

````markdown
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
6. Nel workflow `build-deploy.yml` sostituire `CHANGE_ME` in `MAILGUN_DOMAIN`, `MAILGUN_FROM`, `BOOTSTRAP_EMAIL` (non sono segreti) e committare.
7. Push su `master` (o `workflow_dispatch`): la pipeline builda le tre immagini, applica `apps-be.bicep`, esegue il job di migrazione, applica `apps-fe.bicep` e crea il tag `vX.Y.Z`.
8. Bootstrap SuperAdmin (una volta):

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

Il job di migrazione parte **dopo** l'aggiornamento delle app: la nuova API può avviarsi durante la migrazione. Le migrazioni EF sono idempotenti e l'API non tocca lo schema; se servirà, si separerà l'aggiornamento del job da quello delle app.

## Rete e cookie

FE e API su origin diverse romperebbero il cookie `SameSite=Lax`: `azurecontainerapps.io` è un suffisso pubblico. Perciò l'API ha ingress interno (`allowInsecure=true`, HTTP dentro l'environment) e nginx nel FE inoltra `/api/` e `/hubs/` (WebSocket) a `http://fantastiche-api-prod.internal.<defaultDomain>`. `Cors__AllowedOrigins__0` e `Invitations__PublicBaseUrl` valgono l'URL del FE.

Limite noto: `ReverseProxy__KnownProxies` accetta solo IP puntuali; l'API ignora gli header forwarded e il rate limiter partiziona per l'IP del proxy. Follow-up: supporto a `KnownNetworks` nel backend.

## DataProtection

API, Scheduler e job condividono l'anello di chiavi su `stfantasticheprod/dataprotection/keys.xml`, cifrato con la chiave `dataprotection` di `kv-fantastiche-prod`. Variabili: `DataProtection__BlobUri`, `DataProtection__KeyVaultKeyId`, `AZURE_CLIENT_ID` (obbligatorio con identità user-assigned, altrimenti l'endpoint MI risponde 400). In locale resta il file system.

## Repliche

API e FE `minReplicas=1` (nessun cold start durante un'asta), Scheduler sempre 1, API `maxReplicas=1` (SignalR in memoria, migrazione singola). Cambiare i parametri `apiMinReplicas`/`feMinReplicas` nel Bicep o nella pipeline, non con `az containerapp update`: ogni deploy riallinea Azure al Bicep.

## Accesso locale al DB

Connection string in `be/.env.prod` (gitignored) e regola firewall per il proprio IP (comando al punto 10). Non usare `docker compose down -v` sul DB locale pensando di agire su Azure: sono due mondi.

## Fuori scope

Staging, custom domain, Private Endpoint, SQL passwordless, identità per app, migrazione separata dall'avvio API, PWA.
````

- [ ] **Step 2: `CLAUDE.md`**

Riga 9, sostituire `PWA e deploy restano da realizzare.` con `Bicep, immagine FE nginx e pipeline GitHub Actions sono predisposti in [infra](infra/README.md); il provisioning Azure e la PWA restano da eseguire.`

Riga 23, sostituire `- \`infra\`: futura infrastruttura Bicep.` con `- [infra](infra/README.md): Bicep, moduli e procedura operativa per Azure Container Apps.`

Riga 35, sostituire `Bicep e trasferimento dati sono attività successive.` con `Bicep e pipeline sono in \`infra/\` e \`.github/workflows/\`; il trasferimento dati su Azure è una procedura operativa documentata in [infra](infra/README.md).`

- [ ] **Step 3: `docs/workflow/git-workflow.md`**

Aggiungere dopo la sezione «Modello di riferimento da attivare»:

```markdown
## Rilascio

Oggi `master` è l'unico branch di rilascio: ogni push che tocca `be/`, `fe/`, `infra/`, `.github/workflows/` o `GitVersion.yml` avvia `.github/workflows/build-deploy.yml`, che builda le immagini, applica i Bicep delle app, esegue la migrazione e crea il tag `vX.Y.Z` calcolato da GitVersion sui conventional commits. Con lo squash merge conta il titolo della PR. Il foundation Bicep non passa dalla pipeline: vedi [infra](../../infra/README.md).
```

- [ ] **Step 4: `fe/docs/getting-started/development-setup.md`**

Aggiungere in coda alla sezione «Verifiche e build»:

```markdown
### Immagine di produzione

`just fe build-image` costruisce `fantastiche-fe:local` (build SPA + nginx non root su 8080); `just fe run-image` la avvia su `http://localhost:8080` inoltrando `/api` e `/hubs` all'API locale 6060. La configurazione nginx è `fe/nginx/default.conf.template`: `API_UPSTREAM` è l'unica variabile resa a runtime. Su Azure la stessa immagine riceve l'FQDN interno dell'API dal Bicep `infra/apps-fe.bicep`.
```

- [ ] **Step 5: Verifica link**

Run: `for f in infra/README.md docs/workflow/git-workflow.md fe/docs/getting-started/development-setup.md CLAUDE.md; do grep -o '](\.\./[^)]*\|]([a-z][^):]*' "$f" | sed 's/](//' | while read -r p; do [ -e "$(dirname "$f")/$p" ] || echo "link rotto in $f: $p"; done; done; echo fine`
Expected: solo `fine`, nessun link rotto.

- [ ] **Step 6: Commit**

```bash
git add infra/README.md docs/workflow/git-workflow.md
git add -p CLAUDE.md fe/docs/getting-started/development-setup.md   # solo gli hunk di questo task
git commit -m "docs: documenta il deploy Azure e la procedura operativa"
```

---

## Verifica finale del piano

- [ ] `just be test` verde; `just be build` senza warning nuovi.
- [ ] `for f in infra/*.bicep infra/modules/*.bicep; do az bicep build --file "$f" --stdout > /dev/null || echo "ERRORE $f"; done` senza errori.
- [ ] `actionlint .github/workflows/build-deploy.yml` senza errori.
- [ ] `just fe build-image` completata e smoke del Task 2 riuscito.
- [ ] `git status` mostra solo le modifiche dell'altro agente non ancora committate; nessun file `.env`, `.local` o segreto aggiunto.
- [ ] Nessuna stringa `acksd`, `mahiz.dev`, `c3270a9c` nei file creati: `grep -rIl -i "acksd\|c3270a9c\|acksd.mahiz" infra .github GitVersion.yml fe/Dockerfile fe/nginx be/.dockerignore` vuoto.
