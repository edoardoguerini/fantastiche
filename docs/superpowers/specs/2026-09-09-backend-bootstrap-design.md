# Primo incremento backend

L’utente ha autorizzato l’esecuzione autonoma del backend il 9 settembre 2026. ACKSD (`/Users/edoardoguerini/Documents/Lavoro/ACKS/app`) è il riferimento strutturale. Frontend, catalogo, Blob e asta restano incrementi successivi.

## Decisioni operative

- .NET 10; progetti Core, Gateways, Infrastructure, Application e Scheduler, versioni NuGet centralizzate. SDK iniziale 10.0.202 disponibile sul Mac, patch di pacchetto verificate nel restore.
- Application espone moduli HTTP `/api` PascalCase con ApiResponse, validazione al boundary e dispatch IRequestPublisher; Infrastructure ospita use-case e configurazioni EF per feature. Dispatcher tramite DI, senza dipendenza mediator aggiuntiva.
- Identity con chiavi Guid UUIDv7, email normalizzata univoca. Cookie HttpOnly, SameSite=Lax, Secure fuori Development; antiforgery esplicito su tutte le mutazioni JSON, login compreso. API e SPA sullo stesso origin tramite proxy in sviluppo/ingresso in produzione; build e deploy restano separati. Sessione 8 ore senza rinnovo scorrevole, security stamp verificato. SignalR futuro usa lo stesso cookie con controllo Origin.
- SuperAdmin creato soltanto da comando locale esplicito con email/password da environment; nessuna password di default né reset automatico di account esistenti. Creazione lega limitata al ruolo globale.
- Creazione lega con stagione e organizzatore Pending invitato. Accettazione dell’invito organizzatore attiva il permesso senza imporre una squadra; inviti ai partecipanti richiedono nome squadra. Organizzatore già attivo può invitare sé stesso per partecipare.
- Account senza password predisposti tramite Identity; membership lega separata da team. LeagueSeason conserva budget 500 e rosa 3/8/8/6 configurabili alla creazione. Inviti associati anche alla stagione e al tipo (organizzatore/partecipante).
- Inviti 72 ore; reinvio revoca il token precedente e ne genera uno nuovo. GET pubblico non consuma e restituisce solo nome lega, scadenza e necessità di login. Token casuale 32 byte, hash SHA256; payload email cifrato con ASP.NET Data Protection, stesso application name e key ring per API/Scheduler. Produzione richiede percorso chiavi persistente e certificato di protezione; configurazioni locali ignorate da Git.
- Account attivo deve autenticarsi come destinatario; un invito non può reimpostarne la password. Accettazione in transazione serializable: password iniziale, membership, team e consumo atomici; replay restituisce stesso risultato senza nuovi effetti, con autenticazione del destinatario dopo consumo.
- Indici univoci su email, membership lega/utente, team stagione/nome normalizzato, TeamMember stagione/utente e token hash. Foreign key composte impediscono relazioni tra leghe/stagioni incoerenti. Conflitti SQL traducibili in HTTP 409.
- SQL Server reale in container Fantastiche dedicato (porta 14333), nessun accesso ai DB ACKSD. Su Apple Silicon emulazione linux/amd64 per sviluppo, non ambiente supportato da Microsoft per produzione.
- Scheduler BackgroundService + PeriodicTimer, coda persistita con lease atomico SQL, retry limitati e backoff. Coda e invito nella stessa transazione. Mailgun adapter con timeout; default mittente locale su file protetti gitignored, nessun invio esterno. Stati AcceptedByProvider/Failed/Cancelled distinti dalla consegna. Inviti revocati/scaduti non acquisiti per invio; delivery at-least-once, possibili duplicati dopo crash.

## Verifiche

Build, test unitari contratti/token, test HTTP con SQL Server reale: CSRF, auth, SuperAdmin, invito nuovo/esistente, revoca/scadenza/reinvio, replay, duplicati e isolamento leghe. Test worker: lease concorrenti, retry, cancellazione, payload protetto. Migrazione iniziale applicata e controllo assenza modifiche modello pendenti. Nessun test di asta dichiarato prima del relativo incremento.

## Fonti tecniche

- [CSRF ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0): antiforgery token verificato anche per JSON.
- [Supporto container SQL Server](https://learn.microsoft.com/en-us/sql/linux/sql-server-linux-docker-container-deployment?view=sql-server-ver17): supporto Linux x86-64, emulazione solo verifica locale.
- [Ciclo di supporto .NET](https://dotnet.microsoft.com/en-us/platform/support/policy).
