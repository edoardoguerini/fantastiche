# Setup frontend

Il frontend non è ancora eseguibile: mancano package.json, lockfile e configurazioni.

Bootstrap previsto: TanStack Start con Vite e React/TypeScript, pnpm, TanStack Query, Tailwind e primitives shadcn. Forms con TanStack Form/Zod; TanStack Table solo per tabelle che ne richiedono le funzioni.

Verificare versioni compatibili e fissare packageManager/lockfile. Predisporre scripts per build, typecheck, lint e test; aggiornare questa guida con i comandi verificati. Riprendere l’orchestrazione Docker/just del riferimento quando implementata.

Le variabili VITE sono pubbliche nel bundle: nessuna chiave API, SQL o Blob al loro interno. URL backend e modalità di autenticazione vanno configurati separatamente per ambiente. Predisporre PWA e test su build di produzione; il solo dev server non ne prova il funzionamento.
