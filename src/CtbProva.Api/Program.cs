using System.Collections.Concurrent;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<TodoStore>();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseCors();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/todos", (TodoStore store) => store.All());

app.MapGet("/api/todos/{id:int}", (int id, TodoStore store) =>
    store.Find(id) is { } todo ? Results.Ok(todo) : Results.NotFound());

// Il corpo è JsonElement: serve distinguere campo assente da null (in POST l'assenza è un errore, in PATCH significa "invariato").
app.MapPost("/api/todos", (JsonElement body, TodoStore store) =>
{
    var errors = TodoInput.Validate(body, requireAll: true, out var title, out var priority);
    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    var todo = store.Add(title!, priority);
    return Results.Created($"/api/todos/{todo.Id}", todo);
});

app.MapPatch("/api/todos/{id:int}", (int id, JsonElement body, TodoStore store) =>
{
    var errors = TodoInput.Validate(body, requireAll: false, out var title, out var priority);
    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    var hasPriority = body.TryGetProperty("priority", out _);
    return store.Update(id, title, hasPriority, priority) is { } todo ? Results.Ok(todo) : Results.NotFound();
});

app.MapPost("/api/todos/{id:int}/done", (int id, TodoStore store) =>
    store.MarkDone(id) is { } todo ? Results.Ok(todo) : Results.NotFound());

app.Run();

public record Todo(int Id, string Title, bool Done, string? Priority);

public static class TodoInput
{
    private static readonly string[] Priorities = ["high", "medium", "low"];

    // Con requireAll tutti i campi devono esserci (POST); altrimenti quelli assenti non sono validati né applicati (PATCH).
    // title: mai null né vuoto. priority: uno dei tre valori oppure null.
    public static Dictionary<string, string[]> Validate(JsonElement body, bool requireAll, out string? title, out string? priority)
    {
        var errors = new Dictionary<string, string[]>();
        title = null;
        priority = null;

        if (body.ValueKind != JsonValueKind.Object)
        {
            errors["body"] = ["Il corpo deve essere un oggetto JSON."];
            return errors;
        }

        if (body.TryGetProperty("title", out var t))
        {
            title = t.ValueKind == JsonValueKind.String ? t.GetString()?.Trim() : null;
            if (string.IsNullOrEmpty(title))
            {
                errors["title"] = ["Il titolo è obbligatorio."];
            }
        }
        else if (requireAll)
        {
            errors["title"] = ["Il titolo è obbligatorio."];
        }

        if (body.TryGetProperty("priority", out var p))
        {
            priority = p.ValueKind == JsonValueKind.String ? p.GetString() : null;
            if (p.ValueKind != JsonValueKind.Null && !Priorities.Contains(priority))
            {
                errors["priority"] = ["La priorità deve essere high, medium, low o null."];
            }
        }
        else if (requireAll)
        {
            errors["priority"] = ["La priorità è obbligatoria (anche null)."];
        }

        return errors;
    }
}

public sealed class TodoStore
{
    private readonly ConcurrentDictionary<int, Todo> _items = new();
    private int _lastId;

    public IReadOnlyList<Todo> All() => _items.Values.OrderBy(t => t.Id).ToList();

    public Todo? Find(int id) => _items.TryGetValue(id, out var todo) ? todo : null;

    public Todo Add(string title, string? priority)
    {
        var todo = new Todo(Interlocked.Increment(ref _lastId), title, false, priority);
        _items[todo.Id] = todo;
        return todo;
    }

    public Todo? Update(int id, string? title, bool setPriority, string? priority)
    {
        if (!_items.TryGetValue(id, out var todo))
        {
            return null;
        }

        var updated = todo with { Title = title ?? todo.Title, Priority = setPriority ? priority : todo.Priority };
        _items[id] = updated;
        return updated;
    }

    public Todo? MarkDone(int id)
    {
        if (!_items.TryGetValue(id, out var todo))
        {
            return null;
        }

        var done = todo with { Done = true };
        _items[id] = done;
        return done;
    }
}

public partial class Program;
