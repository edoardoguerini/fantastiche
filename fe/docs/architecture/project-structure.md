# Struttura frontend

Struttura ripresa da ACKS; bootstrap eseguibile con auth, leagues e auctions implementate; catalog e teams restano predisposte come feature autonome.

```text
fe/
  src/
    routes/
    features/
      auth/
      leagues/
      catalog/
      teams/
      auctions/
    components/
      primitives/
      common/
      layout/
    lib/
      api/
      auth/
      realtime/
      utils/
    styles/
  public/
  tests/
  docs/
```

Ogni feature contiene components, actions, validations, types, hooks e una public API index.ts quando viene implementata. In actions: <feature>.queries.ts e <feature>.mutations.ts. In validations: schemi Zod. In types: DTO e tipi del contratto.

Routes compongono le feature; i layout pathless non determinano automaticamente SSR. TanStack Start rende le pagine sul server: `router.tsx` crea un QueryClient per richiesta e integra la hydration. `start.ts` configura serializzazione degli errori e header privati. Il resolver auth server-only resta co-locato nella feature; `server/index.mjs` è l’entry HTTP della build. Login e guard sono SSR; invito e sala hanno rendering client selettivo. Il documento HTML vive nel `shellComponent` del root, anche quando una route mostra un errore.

Primitives non conoscono il dominio; common contiene UI trasversale, layout la shell. Lib/api gestisce HTTP; il client SignalR della sala vive in auctions/hooks/use-auction-live.ts perché sottoscrizione, versioni e recupero sono specifici dell’asta. Lib/realtime resta predisposta per un eventuale trasporto condiviso. Public contiene solo asset distribuibili, non importazioni del listone.
