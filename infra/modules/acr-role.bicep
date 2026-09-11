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
