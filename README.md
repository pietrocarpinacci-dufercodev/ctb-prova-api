# ctb-prova-api

Piccola Web API .NET 10 per la prova end-to-end di claude-teams-bridge. Gestisce un elenco di cose da fare tenuto in memoria.

È il submodule `api/` del meta-repo [ctb-prova-meta](https://github.com/pietrocarpinacci-dufercodev/ctb-prova-meta).

## Endpoint

| Metodo | Percorso | Cosa fa |
| --- | --- | --- |
| GET | `/api/health` | stato del servizio |
| GET | `/api/todos` | elenco delle cose da fare, ordinato per id; filtro opzionale `?priority=bassa\|media\|alta` (400 se non valido) |
| GET | `/api/todos/{id}` | una cosa da fare, 404 se non esiste |
| POST | `/api/todos` | crea una cosa da fare (`{ "title": "...", "priority": "media" }`, `priority` opzionale, default `media`), 400 se il titolo è vuoto o la priorità non è valida |
| POST | `/api/todos/{id}/done` | segna una cosa come fatta |
| PUT | `/api/todos/{id}/priority` | cambia la priorità (`{ "priority": "alta" }`), 400 se non valida, 404 se non esiste |

La priorità vale `bassa`, `media` o `alta` (minuscolo, case-sensitive) ed è presente in ogni todo restituito.

## Comandi

```bash
dotnet build
dotnet test
dotnet run --project src/CtbProva.Api
```

I dati stanno in memoria e si perdono al riavvio: è un progetto di prova.
