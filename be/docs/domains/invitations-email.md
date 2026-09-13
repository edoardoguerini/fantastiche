# Inviti, logo della lega ed email

## Decisioni concordate

Il SuperAdmin crea la lega e assegna l’organizzatore. L’organizzatore invita i partecipanti tramite email; al primo accesso la configurazione guidata propone impostazioni e inviti, che rimangono accessibili successivamente.

L’organizzatore inserisce nome ed email. Se l’email non corrisponde a un account esistente, il backend predispone un utente Identity senza password. Se esiste, riusa quell’utente senza modificarne credenziali o profilo. Crea l’appartenenza alla lega in stato Pending e un invito personale. Il flusso sostituisce la precedente proposta di registrazione autonoma dal form.

L’invitato apre il form pubblico dal link personale, con nome e logo della lega. Per un account nuovo imposta la password; per un account già attivo effettua il login con quello associato all’invito. Sceglie il nome della squadra e conferma l’adesione. Il server valida il token, scadenza, stato e associazione a utente/lega, attiva l’appartenenza, crea Team e TeamMember e consuma l’invito atomicamente. Anche l’impostazione iniziale della password deve essere coordinata con la transazione tramite gli store Identity. Un invito di lega non permette di reimpostare la password di un account già attivo.

Pending non concede accesso ai dati privati né diritto di rilancio. L’apertura del link, inclusa quella da parte di scanner email, non consuma l’invito: serve la conferma esplicita tramite un’operazione di scrittura.

Il form pubblico non espone rose, partecipanti o altri dati privati. Il logo è una proprietà della lega; l’organizzatore lo carica/modifica e il file viene conservato in Azure Blob Storage. In assenza del logo usare un segnaposto con le iniziali.

## Provider e invio asincrono

Provider scelto: **Mailgun**, come nel progetto ACKSD. Verifica del riferimento: `be/src/Acksd.Gateways/Mailgun/MailgunEmailSender.cs` implementa `IEmailSender`; `be/src/Acksd.Scheduler/Jobs/EmailDispatchJob.cs` delega a `IEmailDispatcher`.

L’API salva l’eventuale nuovo account, l’appartenenza Pending, l’invito ed EmailMessage nella stessa transazione EF Core, coordinata con gli store Identity. Non chiama Mailgun nella richiesta HTTP. Lo Scheduler preleva la coda persistita, invia tramite il gateway Mailgun e registra esito e tentativi, con retry degli errori temporanei.

- `LeagueInvitations`: lega, utente destinatario, email destinataria, mittente, hash del token, scadenza e stato di accettazione/revoca. Il token è casuale, unico per invito e non derivato dall’email; nell’email viaggia il token originale, non il suo hash.
- `EmailMessages`: invito di riferimento, destinatario, template/payload, stato invio, tentativi, prossimo tentativo e identificativo del messaggio del provider quando disponibile.
- Stati di recapito e accettazione distinti. Accettazione da parte del provider non dimostra consegna nella casella: non mostrarla come consegna verificata senza evidenza aggiuntiva.
- Inviti scaduti o revocati non vengono spediti dalla coda in ritardo; l’accettazione verifica comunque sempre lo stato corrente.
- Reinvio e revoca disponibili all’organizzatore. Durata implementata: 72 ore; il reinvio revoca il precedente invito e ne crea uno nuovo, mentre un invito già revocato richiede una nuova creazione. Ogni nuovo invito ha un token distinto, anche per lo stesso utente in leghe diverse.

## Affidabilità implementata nel primo incremento

Acquisizione atomica dei messaggi con lease o meccanismo equivalente, per evitare che due worker li spediscano contemporaneamente. Il sistema deve tollerare un crash dopo l’invio e prima della registrazione dell’esito: non assumere una garanzia exactly-once del provider. L’accettazione dell’invito è idempotente e non crea due squadre.

Conservare solo l’hash per verificare il token dell’invito. Poiché l’email asincrona deve contenere il link originale, il materiale sensibile necessario alla spedizione deve essere protetto nella coda, con una gestione delle chiavi definita prima dell’implementazione; niente token nei log.

Il link è una credenziale temporanea: per l’account nuovo la sua disponibilità abilita l’attivazione. Non si presume un’ulteriore verifica OTP concordata. Il riuso di un link consumato non deve ripetere impostazione password, creazione squadra o adesione. Vincoli univoci e transazioni devono gestire anche inviti concorrenti verso la stessa email normalizzata, senza duplicare account o membership.

## Organizzazione prevista

- Core/Email: contratti del mittente.
- Gateways/Mailgun: adapter provider.
- Infrastructure/Leagues: inviti e adesione.
- Infrastructure/Emails: coda, template e dispatcher.
- Scheduler/Jobs: job sottile che richiama il dispatcher.

Lo Scheduler usa BackgroundService e PeriodicTimer, senza Hangfire. Il timer d’asta non dipende dalla coda email né dai suoi intervalli di polling.

Dominio mittente, indirizzo From, regione del servizio e credenziali Fantastiche sono da configurare; nessuna credenziale o risorsa ACKSD viene riutilizzata implicitamente. Bicep predisporrà la configurazione Azure necessaria. Questa decisione non crea account Mailgun, non configura DNS e non invia email.

## Dettagli del primo incremento

L’invito iniziale di tipo Organizer attiva solo il permesso di gestione, senza imporre una squadra. L’organizzatore può successivamente invitare sé stesso come Participant per giocare. Il SuperAdmin può reinviare l’invito iniziale prima che esista un organizzatore attivo.

Il payload email è protetto con ASP.NET Data Protection; il token è conservato come hash SHA256 per la verifica. Il link usa `/invito#token=...`: il frammento non viene inviato dal browser al server. L’anteprima API legge `X-Invitation-Token` e disabilita la cache; l’accettazione riceve il token nel JSON con antiforgery. Il form frontend gestisce attivazione, login in pagina e adesione; [dettagli](../../../fe/docs/architecture/invitations.md).

GET `/api/Leagues/{leagueId}/Seasons/{leagueSeasonId}/Participants` restituisce `{leagueId, leagueSeasonId, canManage, participants, invitations}`. Membri attivi ordinari ricevono `canManage=false` e liste vuote. Organizzatori attivi e SuperAdmin verificati nel database ricevono partecipanti attivi della stagione (inclusi gli organizzatori senza squadra) e inviti della stessa lega/stagione; nessun token/hash/payload email è esposto. `participants` contiene `{userId, displayName, teamName, isOrganizer}`; `invitations` è `LeaguePage` con `{id, displayName, email, kind, status, expiresAt}`. Stati `Pending`, `Accepted`, `Revoked`, `Expired`, ordinamento creazione/id decrescente; default `page=1`, `pageSize=20`, limiti 1–10000 e 1–100. Le scritture invito/reinvio/revoca verificano ora il ruolo SuperAdmin nel database anche con cookie già emesso.

Il replay di un invito già consumato richiede l’account destinatario autenticato e restituisce il risultato salvato. Il link consumato da solo non permette di recuperare informazioni private. Tutte le mutazioni onboarding usano transazione serializable e un lock SQL applicativo dedicato; l’accettazione iniziale coordina Identity e dominio nello stesso DbContext.

## Anteprima, esito ed email

GET `/api/Invitations/Preview` restituisce `{leagueName, leagueLogoUrl, invitedBy, recipientEmailHint, expiresAt, requiresLogin, requiresTeam}`. `leagueLogoUrl` è l’URL pubblico del logo (null senza logo o senza `Storage:LeagueLogos:PublicBaseUrl`); `invitedBy` è il DisplayName di chi ha inviato l’invito, con fallback `Fantastiche` se l’account non esiste più. `recipientEmailHint` è l’email del destinatario mascherata da `EmailMasking.Mask`: primo carattere della parte locale, `•••`, ultimo carattere solo se la parte locale ha almeno tre caratteri, poi il dominio intero (`edoardo@mahiz.it` → `e•••o@mahiz.it`, `ab@x.it` → `a•••@x.it`). L’anteprima resta pubblica e non consuma l’invito, quindi non espone mai l’indirizzo completo.

POST `/api/Invitations/Accept` restituisce `{leagueId, leagueSeasonId, teamId, email, teamName}`: `email` è l’indirizzo dell’account attivato, `teamName` il nome della squadra creata (null per l’organizzatore). Il replay legge il nome da `Teams` tramite `AcceptedTeamId`.

L’email è generata da `InvitationEmailTemplate.Render`, funzione pura in `Infrastructure/Emails`, chiamata da `QueueInvitationAsync` dentro la transazione con nome stagione, budget e nome del mittente. Oggetto: `{Mittente} ti ha invitato a {Lega}` per i partecipanti, `Organizza {Lega} su Fantastiche` per l’organizzatore; testi al genere neutro. HTML a tabelle con stili inline: sfondo esterno viola `#1a1125` pieno come fallback e `linear-gradient` sui client che lo supportano, card scura con bordo minimale, riquadro lega con nome e stagione, più `<img>` tondo del logo solo se esiste un URL pubblico (l’`alt` usa le iniziali calcolate con la stessa regola del frontend `fe/src/lib/utils/initials.ts`); senza logo nessun badge, bottone lilla, link in chiaro e nota per chi non attendeva l’email. La testata mostra lo stemma della piattaforma (`fe/public/brand/fantastiche-logo-email.png`, 112 px per 56 px CSS, circa 5 KB) come immagine remota sulla stessa origin di `Invitations:PublicBaseUrl`; l’`alt` "Fantastiche" resta il testo quando il client blocca le immagini, e senza URL il template stampa il nome in testo. Il tema è scuro per scelta: `<head>` dichiara `color-scheme: light` (meta e `:root`) per dire ai client di non ricolorare, e un blocco `<style>` riafferma la palette con `!important` sulle classi `fx-*` sia dentro `@media (prefers-color-scheme: dark)` sia con i selettori `[data-ogsb]`/`[data-ogsc]` che Outlook (web e app mobile) aggiunge quando altera i colori. Il margine chiaro attorno al messaggio in Outlook mobile è il padding del client e non è controllabile dall’email. La scadenza è una data assoluta in italiano nel fuso `Europe/Rome` (`sabato 12 settembre alle 18:30`), con fallback UTC e suffisso esplicito ` UTC` se il fuso non è disponibile nel sistema. Il testo semplice riporta le stesse informazioni e termina con il link: i test lo estraggono con `#token=([0-9a-fA-F]{64})`.

La coda acquisisce ogni messaggio in transazione READ COMMITTED esplicita con hint compatibili con RCSI. Lease 2 minuti, timeout mittente 30 secondi, massimo 5 tentativi, backoff 60/120/240/480 secondi. Un errore SQL dopo l’invio lascia il lease recuperabile. La revoca concorrente all’ultima fase della chiamata al provider non può annullare una spedizione già in corso, ma invalida sempre l’accettazione del link.

[Setup e configurazione chiavi](../getting-started/development-setup.md). [Contratto Mailgun HTTP](https://documentation.mailgun.com/docs/mailgun/user-manual/sending-messages/send-http).

## Caricamento logo durante la creazione

POST `/api/Leagues` accetta il campo JSON opzionale `logo` (base64). Limite di 2 MiB dopo decodifica; i formati PNG, JPEG e WebP sono riconosciuti dalle firme del contenuto, senza fidarsi del nome o del MIME del browser. SVG e file vuoti vengono rifiutati. Non viene eseguita una decodifica o ricompressione dell’immagine sul server.

Il ruolo SuperAdmin viene verificato nel database. `ILeagueLogoStore` è il contratto Core e `AzureLeagueLogoStore` il gateway Blob; i nomi sono generati dal server come `{leagueId}/{randomId}.{extension}`, con MIME coerente e cache immutabile. Il blob viene salvato durante la transazione di creazione, prima del template email. Se upload o accodamento falliscono, SQL esegue rollback e il backend tenta di eliminare il blob con un timeout separato. Se il commit è iniziato ma il suo esito è incerto, il blob viene conservato per evitare di rompere una lega effettivamente salvata; eventuali blob orfani richiedono riconciliazione. Nessun token o payload viene scritto nei log.

In locale `Storage__LeagueLogos__ConnectionString` punta ad Azurite (configurato da `scripts/dev.py` e Docker watch); `Storage__LeagueLogos__PublicBaseUrl` usa l’URL raggiungibile dal browser. In Azure il gateway usa Managed Identity e il container pubblico per singolo blob `league-logos`, predisposto dal foundation Bicep. La API richiede `Storage__LeagueLogos__PublicBaseUrl`, aggiunto anche allo Scheduler per gli URL dei template. Nessuna migrazione SQL aggiuntiva.
