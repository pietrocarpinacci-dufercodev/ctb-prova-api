# ctb-prova-api

Piccola Web API .NET 10 per la prova end-to-end di claude-teams-bridge. Gestisce un elenco di cose da fare tenuto in memoria.

È il submodule `api/` del meta-repo [ctb-prova-meta](https://github.com/pietrocarpinacci-dufercodev/ctb-prova-meta).

## Endpoint

| Metodo | Percorso | Cosa fa |
| --- | --- | --- |
| GET | `/api/health` | stato del servizio |
| GET | `/api/todos` | elenco delle cose da fare |
| GET | `/api/todos/{id}` | una cosa da fare, 404 se non esiste |
| POST | `/api/todos` | crea una cosa da fare (`{ "title": "..." }`), 400 se il titolo è vuoto |
| POST | `/api/todos/{id}/done` | segna una cosa come fatta |

## Comandi

```bash
dotnet build
dotnet test
dotnet run --project src/CtbProva.Api
```

I dati stanno in memoria e si perdono al riavvio: è un progetto di prova.

prova D55
prova D55 modificata su e2e-base
