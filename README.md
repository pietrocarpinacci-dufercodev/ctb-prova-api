# ctb-prova-api

Piccola Web API .NET 10 per la prova end-to-end di claude-teams-bridge. Gestisce un elenco di cose da fare tenuto in memoria.

È il submodule `api/` del meta-repo [ctb-prova-meta](https://github.com/pietrocarpinacci-dufercodev/ctb-prova-meta).

## Endpoint

| Metodo | Percorso | Cosa fa |
| --- | --- | --- |
| GET | `/api/health` | stato del servizio |
| GET | `/api/todos` | elenco delle cose da fare |
| GET | `/api/todos/{id}` | una cosa da fare (`id`, `title`, `done`, `priority`), 404 se non esiste |
| POST | `/api/todos` | crea una cosa da fare (`{ "title": "...", "priority": "high" }`). Tutti i campi devono esserci: `priority` è `high`, `medium`, `low` o `null`. 400 se un campo manca o non è valido |
| PATCH | `/api/todos/{id}` | aggiorna `title` e/o `priority`: un campo assente resta invariato, `"priority": null` la rimuove. 400 se un valore non è valido, 404 se non esiste |
| POST | `/api/todos/{id}/done` | segna una cosa come fatta |

## Comandi

```bash
dotnet build
dotnet test
dotnet run --project src/CtbProva.Api
```

I dati stanno in memoria e si perdono al riavvio: è un progetto di prova.
