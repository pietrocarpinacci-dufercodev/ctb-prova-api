using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<TodoStore>();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));

var app = builder.Build();
app.UseCors();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/todos", (string? priority, TodoStore store) =>
{
    if (priority is null)
    {
        return Results.Ok(store.All());
    }

    return PriorityParser.TryParse(priority, out var value)
        ? Results.Ok(store.All().Where(t => t.Priority == value).ToList())
        : PriorityParser.Invalid();
});

app.MapGet("/api/todos/{id:int}", (int id, TodoStore store) =>
    store.Find(id) is { } todo ? Results.Ok(todo) : Results.NotFound());

app.MapPost("/api/todos", (NewTodo input, TodoStore store) =>
{
    if (string.IsNullOrWhiteSpace(input.Title))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["title"] = ["Il titolo è obbligatorio."] });
    }

    var priority = Priority.Medium;
    if (input.Priority is not null && !PriorityParser.TryParse(input.Priority, out priority))
    {
        return PriorityParser.Invalid();
    }

    var todo = store.Add(input.Title.Trim(), priority);
    return Results.Created($"/api/todos/{todo.Id}", todo);
});

app.MapPost("/api/todos/{id:int}/done", (int id, TodoStore store) =>
    store.MarkDone(id) is { } todo ? Results.Ok(todo) : Results.NotFound());

app.MapPut("/api/todos/{id:int}/priority", (int id, PriorityChange input, TodoStore store) =>
{
    if (!PriorityParser.TryParse(input.Priority, out var priority))
    {
        return PriorityParser.Invalid();
    }

    return store.SetPriority(id, priority) is { } todo ? Results.Ok(todo) : Results.NotFound();
});

app.Run();

public enum Priority { Low, Medium, High }

public record Todo(int Id, string Title, bool Done, Priority Priority);

public record NewTodo(string? Title, string? Priority);

public record PriorityChange(string? Priority);

public static class PriorityParser
{
    // Match names only: Enum.TryParse alone would also accept numbers like "1" and lists like "low,high".
    public static bool TryParse(string? value, out Priority priority)
    {
        priority = default;
        return Enum.GetNames<Priority>().Contains(value, StringComparer.OrdinalIgnoreCase)
            && Enum.TryParse(value, ignoreCase: true, out priority);
    }

    public static IResult Invalid() =>
        Results.ValidationProblem(new Dictionary<string, string[]> { ["priority"] = ["Priority must be one of: low, medium, high."] });
}

public sealed class TodoStore
{
    private readonly ConcurrentDictionary<int, Todo> _items = new();
    private int _lastId;

    public IReadOnlyList<Todo> All() => _items.Values.OrderBy(t => t.Id).ToList();

    public Todo? Find(int id) => _items.TryGetValue(id, out var todo) ? todo : null;

    public Todo Add(string title, Priority priority)
    {
        var todo = new Todo(Interlocked.Increment(ref _lastId), title, false, priority);
        _items[todo.Id] = todo;
        return todo;
    }

    public Todo? MarkDone(int id) => Update(id, todo => todo with { Done = true });

    public Todo? SetPriority(int id, Priority priority) => Update(id, todo => todo with { Priority = priority });

    private Todo? Update(int id, Func<Todo, Todo> change)
    {
        if (!_items.TryGetValue(id, out var todo))
        {
            return null;
        }

        var updated = change(todo);
        _items[id] = updated;
        return updated;
    }
}

public partial class Program;
