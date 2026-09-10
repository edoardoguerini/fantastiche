# Inviti e partecipanti

La route pubblica `/invito` gestisce attivazione e adesione con le API onboarding esistenti. Il link email usa `#token=…`; la query `?token=…` è accettata per compatibilità e ripulita dalla barra degli indirizzi appena montata la pagina. I token nuovi devono continuare a viaggiare nel frammento, che non arriva al server del sito. Il token resta soltanto nello stato della pagina, mai in query key, storage o log; dopo un reload si riapre il link dall’email.

Anteprima tramite GET `/Invitations/Preview` con header `X-Invitation-Token`, cache HTTP disabilitata e `Referrer-Policy: no-referrer`. Conferma con POST `/Invitations/Accept`, antiforgery e token nel body. Il semplice accesso alla pagina non consuma l’invito.

Un account nuovo imposta password e conferma; il nome squadra è richiesto solo per un invito Participant. Un invito Organizer attiva il permesso di gestione senza creare la squadra. L’attivazione non emette una sessione: dopo il successo il login rimane nella pagina e conduce alla lega. Un account esistente accede nella pagina prima della conferma; l’account autenticato è mostrato e può essere cambiato senza spostare il token in un URL di ritorno. Il backend verifica che corrisponda al destinatario.

Scadenza, revoca, invito consumato, nome squadra occupato e rifiuti Identity restano esiti del server. In caso di conferma di rete incerta il form consente di verificare nuovamente l’invito; non ritenta automaticamente l’adesione. Se l’invito risulta consumato, l’utente può accedere alle proprie leghe. Il form non reimposta password esistenti.

`ParticipantsPanel` è nel dettaglio lega, sotto il listone. Legge GET `/Leagues/{leagueId}/Seasons/{seasonId}/Participants?page=1&pageSize=20`; cache isolata per utente/lega/stagione/pagina. Solo `canManage=true` restituito dal server mostra partecipanti, email degli inviti e form di gestione. Membri ordinari non ricevono i dati di gestione. Inviti paginati con stati In attesa, Accettato, Scaduto e Revocato; reinvio e revoca usano le API esistenti, con conferma esplicita della revoca. Le azioni non chiamano direttamente il provider email e la UI descrive l’accodamento, non la consegna.

Il client comune supporta GET con header aggiuntivi e PUT/POST con un nuovo antiforgery per ogni mutazione. `replaceSession` cancella le query private e la mutation cache mantenendo l’osservazione della sola identità, così login e cambio account funzionano anche senza cambiare route.

Test: `tests/e2e/invitations.spec.ts` usa esclusivamente API simulate; `auth.cache.test.ts` copre gli observer durante il cambio identità. Il test backend `LeagueParticipantsHttpTests` usa SQL temporaneo e verifica permessi dal database, isolamento, paginazione e revoca del SuperAdmin. Nessun invito a indirizzi reali è richiesto dalle verifiche.
