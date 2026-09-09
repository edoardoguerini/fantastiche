# Scheduler

Host .NET con BackgroundService/PeriodicTimer: ogni 5 secondi elabora fino a 20 messaggi con lease SQL di 2 minuti e timeout mittente di 30 secondi. Invio tramite Core/Email e Gateways/Mailgun; default locale su file. Cinque tentativi massimi e backoff esponenziale. Nessun timer d’asta in questo host. Setup e comandi in [guida backend](../../docs/getting-started/development-setup.md).
