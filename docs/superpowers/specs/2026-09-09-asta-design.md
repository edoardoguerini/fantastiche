# Fantastiche: specifica dell’asta

Decisioni concordate il 9 settembre 2026. Questa specifica descrive il primo nucleo dell’applicazione; non implica la creazione di risorse Azure.

## Obiettivo

Consentire ai partecipanti di una lega di fantacalcio di entrare con il proprio account nella stessa sessione d’asta e fare offerte in tempo reale. L’esperienza prende FantaLab come riferimento funzionale.

La web app deve adattarsi a telefono, tablet e desktop e poter essere aggiunta alla schermata Home senza pubblicazione negli store.

Modalità concordata: Classic, con ruoli P/D/C/A. Mantra escluso dalla prima versione.

## Stack

- Frontend: TanStack Start, React e TypeScript, con Vite e modalità SPA come impostazione iniziale proposta.
- Navigazione: TanStack Router; dati HTTP: TanStack Query.
- Interfaccia: Tailwind CSS e shadcn/ui.
- PWA: manifest e service worker da configurare e verificare nella build di Start.
- Backend separato: ASP.NET Core, con API HTTP e SignalR.
- Identità: ASP.NET Core Identity, con persistenza tramite Entity Framework Core.
- Email: Mailgun, come ACKSD, con coda persistita e invio asincrono tramite Scheduler.
- Persistenza: Azure SQL Database con modello DTU; Entity Framework Core per migrazioni e configurazione ordinaria, Dapper per query delle schermate e operazioni critiche dell’asta.
- Hosting: Azure; livello DTU e servizi di hosting specifici da dimensionare in fase di deployment mediante misure di carico.
- Infrastruttura come codice: Bicep, da predisporre in una fase successiva.
- Immagini giocatori: importazione dal CSV verso Azure Blob Storage, con associazione tramite ID del giocatore; trasferimento da eseguire quando l’ambiente sarà configurato.

Frontend e backend avranno build e deploy indipendenti. Il frontend non accederà direttamente al database. Tutte le regole dell’asta risiedono nel backend.

L’organizzazione delle cartelle e della documentazione riprende il progetto ACKS, adattata a Fantastiche: [decisione e differenze](../../decisions/0001-struttura-e-convenzioni.md). Backend in `be/`, frontend in `fe/`, infrastruttura in `infra/`; guide e istruzioni per Claude/Codex co-locate. Questa preparazione non costituisce ancora il bootstrap eseguibile.

## Accesso ai dati: EF Core e Dapper

Un unico schema Azure SQL viene gestito dalle migrazioni EF Core, incluse le tabelle utilizzate da Dapper. La tabella `__EFMigrationsHistory` registra le migrazioni applicate.

- EF Core gestisce la persistenza di ASP.NET Core Identity e le operazioni ordinarie di configurazione, come leghe e impostazioni.
- Dapper gestisce le query delle schermate con SQL esplicito e proiezioni dei soli campi necessari, oltre al percorso di scrittura critico dell’asta.
- Avvio, rilanci e aggiudicazioni usano transazioni SQL esplicite tramite Dapper. Tutte le istruzioni della singola operazione condividono connessione e transazione; le notifiche SignalR vengono emesse dopo il commit.
- Ogni operazione ha un unico percorso di scrittura. Non si modificano con Dapper entità che verranno poi salvate da un DbContext con uno stato non aggiornato.
- Le query SQL sono parametrizzate. L’accesso a dati privati verifica appartenenza e permessi della lega anche nei percorsi Dapper.
- Indici mirati, paginazione degli storici e test di carico su Azure SQL verificano prestazioni e consumo DTU; la scelta di Dapper da sola non garantisce questi risultati.

La fonte di identità è `AspNetUsers`, senza una seconda tabella utenti duplicata. `SuperAdmin` è un ruolo globale Identity ed è l’unico abilitato a creare leghe nella prima versione. I permessi per lega, incluso quello di organizzatore, sono conservati in `LeagueMembers`.

Le squadre fantacalcio sono `Teams`, le squadre reali `Clubs`. `TeamMembers` collega gli utenti alle squadre. Essere organizzatore ed essere giocatore sono indipendenti: lo stesso utente può avere entrambi i compiti nella stessa lega. Per rilanciare serve un’associazione valida alla squadra e alla lega stagionale dell’asta; il solo permesso di organizzatore non basta. Il chiamante è determinato dal turno, non da un ruolo Identity.

## Budget e rose

Budget e composizione della rosa sono configurabili per lega stagionale. Valori iniziali confermati: 500 crediti e 25 giocatori, suddivisi in 3 portieri, 8 difensori, 8 centrocampisti e 6 attaccanti. Le regole vengono salvate nella LeagueSeason, preservando le stagioni precedenti.

## Inviti e logo

L’organizzatore inserisce nome/email dei partecipanti: il server crea l’account senza password se nuovo, oppure riusa quello esistente, e predispone l’appartenenza Pending. Il link personale apre un form pubblico con nome/logo della lega, impostazione iniziale della password o login e scelta del nome squadra. L’accettazione attiva l’appartenenza e crea la squadra. Ogni lega ha un logo caricabile dall’organizzatore e conservato su Blob Storage. L’eventuale nuovo account, membership Pending, invito ed email vengono salvati nella stessa transazione; lo Scheduler invia tramite Mailgun. Il link contiene un token casuale specifico per invito, utente e lega, mentre la verifica usa l’hash salvato nel database. Dettaglio nella [guida inviti ed email](../../../be/docs/domains/invitations-email.md).

## Organizzazione e chiamate

L’organizzatore configura nelle impostazioni della sessione l’ordine dei chiamanti tramite drag & drop. Sono previsti anche comandi per spostare i partecipanti senza trascinamento.

Ogni partecipante chiama a turno. Dopo la conclusione di un giocatore si passa al successivo partecipante; al termine del giro si ricomincia.

Come comportamento iniziale proposto, l’organizzatore può mettere in pausa tra due giocatori e saltare il turno di un assente. Le modifiche all’ordine si applicano tra due aste, senza modificare il chiamante di un giocatore già in corso.

## Preparazione del giocatore

Solo il chiamante di turno può preparare e avviare l’asta. Seleziona:

- Un giocatore disponibile.
- La durata: 5, 10, 15, 20, 25 oppure 30 secondi.
- I pulsanti di incremento disponibili, per esempio +1, +5 e +10.

I pulsanti sono scorciatoie comuni a tutti. È sempre disponibile anche un campo per un’offerta totale libera, espressa in crediti interi.

Giocatore, durata e incrementi rimangono fissi dall’avvio fino all’aggiudicazione.

## Avvio, rilanci e aggiudicazione

Il comando «Avvia asta» registra un’offerta iniziale di 1 credito del chiamante e avvia il timer sul server. L’avvio richiede che il chiamante possa acquistare quel giocatore secondo i vincoli della lega.

Tutti i partecipanti, chiamante incluso, possono offrire. Ogni offerta valida accettata fa ripartire l’intera durata scelta. Le offerte rifiutate non modificano la scadenza.

Alla scadenza il server assegna il giocatore al miglior offerente, aggiorna rosa e crediti e avanza il turno. In assenza di rilanci il chiamante acquista il giocatore a 1 credito.

## Concorrenza e consistenza

Ogni richiesta di offerta contiene un identificativo univoco, il riferimento all’asta del singolo giocatore e l’importo totale proposto. L’identità deriva dalla sessione autenticata.

Esempio: prezzo visualizzato 20, clic su +5, offerta inviata 25. Un cambiamento del prezzo durante il transito non aumenta l’importo proposto. Se il prezzo corrente è già 25 o superiore, la richiesta viene rifiutata e richiede un nuovo gesto dell’utente.

Il backend serializza le modifiche della stessa asta e usa una transazione nel database per leggere e validare lo stato aggiornato, registrare l’offerta e modificare la scadenza. La correttezza non deve dipendere esclusivamente da un lock in memoria, che non proteggerebbe più istanze del backend.

- A parità di importo passa la prima offerta validamente elaborata dal server.
- La validazione verifica autorizzazione, asta aperta, importo intero superiore al prezzo corrente, compatibilità della rosa e budget, inclusa la riserva necessaria per completarla.
- I duplicati della stessa richiesta restituiscono lo stesso esito senza ulteriori effetti.
- Un’offerta è accettata solo se al controllo sul server la scadenza non è trascorsa; non fa fede l’orologio del dispositivo.
- La chiusura usa la stessa protezione transazionale dei rilanci e ricontrolla la scadenza aggiornata. Un vecchio timer non può chiudere un’asta prolungata da un rilancio.
- Aggiudicazione, addebito e aggiornamento della rosa avvengono una sola volta nella stessa transazione.

SignalR comunica gli esiti dopo il salvataggio. Gli aggiornamenti includono una versione crescente dello stato per riconoscere notifiche duplicate o obsolete. Il database è la fonte autorevole anche se una notifica non arriva.

## Interfaccia, riconnessione e PWA

Un’offerta resta in attesa finché il server non conferma l’esito. In caso di esito incerto si recupera lo stato o si verifica la stessa richiesta, senza generare un nuovo rilancio automatico.

Al rientro nell’app o alla riconnessione si recuperano stato, prezzo, miglior offerente, turno e scadenza dal backend prima di riabilitare i comandi. Il countdown visualizzato è derivato dalla scadenza del server; non determina la chiusura.

La cache PWA riguarda le risorse dell’interfaccia. I rilanci richiedono una connessione e non vengono accodati offline. Le nuove versioni non devono causare un ricaricamento automatico durante l’asta.

Un’interruzione del backend non cancella le offerte persistite. Alla ripartenza il backend riesamina le aste aperte e conclude quelle scadute con la stessa procedura idempotente.

## Verifiche di accettazione

- Accesso simultaneo di più account autorizzati alla stessa sessione.
- Preparazione e avvio consentiti solo al chiamante corrente.
- Aggiudicazione a 1 al chiamante in assenza di altre offerte.
- Rilanci tramite scorciatoie e importo libero; reset della durata solo dopo accettazione.
- Due offerte simultanee uguali, offerte diverse concorrenti e richieste duplicate senza doppio effetto.
- Concorrenza tra rilancio e chiusura, anche usando due istanze backend contro lo stesso database.
- Rifiuto delle offerte fuori tempo o incompatibili con budget e rosa.
- Recupero dopo perdita della connessione, conferma persa e riavvio backend.
- Passaggio di turno e ripartenza del giro.
- Installazione e uso su telefono e tablet, layout desktop e aggiornamento PWA senza interrompere un’asta.

## Confini della specifica

Questo documento fissa il nucleo della sessione d’asta. Prima di implementare i relativi moduli serviranno decisioni separate sul trasporto dell’autenticazione, durata e reinvio degli inviti, importazione del listone e gestione operativa delle squadre con più utenti. Non sono ancora requisiti concordati funzioni di annullamento acquisti, aste parallele nella stessa lega o gestione dell’intera stagione. Abbonamenti e pagamenti sono esclusi dalla prima versione e non bloccano la progettazione.
