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
