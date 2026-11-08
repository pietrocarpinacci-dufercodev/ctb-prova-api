# ctb-prova-api

Piccola Web API .NET 10 per la prova end-to-end di claude-teams-bridge. Gestisce un elenco di cose da fare tenuto in memoria.

È il submodule `api/` del meta-repo [ctb-prova-meta](https://github.com/pietrocarpinacci-dufercodev/ctb-prova-meta).

## Endpoint

| Metodo | Percorso | Cosa fa |
| --- | --- | --- |
| GET | `/api/health` | stato del servizio |
| GET | `/api/todos` | elenco delle cose da fare, ordinato per id; filtro opzionale `?priority=bassa`, `media` o `alta` (400 se non valido) |
| GET | `/api/todos/{id}` | una cosa da fare, 404 se non esiste |
| POST | `/api/todos` | crea una cosa da fare (`{ "title": "...", "priority": "alta" }`), 400 se il titolo è vuoto o la priorità non è valida; `priority` è opzionale, default `media` |
| POST | `/api/todos/{id}/done` | segna una cosa come fatta |
| PATCH | `/api/todos/{id}/priority` | cambia la priorità (`{ "priority": "bassa" }`), 404 se non esiste, 400 se non valida |

Ogni cosa da fare ha il campo `priority` (`bassa`, `media`, `alta`): il confronto non distingue maiuscole e minuscole e il valore è salvato in minuscolo.

## Comandi

```bash
dotnet build
dotnet test
dotnet run --project src/CtbProva.Api
```

I dati stanno in memoria e si perdono al riavvio: è un progetto di prova.
