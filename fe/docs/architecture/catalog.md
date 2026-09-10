# Catalogo e listoni

La route `/catalogo` è riservata al SuperAdmin e si raggiunge da “Gestisci listoni” nel dettaglio lega. Consente di caricare il CSV Fantacalcio originale a 19 colonne senza intestazione, indicare la stagione e importare una bozza. L’anteprima mostra calciatori, ruolo, club, card e stemmi quando disponibili, con filtri e paginazione. La pubblicazione richiede una conferma esplicita separata dall’importazione.

`LeagueCatalogPanel` nel dettaglio lega permette a tutti i membri autorizzati di consultare il listone adottato. L’organizzatore può scegliere una versione pubblicata della stessa stagione quando non ne è ancora stata assegnata una. La scelta è fissa per la stagione, secondo la regola del backend; il form la descrive prima della conferma. I partecipanti non ricevono controlli di assegnazione.

Le query della feature `catalog` sono separate per utente e risorsa. Le mutazioni passano dal client HTTP comune con antiforgery, blocco dei doppi invii, cancellazione al cambio account e nessun retry automatico. I permessi sono verificati nel database anche quando un cookie contiene ancora un ruolo SuperAdmin revocato. Le bozze restano riservate, mentre le versioni pubblicate sono consultabili dagli utenti autenticati.

Le immagini sono URL restituiti dalle API e serviti dallo storage locale, con segnaposto in caso di assenza o errore. L’importazione CSV non scarica immagini automaticamente: i comandi dedicati sono nella [guida storage](../../../be/docs/getting-started/local-storage.md). I metadati già registrati vengono associati al catalogo tramite fonte/ID del calciatore e fonte/nome normalizzato del club.

Test componenti in `src/features/catalog/components/__tests__`, browser in `tests/e2e/catalog.spec.ts`; le prove UI automatiche usano API simulate e non pubblicano listoni nel database applicativo.
