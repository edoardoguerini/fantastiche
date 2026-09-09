# Aggiungere una feature frontend

1. Leggere la specifica e stabilire il contratto HTTP/SignalR con il backend.
2. Creare components, actions, validations, types e hooks necessari nella feature.
3. Definire query key con scope e stati loading/error/empty; form e validazioni espliciti.
4. Esporre la public API in index.ts e comporre la route sottile.
5. Per auctions gestire conferma, versione eventi e recupero snapshot; non applicare aggiornamenti ottimistici al saldo come se fossero già accettati.
6. Verificare touch, tastiera, tablet e desktop.
7. Eseguire i test pertinenti e la [review](../quality/code-review.md).

Non copiare server functions o asset premium dal riferimento per far funzionare una feature browser.
