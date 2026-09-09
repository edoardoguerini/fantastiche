# Isolamento lega/stagione

LeagueSeason unisce League e Season. Squadre e sessioni la referenziano; offerte e aste giocatore la raggiungono tramite le relazioni. Non serve duplicare LeagueId/SeasonId in ogni tabella.

Per ogni accesso verificare membership, permesso richiesto e appartenenza della risorsa. La squadra offerente deve appartenere alla medesima LeagueSeason dell’asta. Un utente membro di più leghe non può mescolarne i dati nella stessa operazione.

Con EF eventuali query filter sono difesa aggiuntiva. Con Dapper scope esplicito tramite filtro/join e parametri: nessuna protezione ereditata dal DbContext. Anche job e processi fuori HTTP devono avere un ambito esplicito.

Catalogo reale condiviso, proprietà dei giocatori per lega stagionale. Vincoli univoci e foreign key composte dove necessario impediscono associazioni incrociate.

Testare sempre accesso a risorsa di altra lega, ID di squadra estranea, cambio lega, ingresso a gruppo SignalR non autorizzato e accesso diretto ai command.
