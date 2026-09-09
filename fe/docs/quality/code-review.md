# Checklist review frontend

Review in sola lettura del diff e del contesto necessario.

- Struttura feature, naming e import boundaries coerenti.
- Primitives sotto components/primitives; nessuna regola divergente components/ui.
- API tipizzate e chiavi query con scope; cache e connessioni isolate al cambio lega/account.
- Route sottili e nessun segreto/codice server nel browser.
- Pending, rifiuto, doppio clic e riconnessione gestiti senza rilanci automatici.
- Prezzo e timer autorevoli sul server; nessuna assegnazione locale anticipata.
- PWA senza replay di offerte offline o reload durante l’asta.
- Form accessibili, errori comprensibili, drag & drop con alternativa e layout touch.
- Test pertinenti e risultati effettivamente osservati.

Report in italiano con severità, file/riga, scenario ed effetto; distinguere rilievi certi da verifiche mancanti. Non inventare bug né test eseguiti.
