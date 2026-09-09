# Aggiungere una feature backend

1. Leggere specifica e [struttura](../architecture/project-structure.md); scegliere il dominio proprietario.
2. Aggiungere la feature in Infrastructure con Domain, Persistence, Payloads e cartelle dei casi d’uso necessari.
3. Definire command/query e handler; scegliere EF o Dapper secondo [persistence](../architecture/persistence.md).
4. Aggiungere modulo HTTP in Application/Modules con Request e validazione, autorizzazione e descrizione OpenAPI.
5. Per il realtime aggiungere il messaggio nell’Hub e delegare al caso d’uso, evitando una seconda implementazione delle regole.
6. Verificare scope, errori e transazione; aggiornare il contratto frontend.
7. Testare il comportamento e applicare la [review](../quality/code-review.md).

I contratti e i registrar citati saranno disponibili solo dopo il bootstrap.
