# Aggiungere un’entità

Collocare il modello in Infrastructure/<Feature>/Domain e la configuration EF in Persistence. Definire chiave, nullabilità, lunghezze, foreign key e vincoli univoci prima della migration.

Scegliere se l’entità è globale, di lega o di LeagueSeason; documentare la catena di appartenenza. Non aggiungere TenantId o soft-delete universali copiandoli dal riferimento.

Le migrations EF sono l’unico percorso di modifica dello schema, anche per le tabelle lette/scritte da Dapper. Ispezionare la migration e testarla su SQL Server dedicato. Il comando di generazione verrà documentato dopo la creazione della solution.
