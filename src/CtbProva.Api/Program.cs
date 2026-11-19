using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<TodoStore>();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseCors();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/todos", (string? priority, TodoStore store) =>
    priority is null || Priorities.IsValid(priority) ? Results.Ok(store.All(priority)) : Priorities.Invalid());

app.MapGet("/api/todos/{id:int}", (int id, TodoStore store) =>
    store.Find(id) is { } todo ? Results.Ok(todo) : Results.NotFound());

app.MapPost("/api/todos", (NewTodo input, TodoStore store) =>
{
    if (string.IsNullOrWhiteSpace(input.Title))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["title"] = ["Il titolo è obbligatorio."] });
    }

    var priority = input.Priority ?? Priorities.Default;
    if (!Priorities.IsValid(priority))
    {
        return Priorities.Invalid();
    }

    var todo = store.Add(input.Title.Trim(), priority);
    return Results.Created($"/api/todos/{todo.Id}", todo);
});

app.MapPost("/api/todos/{id:int}/done", (int id, TodoStore store) =>
    store.MarkDone(id) is { } todo ? Results.Ok(todo) : Results.NotFound());

app.MapPost("/api/todos/{id:int}/priority", (int id, PriorityInput input, TodoStore store) =>
{
    if (input.Priority is null || !Priorities.IsValid(input.Priority))
    {
        return Priorities.Invalid();
    }

    return store.SetPriority(id, input.Priority) is { } todo ? Results.Ok(todo) : Results.NotFound();
});

app.Run();

public record Todo(int Id, string Title, bool Done, string Priority);

public record NewTodo(string? Title, string? Priority);

public record PriorityInput(string? Priority);

public static class Priorities
{
    public const string Default = "media";

    // Case-sensitive on purpose: only lowercase values are part of the contract.
    private static readonly HashSet<string> Allowed = ["bassa", "media", "alta"];

    public static bool IsValid(string priority) => Allowed.Contains(priority);

    public static IResult Invalid() =>
        Results.ValidationProblem(new Dictionary<string, string[]> { ["priority"] = ["Priority must be one of: bassa, media, alta."] });
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
