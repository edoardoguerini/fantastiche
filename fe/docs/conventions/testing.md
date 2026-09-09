# Test frontend

Convenzione di ACKS: unit/component test in __tests__ accanto al sorgente, nome <sorgente>.test.ts[x]. tests/ riservata a controlli trasversali/end-to-end. Il generatore route dovrà escludere __tests__.

Strumenti previsti: Vitest, Testing Library, Playwright. Installazione e scripts non ancora presenti.

Verifiche prioritarie: offerta in attesa/confermata/rifiutata, doppio clic, riconnessione, cambio lega, timer derivato dal server, importo libero e accessibilità del giro chiamanti. Integrare scenari end-to-end con più account e backend reale; un mock non prova la concorrenza SQL.

Testare comportamento e regressioni, senza imporre test che si limitano a ripetere la struttura di una modifica documentale.
