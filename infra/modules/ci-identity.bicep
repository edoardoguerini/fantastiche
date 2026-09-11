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
