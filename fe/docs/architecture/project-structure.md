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

Routes compongono le feature; gli eventuali layout pathless _app e _public non determinano automaticamente SSR. La scelta iniziale è SPA (`spa.enabled` nel plugin Start), con shell statica in `dist/client/_shell.html`. `router.tsx` crea QueryClient e provider, senza caricare dati privati nella shell. Non creiamo src/server finché non viene deciso un ruolo server per Start.

Primitives non conoscono il dominio; common contiene UI trasversale, layout la shell. Lib/api gestisce HTTP; il client SignalR della sala vive in auctions/hooks/use-auction-live.ts perché sottoscrizione, versioni e recupero sono specifici dell’asta. Lib/realtime resta predisposta per un eventuale trasporto condiviso. Public contiene solo asset distribuibili, non importazioni del listone.
