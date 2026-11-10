using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<TodoStore>();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseCors();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/todos", (string? priority, TodoStore store) =>
{
    if (priority is null)
    {
        return Results.Ok(store.All());
    }

    return Priorities.Normalize(priority) is { } p
        ? Results.Ok(store.All().Where(t => t.Priority == p))
        : InvalidPriority();
});

app.MapGet("/api/todos/{id:int}", (int id, TodoStore store) =>
    store.Find(id) is { } todo ? Results.Ok(todo) : Results.NotFound());

app.MapPost("/api/todos", (NewTodo input, TodoStore store) =>
{
    if (string.IsNullOrWhiteSpace(input.Title))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["title"] = ["Il titolo è obbligatorio."] });
    }

    var priority = input.Priority is null ? "media" : Priorities.Normalize(input.Priority);
    if (priority is null)
    {
        return InvalidPriority();
    }

    var todo = store.Add(input.Title.Trim(), priority);
    return Results.Created($"/api/todos/{todo.Id}", todo);
});

app.MapPut("/api/todos/{id:int}/priority", (int id, NewPriority input, TodoStore store) =>
{
    if (Priorities.Normalize(input.Priority) is not { } priority)
    {
        return InvalidPriority();
    }

    return store.SetPriority(id, priority) is { } todo ? Results.Ok(todo) : Results.NotFound();
});

app.MapPost("/api/todos/{id:int}/done", (int id, TodoStore store) =>
    store.MarkDone(id) is { } todo ? Results.Ok(todo) : Results.NotFound());

app.Run();

static IResult InvalidPriority() =>
    Results.ValidationProblem(new Dictionary<string, string[]> { ["priority"] = ["La priorità deve essere bassa, media o alta."] });

public record Todo(int Id, string Title, bool Done, string Priority);

public record NewTodo(string? Title, string? Priority);

public record NewPriority(string? Priority);

public static class Priorities
{
    public static string? Normalize(string? value) =>
        value?.ToLowerInvariant() is "bassa" or "media" or "alta" ? value.ToLowerInvariant() : null;
}

public sealed class TodoStore
{
    private readonly ConcurrentDictionary<int, Todo> _items = new();
    private int _lastId;

    public IReadOnlyList<Todo> All() => _items.Values.OrderBy(t => t.Id).ToList();

    public Todo? Find(int id) => _items.TryGetValue(id, out var todo) ? todo : null;

    public Todo Add(string title, string priority)
    {
        var todo = new Todo(Interlocked.Increment(ref _lastId), title, false, priority);
        _items[todo.Id] = todo;
        return todo;
    }

    public Todo? SetPriority(int id, string priority)
    {
        if (!_items.TryGetValue(id, out var todo))
        {
            return null;
        }

        var updated = todo with { Priority = priority };
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
