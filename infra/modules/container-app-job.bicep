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
