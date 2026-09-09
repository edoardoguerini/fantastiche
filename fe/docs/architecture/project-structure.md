# Struttura frontend

Struttura ripresa da ACKS; cartelle predisposte, file TypeScript ancora da implementare.

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

Routes compongono le feature; gli eventuali layout pathless _app e _public non determinano automaticamente SSR. La scelta iniziale proposta resta SPA. Non creiamo src/server finché non viene deciso un ruolo server per Start.

Primitives non conoscono il dominio; common contiene UI trasversale, layout la shell. Lib/api gestisce HTTP, lib/realtime il trasporto SignalR; interpretazione degli eventi e regole UI stanno in auctions. Public contiene solo asset distribuibili, non importazioni del listone.
