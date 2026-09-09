# Aggiungere un command

Creare <Feature>/<UseCase> con Command e Handler. Identità dal server, payload validato al boundary. Controllare invarianti e permessi nel caso d’uso.

Per le mutazioni d’asta, utilizzare Dapper con request ID idempotente e transazione esplicita, inclusi esito persistito e aggiornamenti. Nessuna chiamata a provider, Blob o SignalR dentro la transazione SQL.

Testare duplicati, concorrenza, rollback e appartenenza alla lega. Per configurazioni ordinarie utilizzare EF senza mescolare scritture tracciate e Dapper nella stessa operazione.
