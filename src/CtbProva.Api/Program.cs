using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<TodoStore>();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseCors();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/todos", (string? priority, TodoStore store) =>
{
    if (priority is not null && !Priorities.IsValid(priority))
    {
        return Results.ValidationProblem(Priorities.Error);
    }

    return Results.Ok(store.All(priority));
});

app.MapGet("/api/todos/{id:int}", (int id, TodoStore store) =>
    store.Find(id) is { } todo ? Results.Ok(todo) : Results.NotFound());

app.MapPost("/api/todos", (NewTodo input, TodoStore store) =>
{
    if (string.IsNullOrWhiteSpace(input.Title))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["title"] = ["Il titolo è obbligatorio."] });
    }

    if (input.Priority is not null && !Priorities.IsValid(input.Priority))
    {
        return Results.ValidationProblem(Priorities.Error);
    }

    var todo = store.Add(input.Title.Trim(), input.Priority ?? Priorities.Default);
    return Results.Created($"/api/todos/{todo.Id}", todo);
});

app.MapPost("/api/todos/{id:int}/done", (int id, TodoStore store) =>
    store.MarkDone(id) is { } todo ? Results.Ok(todo) : Results.NotFound());

app.MapPut("/api/todos/{id:int}/priority", (int id, PriorityChange input, TodoStore store) =>
{
    if (!Priorities.IsValid(input.Priority))
    {
        return Results.ValidationProblem(Priorities.Error);
    }

    return store.SetPriority(id, input.Priority!) is { } todo ? Results.Ok(todo) : Results.NotFound();
});

app.Run();

public record Todo(int Id, string Title, bool Done, string Priority = Priorities.Default);

public record NewTodo(string? Title, string? Priority = null);

public record PriorityChange(string? Priority);

public static class Priorities
{
    public const string Default = "media";

    public static readonly Dictionary<string, string[]> Error = new() { ["priority"] = ["La priorità deve essere bassa, media o alta."] };

    // Confronto case-sensitive, come deciso (D4).
    public static bool IsValid(string? value) => value is "bassa" or "media" or "alta";
}

public sealed class TodoStore
{
    private readonly ConcurrentDictionary<int, Todo> _items = new();
    private int _lastId;

    public IReadOnlyList<Todo> All(string? priority = null) =>
        _items.Values.Where(t => priority is null || t.Priority == priority).OrderBy(t => t.Id).ToList();

    public Todo? Find(int id) => _items.TryGetValue(id, out var todo) ? todo : null;

    public Todo Add(string title, string priority)
    {
        var todo = new Todo(Interlocked.Increment(ref _lastId), title, false, priority);
        _items[todo.Id] = todo;
        return todo;
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
}

public partial class Program;
