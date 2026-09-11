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
