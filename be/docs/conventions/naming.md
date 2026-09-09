# Naming backend

Codice, tabelle e campi in inglese; commenti e documentazione in italiano.

- Tipi, metodi e proprietà PascalCase; variabili/parametri camelCase; interfacce con I.
- File C# con il nome del tipo principale.
- Command `{Verb}{Entity}Command`, query `Get{Entity}Query` / `Search{Entities}Query`, handler con suffisso Handler.
- Request/Validator al boundary; Details/ListItem nei DTO.
- Namespace Infrastructure flat per feature; sottocartelle Domain/Persistence/UseCase organizzative.
- Tabelle al plurale e mapping espliciti.
- Route HTTP sotto /api con risorse PascalCase; parametri in camelCase.
- Configurazioni tipizzate; environment .NET con doppio underscore. Mai valori sensibili negli esempi.
