# Storage locale e immagini calciatori

Backend loghi aggiunto su richiesta dell’utente: `ClubMedia`, CLI `--import-club-media`, configurazione `Storage:ClubLogos:PublicBaseUrl` e `clubLogoUrl` nullable nei DTO catalogo/asta/rosa, dal ClubId dello snapshot. Import idempotente precedente al listone, senza cancellazione dei media assenti. Report backend in `.superpowers/sdd/2026-09-10-local-storage/backend-report.md`.

Incremento autorizzato in chat dopo il completamento della sala d’asta. L’utente richiede storage locale come ACKSD e tutte le immagini. Le verifiche hanno trovato il CSV originale con594URL, nessun download precedente. Applicare sviluppo con subagent-driven-development per backend media indipendente da provisioning/importatore locale e UI.

- [x] Azurite dedicato al progetto Fantastiche, porta10010 su loopback, volume persistente e `--location /data`, rete app, healthcheck. Credenziale locale generata in .env, nessun riuso di ACKSD.
- [x] Importatore locale ripetibile: legge CSV originale, scarica solo PNG da content.fantacalcio.it, verifica dimensione/formato, salva file e manifest fuori Git, carica nel solo container player-photos. Nessuna sovrascrittura di contenuti privati o modifica al CSV. Pubblico sui singoli blob locali, nessuna lista anonima.
- [x] Metadati PlayerMedia indicizzati per fonte/ID esterno, indipendenti dalla pubblicazione del listone. Import manifest via comando amministrativo locale .NET, nessuna nuova API di upload pubblico. API asta/catalogo/rosa con photoUrl nullable e configurazione base URL browser separata dal DNS Docker. Nessuna foto errata sui giocatori fittizi.
- [x] UI usa foto quando disponibili e segnaposto altrimenti. Test mirati su mapping, mancanze e URL locale; verifica lettura dal browser e persistenza ricreando solo Azurite, preservando il volume.
- [x] Documentazione, controlli pertinenti e review. Conservare gli account demo e sostituire il catalogo inventato e i suoi acquisti con il CSV reale, come richiesto successivamente; nessun deploy Azure o commit/push.

Decisione: le immagini sono collegate a FantacalcioCsv e all’ID esterno, non ai nomi. Il successivo messaggio dell’utente autorizza la sostituzione completa dei giocatori inventati anche nella demo. Conservare gli account e le squadre fantasy, ripristinare budget e avviare sessione con il catalogo reale. Scaricare inoltre i20stemmi dei club dagli URL verificati nel sito ufficiale e collegarli tramite nome normalizzato della fonte.
