# Validazione, errori e log

Validazione sintattica al boundary HTTP e SignalR; nel riferimento si usa FluentValidation. La libreria e la registrazione saranno predisposte al bootstrap, non sono già presenti.

Regole semantiche negli handler: permessi sulla lega, chiamante di turno, prezzo, budget e scadenza. Le verifiche sullo stato concorrente avvengono nella transazione, non solo in un validator precedente.

Errori di dominio con codici stabili, ad esempio `auction.closed` o `bid.too_low`. HTTP restituisce status coerenti e ApiResponse; SignalR un esito correlato al comando. Non esporre SQL o stack trace.

Logging strutturato tramite ILogger<T>, correlation ID e informazioni di decisione utili. Eventuali interceptor EF non registrano le modifiche Dapper: audit applicativo e registri transazionali devono coprire entrambi i percorsi. Niente password, token o credenziali nei log.
