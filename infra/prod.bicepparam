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
