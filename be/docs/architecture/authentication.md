# Sessioni e rinnovo

L’autenticazione usa ASP.NET Core Identity e il cookie `Fantastiche.Auth`, HttpOnly,
SameSite=Lax e Secure fuori Development. Il login crea una sessione persistente di
7 giorni: il browser può conservarla anche dopo la chiusura della PWA. Non esistono
JWT, refresh token separati o endpoint di refresh.

Le richieste autenticate rinnovano il cookie con la sliding expiration di Identity.
La verifica del security stamp, eseguita dopo 5 minuti dall’emissione sulle richieste
successive, ricostruisce le claim e rinnova anch’essa il ticket. Un cookie già scaduto
non può essere rinnovato: dopo 7 giorni senza rinnovi occorre accedere nuovamente.

`CookieSession` conserva la data iniziale nelle proprietà del ticket protetto con
Data Protection e impone un massimo di 30 giorni dal login, anche con uso continuo.
Il rinnovo limita a questa data la scadenza sia del ticket sia del cookie, affinché
anche SignalR, configurato con `CloseOnAuthenticationExpiration`, rispetti il limite.
Il trasporto realtime si riconnette usando il cookie corrente; i messaggi WebSocket
non emettono cookie. Le richieste HTTP, incluso `/api/Auth/Me`, gestiscono il rinnovo.

Date iniziali non valide, future o oltre il limite causano rifiuto e cancellazione
del cookie. La verifica standard Identity viene mantenuta: un security stamp
revocato impedisce il rinnovo. Il logout cancella il cookie del browser corrente.

I cookie emessi prima di questa modifica conservano come origine il proprio
`IssuedUtc`; al primo rinnovo tale data viene memorizzata senza azzerare il limite.
Restano non persistenti fino al prossimo login. Le nuove durate si applicano ai
cookie rinnovati; le sessioni già scadute richiedono il login.

Il frontend verifica `/api/Auth/Me` ogni minuto quando la pagina è attiva e al ritorno
del focus. Il resolver SSR inoltra i `Set-Cookie` Identity, inclusi i chunk, al browser.
Nessun refresh in background del service worker e nessuna persistenza di credenziali
in localStorage. Le mutazioni mantengono la protezione antiforgery esistente.

Verifiche: `AuthSessionHttpTests.cs` esercita login persistente, riapertura, rinnovi,
inattività, limite assoluto, data iniziale invalida, cookie precedenti e revoca con
SQL Server temporaneo e orologio controllato. I test HTTP esistenti coprono logout
e antiforgery; `auth.server.test.ts` verifica la propagazione SSR dei cookie rinnovati.
