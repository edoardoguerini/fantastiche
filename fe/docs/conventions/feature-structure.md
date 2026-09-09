# Feature e naming

File kebab-case: auction-panel.tsx esporta AuctionPanel. Hook useAuctionState in use-auction-state.ts. Suffissi del ruolo: auction.queries.ts, auction.mutations.ts, auction.validations.ts, auction.types.ts.

Form con TanStack Form, validazione Zod in validations; tipi form derivati dallo schema. DTO rispecchiano i contratti backend. Niente any come scorciatoia per aggirare un contratto.

Componenti feature in components; sottocartelle per blocchi quando necessario. Public API index.ts espone solo il necessario. Utils nasce quando esiste un helper reale, non come contenitore generico di logica di dominio.

Messaggi UI in italiano e accessibili; non mostrare stack trace o SQL. Le scelte grafiche specifiche di ACKS non definiscono automaticamente il design Fantastiche.
