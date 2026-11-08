using System.Collections.Concurrent;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<TodoStore>();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseCors();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/todos", (string? priority, TodoStore store) =>
    priority is not null && !Priorities.IsValid(priority)
        ? PriorityError()
        : Results.Ok(store.All().Where(t => priority is null || t.Priority == priority)));

app.MapGet("/api/todos/{id:int}", (int id, TodoStore store) =>
    store.Find(id) is { } todo ? Results.Ok(todo) : Results.NotFound());

app.MapPost("/api/todos", (NewTodo input, TodoStore store) =>
{
    if (string.IsNullOrWhiteSpace(input.Title))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["title"] = ["Il titolo è obbligatorio."] });
    }

    var priority = Priorities.Default;
    if (input.Priority.ValueKind != JsonValueKind.Undefined && !Priorities.TryParse(input.Priority, out priority))
    {
        return PriorityError();
    }

    var todo = store.Add(input.Title.Trim(), priority);
    return Results.Created($"/api/todos/{todo.Id}", todo);
});

app.MapPost("/api/todos/{id:int}/done", (int id, TodoStore store) =>
    store.MarkDone(id) is { } todo ? Results.Ok(todo) : Results.NotFound());

app.MapPut("/api/todos/{id:int}/priority", (int id, PriorityChange input, TodoStore store) =>
    !Priorities.TryParse(input.Priority, out var priority) ? PriorityError()
    : store.SetPriority(id, priority) is { } todo ? Results.Ok(todo) : Results.NotFound());

static IResult PriorityError() =>
    Results.ValidationProblem(new Dictionary<string, string[]> { ["priority"] = ["Valori ammessi: bassa, media, alta."] });

app.Run();

public record Todo(int Id, string Title, bool Done, string Priority);

// JsonElement e non string?: distingue il campo assente (Undefined) dal null esplicito.
public record NewTodo(string? Title, JsonElement Priority);

public record PriorityChange(JsonElement Priority);

public static class Priorities
{
    public const string Default = "media";

    public static bool IsValid(string? value) => value is "bassa" or "media" or "alta";

    public static bool TryParse(JsonElement element, out string priority)
    {
        priority = element.ValueKind == JsonValueKind.String ? element.GetString()! : "";
        return IsValid(priority);
    }
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

        var changed = todo with { Priority = priority };
        _items[id] = changed;
        return changed;
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
