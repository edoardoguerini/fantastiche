# Inviti e partecipanti

La route pubblica `/invito` gestisce attivazione e adesione con le API onboarding esistenti. Il link email usa `#token=…`; la query `?token=…` è accettata per compatibilità e ripulita dalla barra degli indirizzi appena montata la pagina. I token nuovi devono continuare a viaggiare nel frammento, che non arriva al server del sito. Il token resta soltanto nello stato della pagina, mai in query key, storage o log; dopo un reload si riapre il link dall’email.

Anteprima tramite GET `/Invitations/Preview` con header `X-Invitation-Token`, cache HTTP disabilitata e `Referrer-Policy: no-referrer`. Restituisce nome e logo della lega, nome di chi invita, email del destinatario mascherata, scadenza e requisiti del form. Conferma con POST `/Invitations/Accept`, antiforgery e token nel body; la risposta include email del destinatario e nome squadra. Il semplice accesso alla pagina non consuma l’invito.

## Struttura della pagina

La pagina condivide scena e pannello con la login (`login-page`, `login-panel`), con contenitore leggermente più largo (460 px) perché ospita la carta della lega e il form. L’intestazione mostra solo il logo della lega, senza il logo Fantastiche sopra il pannello. I componenti stanno in `features/invitations/components`:

- `InvitationHeader`: logo della lega o iniziali su fondo lilla scuro, nome, chip del ruolo (Partecipante/Organizzatore), scadenza assoluta in italiano e frase con chi invita.
- `InvitationSteps`: due passi (`Accedi` → `Conferma squadra`) usati solo quando il login è separato dalla conferma, cioè per un account già attivo. Lo stato corrente ha `aria-current="step"`.
- `AcceptInvitationForm`: campi con etichette visibili "Nome squadra" e "Password", senza titoli di sezione aggiuntivi; le legende dei gruppi restano disponibili agli screen reader. Include anteprima del nome e iniziali, toggle Mostra e checklist dei requisiti aggiornata a ogni tasto (`data-satisfied` per riga). Non esiste più il campo di conferma password. Le regole vivono in `validations/invitation.validations.ts` e alimentano sia la checklist sia lo schema Zod.
- Testi di esito e scadenza in `utils/invitation-copy.ts`: gli errori sono distinti dal codice del backend (`invitation.expired`, `invitation.revoked`, `invitation.consumed`, non trovato, rete) e propongono l’azione giusta (accedi, vai alle leghe, riprova).

## Percorsi

- Account nuovo: un solo form con squadra (se Participant) e password; il pulsante dice cosa succede ("Attiva account e partecipa" / "Attiva account e organizza"). Dopo il successo la pagina mostra "Sei in {lega}." con il nome squadra e la `LoginForm` con email precompilata e bloccata (`initialEmail`, `lockEmail`), focus sulla password. L’attivazione non emette una sessione: è una decisione di sicurezza, non un limite.
- Account esistente non collegato: passo 1 con l’email mascherata come suggerimento e login in pagina; dopo il login la stessa pagina passa al passo 2.
- Account esistente collegato: blocco "Stai accettando come {email}" con "Cambia account" inline, poi il form. Un 403 del server spiega che l’invito è per un altro destinatario e ripropone il cambio account accanto all’errore.
- Account collegato che apre un invito per un account nuovo: avviso con "Cambia account"; il form non compare.
- Scaduto, revocato, già accettato, link incompleto: esito a tutta larghezza con titolo, spiegazione e un solo collegamento (login o leghe). Gli errori di rete mostrano "Riprova".

Il backend verifica sempre che l’account corrisponda al destinatario. In caso di conferma di rete incerta il form consente di verificare nuovamente l’invito; non ritenta automaticamente l’adesione. Il form non reimposta password esistenti.

## Pannello organizzatore

`ParticipantsPanel` è nel dettaglio lega, sotto il listone. Legge GET `/Leagues/{leagueId}/Seasons/{seasonId}/Participants?page=1&pageSize=20`; cache isolata per utente/lega/stagione/pagina. Solo `canManage=true` restituito dal server mostra partecipanti, email degli inviti e form di gestione. Membri ordinari non ricevono i dati di gestione. Inviti paginati con stati In attesa, Accettato, Scaduto e Revocato; reinvio e revoca usano le API esistenti, con conferma esplicita della revoca. Le azioni non chiamano direttamente il provider email e la UI descrive l’accodamento, non la consegna.

Il client comune supporta GET con header aggiuntivi e PUT/POST con un nuovo antiforgery per ogni mutazione. `replaceSession` cancella le query private e la mutation cache mantenendo l’osservazione della sola identità, così login e cambio account funzionano anche senza cambiare route.

## Test

`tests/e2e/invitations.spec.ts` usa esclusivamente API simulate e copre account nuovo, account esistente con passi, account sbagliato, esiti scaduto/revocato, organizzatore senza squadra, password debole e layout a 320, 390 e 1280 px. I test componente in `components/__tests__` coprono form (checklist, anteprima, payload, 403) e pagina (carta lega, passi, identità, esito con email precompilata); `utils/__tests__` e `validations/__tests__` coprono testi, scadenza e regole. `auth.cache.test.ts` copre gli observer durante il cambio identità. Il test backend `LeagueParticipantsHttpTests` usa SQL temporaneo e verifica permessi dal database, isolamento, paginazione e revoca del SuperAdmin. Nessun invito a indirizzi reali è richiesto dalle verifiche.
