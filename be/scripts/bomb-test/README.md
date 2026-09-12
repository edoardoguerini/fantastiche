# Runner locale Bomba

Simula le offerte degli altri sette account demo della lega «Gli ultimi del bar». Atletico Spritz / Luca Ferri è esclusa sia dalla selezione degli account sia dall’invio. Usa gli stessi handler e controlli di appartenenza del backend; non è un test del login HTTP o delle presenze SignalR. Non cambia password, privilegi, timer o importi nel database mediante SQL diretto.

Dal root:

```sh
# Compila e verifica account e stato, senza offerte.
python3 be/scripts/dev.py dotnet run --project scripts/bomb-test -- --check

# Prima di lanciare la Bomba nell’interfaccia:
python3 be/scripts/dev.py dotnet run --no-build --project scripts/bomb-test -- --watch
```

Attendere «ARMATO» prima di avviare la Bomba. Il runner controlla lo stato ogni 500 ms, aspetta `Collecting` e invia in parallelo le offerte delle squadre idonee che non hanno ancora confermato. Non anticipa la transizione Waiting → Collecting: i 60 secondi di attesa e quelli di raccolta appartengono al server.

Offerte iniziali in ordine alfabetico di squadra: 5, 10, 15, 20, 25, 30, 35 crediti. `--tie` porta le ultime due a 35 per provare uno spareggio. Nei turni successivi usa minimo corrente + indice squadra × 5; conserva sempre la riserva di budget prevista dalla rosa. L’offerta della squadra di Luca resta manuale.

Il runner si ferma alla conclusione/annullamento della Bomba selezionata e non partecipa a quella successiva. Scade dopo 30 minuti; Ctrl+C lo interrompe. Se una Bomba è già aperta, serve indicarla esplicitamente con `--watch --bomb-id UUID`, usando l’ID della Bomba, non quello del giocatore.

Ogni tentativo salva prima UUID e payload in `be/.local/bomb-test`, escluso da Git. Una risposta incerta viene recuperata mediante ricevuta, senza reinvio automatico. Riavviare con lo stesso `--bomb-id` conserva questi UUID e verifica le ricevute dei tentativi già presenti. Conservare il registro finché il test non è concluso.

Il runner rifiuta ambienti diversi da Development, database diversi da Fantastiche su `127.0.0.1,14333`, squadre/account non demo e una squadra esclusa che non corrisponda a Luca. Gli identificativi della lega di test sono espliciti in `Program.cs`; non è un comando di produzione. La modalità `--watch` genera vere offerte e può aggiudicare il calciatore nel database locale.
