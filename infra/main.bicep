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
@description('Repository GitHub autorizzato via OIDC, nel formato del subject presentato da GitHub: owner@ownerId/repo@repoId.')
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
