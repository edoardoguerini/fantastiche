# Test frontend

Convenzione di ACKS: unit/component test in **tests** accanto al sorgente, nome <sorgente>.test.ts[x]. tests/ riservata a controlli trasversali/end-to-end. Il generatore route esclude **tests**.

Strumenti installati: Vitest, Testing Library, Playwright. `just fe test` esegue unit/component test; `just fe test-e2e` avvia Vite se necessario e usa Chrome locale. I test browser intercettano soltanto le API sulla porta 6060; non provano SQL o la concorrenza backend. Il backend ha test HTTP/SQL reali separati.

Verifiche prioritarie: offerta in attesa/confermata/rifiutata, doppio clic, riconnessione, cambio lega, timer derivato dal server, importo libero e accessibilità del giro chiamanti. Integrare scenari end-to-end con più account e backend reale; un mock non prova la concorrenza SQL.

Testare comportamento e regressioni, senza imporre test che si limitano a ripetere la struttura di una modifica documentale.
