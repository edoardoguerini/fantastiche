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
  // L ingress dell API e interno: solo nginx e l ingress di Container Apps la raggiungono,
  // quindi gli header X-Forwarded-* sono affidabili. nginx riscrive X-Forwarded-For con la
  // sola voce fidata (IP del client); l ingress dell API puo aggiungere la propria. Limite 2:
  // con una o due voci si arriva sempre al client (rate limiter del login per IP).
  { name: 'ReverseProxy__TrustAllProxies', value: 'true' }
  { name: 'ReverseProxy__ForwardLimit', value: '2' }
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
    // Solo il FE e pubblico: nginx inoltra /api e /hubs all FQDN interno in HTTPS.
    // Niente HTTP: l ingress riscriverebbe X-Forwarded-Proto a http e l antiforgery
    // (cookie Secure) rifiuterebbe ogni POST.
    external: false
    allowInsecure: false
    targetPort: 8080
    minReplicas: apiMinReplicas
    // max 1: SignalR in memoria e nessuna migrazione concorrente.
    maxReplicas: 1
    secrets: [ connSecret, mailgunSecret ]
    // Solo liveness: /health/ready interroga il DB e al primo deploy lo schema non esiste ancora.
    probes: [
      {
        type: 'Liveness'
        httpGet: { path: '/health/live', port: 8080 }
        initialDelaySeconds: 15
        periodSeconds: 30
        failureThreshold: 3
      }
    ]
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
