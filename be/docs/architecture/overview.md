# Architettura backend

Monolite modulare con host ASP.NET Core, backend separato dal frontend. Organizzazione identica al riferimento per nomi e responsabilità dei layer:

`Application → Infrastructure → Gateways → Core`, con riferimenti diretti a Core dove necessari.

Core espone contratti e tipi senza dipendere dagli altri progetti. Infrastructure contiene feature e handler. Gateways implementa gli adapter esterni. Application compone i servizi e traduce HTTP/SignalR in operazioni applicative.

Flusso: payload → validazione sintattica → identità e scope → command/query → handler → EF o Dapper → esito → risposta/notifica.

Gli Hub non eseguono SQL e non decidono l’aggiudicazione. I gateway non decidono budget o ruoli. Il database protegge gli invarianti concorrenti. Vedi [persistence](persistence.md).
