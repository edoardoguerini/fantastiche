# Logo della lega

Il form di creazione lega include una dropzone facoltativa nella sezione «La lega»: trascinamento o scelta da file, anteprima, sostituzione e rimozione prima dell’invio. Il pulsante di scelta è utilizzabile da tastiera; il file input resta disponibile al selettore nativo. Sono accettati PNG, JPEG e WebP non vuoti fino a 2 MiB. Errori di formato o dimensione bloccano l’invio senza cancellare gli altri campi. Gli URL temporanei dell’anteprima vengono revocati alla sostituzione o allo smontaggio.

`LeagueLogoDropzone` e il CSS sono nella feature `leagues`; la validazione e la lettura del file sono in `utils/league-logo-file.ts`. La mutation include `logo` come base64 nel JSON esistente di POST `/Leagues` solo quando è selezionata un’immagine. La creazione senza logo mantiene lo stesso contratto. Nessun upload parte alla selezione del file.

Il backend verifica nuovamente formato, dimensione e ruolo SuperAdmin nel database; salva il blob prima di accodare l’invito, così il logo è già presente nell’email. Un errore dello storage annulla la transazione della lega e dell’eventuale nuovo account. La risposta di creazione restituisce `logoUrl`, già utilizzato da elenco, dettaglio e inviti. L’aggiunta o sostituzione del logo di una lega già creata resta fuori da questo incremento.

Verifiche browser in `tests/e2e/create-league.spec.ts`: scelta, trascinamento, tastiera, anteprima, rimozione, file non ammessi, limite di dimensione, payload e layout mobile. I test HTTP backend verificano upload/lettura su Azurite e logo nell’email, rollback per errore storage e rifiuto dei file invalidi; usano database temporanei e rimuovono i blob di prova.
