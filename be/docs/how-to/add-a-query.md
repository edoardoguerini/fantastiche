# Aggiungere una query

Creare Query e Handler nella cartella del caso d’uso; DTO locale o in Payloads se condiviso. Usare Dapper per le letture delle schermate.

Specificare scope, parametri e colonne; paginare gli storici e usare un ordinamento stabile. Niente query SQL costruite concatenando input utente, nemmeno per sort: campi ordinabili selezionati da un insieme consentito.

Nessuna scrittura nel query handler. Testare filtri, pagina vuota, ordinamento e isolamento tra leghe; misurare le query critiche su SQL Server.
