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
@description('Probe del container (array di oggetti probes di Container Apps). Vuoto = solo il probe TCP di default.')
param probes array = []

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
          probes: probes
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
