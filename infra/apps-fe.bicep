@description('Deploy del Container App frontend (nginx + Node SSR) nel RG dell ambiente. Deploy Incremental: non tocca BE/Scheduler.')
param location string = resourceGroup().location
param environmentName string = 'prod'
param environmentId string
param identityId string
param acrLoginServer string
param feImage string
@description('URL interno dell API usato dal proxy nginx e dalla verifica sessione SSR, es. https://fantastiche-api-prod.internal.<defaultDomain> (HTTPS: l ingress interno riscrive X-Forwarded-Proto con lo schema usato da nginx).')
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
