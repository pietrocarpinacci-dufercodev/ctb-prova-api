# ctb-prova-api

Piccola Web API .NET 10 per la prova end-to-end di claude-teams-bridge. Gestisce un elenco di cose da fare tenuto in memoria.

È il submodule `api/` del meta-repo [ctb-prova-meta](https://github.com/pietrocarpinacci-dufercodev/ctb-prova-meta).

## Endpoint

| Metodo | Percorso | Cosa fa |
| --- | --- | --- |
| GET | `/api/health` | stato del servizio |
| GET | `/api/todos` | elenco delle cose da fare, per id; con `?priority=bassa\|media\|alta` solo quelle con quella priorità, 400 se il valore non è valido |
| GET | `/api/todos/{id}` | una cosa da fare, 404 se non esiste |
| POST | `/api/todos` | crea una cosa da fare (`{ "title": "...", "priority": "alta" }`), 400 se il titolo è vuoto o la priorità non è valida; senza `priority` vale `media` |
| POST | `/api/todos/{id}/done` | segna una cosa come fatta |
| PUT | `/api/todos/{id}/priority` | cambia la priorità (`{ "priority": "bassa" }`), 404 se non esiste, 400 se la priorità manca o non è valida |

Ogni cosa da fare ha i campi `id`, `title`, `done` e `priority` (`bassa`, `media` o `alta`).

## Comandi

```bash
dotnet build
dotnet test
dotnet run --project src/CtbProva.Api
```

I dati stanno in memoria e si perdono al riavvio: è un progetto di prova.
