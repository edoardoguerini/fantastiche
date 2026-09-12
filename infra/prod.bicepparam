using 'main.bicep'

param environmentName = 'prod'
param location = 'westeurope'
param sqlAdministratorLogin = 'fantasticheadmin'
// GitHub presenta il subject OIDC con gli id numerici di owner e repository
// (`repo:<owner>@<ownerId>/<repo>@<repoId>:ref:...`): senza gli id il login fallisce con AADSTS700213.
// Id ricavabili con `gh api users/<owner> --jq .id` e `gh api repos/<owner>/<repo> --jq .id`.
param githubRepository = 'edoardoguerini@51255814/fantastiche@1361413706'
param githubBranch = 'master'
// Valori reali passati da CLI a runtime (NON committare segreti):
//   --parameters sqlAdministratorLoginPassword=... operatorObjectId=...
param sqlAdministratorLoginPassword = ''
param operatorObjectId = ''
