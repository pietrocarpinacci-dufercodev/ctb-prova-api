# ctb-prova-api

Piccola Web API .NET 10 per la prova end-to-end di claude-teams-bridge. Gestisce un elenco di cose da fare tenuto in memoria.

È il submodule `api/` del meta-repo [ctb-prova-meta](https://github.com/pietrocarpinacci-dufercodev/ctb-prova-meta).

## Endpoint

| Metodo | Percorso | Cosa fa |
| --- | --- | --- |
| GET | `/api/health` | stato del servizio |
| GET | `/api/todos` | elenco delle cose da fare ordinato per id; `?priority=low\|medium\|high` filtra per priorità, 400 se il valore non è ammesso |
| GET | `/api/todos/{id}` | una cosa da fare, 404 se non esiste |
| POST | `/api/todos` | crea una cosa da fare (`{ "title": "...", "priority": "low\|medium\|high" }`, `priority` facoltativo, default `medium`), 400 se il titolo è vuoto o la priorità non è ammessa |
| POST | `/api/todos/{id}/done` | segna una cosa come fatta |
| PUT | `/api/todos/{id}/priority` | cambia la priorità (`{ "priority": "high" }`), 400 se mancante o non ammessa, 404 se la cosa non esiste |

## Comandi

```bash
dotnet build
dotnet test
dotnet run --project src/CtbProva.Api
```

I dati stanno in memoria e si perdono al riavvio: è un progetto di prova.

## Prova ripubblicazione

18 novembre 2026: Commit fatto prima dell'interruzione del runner.

Commit fatto prima dell'interruzione del runner (secondo tentativo).

Ripubblicazione verificata.
