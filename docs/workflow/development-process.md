# Processo di sviluppo

1. Leggere la specifica e le guide dello stack. Distinguere decisioni confermate da proposte.
2. Definire il comportamento concreto, i vincoli e la verifica; usare le autorizzazioni già date senza chiedere nuovamente conferma per ogni passaggio.
3. Cercare il pattern già presente prima di aggiungere infrastrutture o dipendenze.
4. Implementare una feature verticale: boundary, handler, persistenza, contratto e UI.
5. Eseguire i controlli pertinenti; per l’asta sono necessari test di concorrenza contro SQL Server reale.
6. Eseguire una review del diff usando [backend](../../be/docs/quality/code-review.md) e/o [frontend](../../fe/docs/quality/code-review.md). Può essere svolta direttamente o da un reviewer disponibile; nessuna dipendenza da plugin obbligatori.
7. Aggiornare documentazione e contratti, riportare risultati verificati e limiti.
8. Seguire il [workflow Git](git-workflow.md) per l’integrazione.

Il primo incremento backend dispone di build, test unitari/integrazione SQL Server, verifica migrazioni e Docker tramite just. Usare la guida setup e riportare l’esito reale; le verifiche backend non coprono frontend o asta ancora da implementare.
