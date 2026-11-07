using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<TodoStore>();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseCors();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/todos", (TodoStore store) => store.All());

app.MapGet("/api/todos/{id:int}", (int id, TodoStore store) =>
    store.Find(id) is { } todo ? Results.Ok(todo) : Results.NotFound());

app.MapPost("/api/todos", (NewTodo input, TodoStore store) =>
{
    if (string.IsNullOrWhiteSpace(input.Title))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["title"] = ["Il titolo è obbligatorio."] });
    }

    var todo = store.Add(input.Title.Trim());
    return Results.Created($"/api/todos/{todo.Id}", todo);
});

app.MapPost("/api/todos/{id:int}/done", (int id, TodoStore store) =>
    store.MarkDone(id) is { } todo ? Results.Ok(todo) : Results.NotFound());

app.MapDelete("/api/todos/{id:int}", (int id, TodoStore store) =>
    store.Remove(id) ? Results.NoContent() : Results.NotFound());

app.Run();

public record Todo(int Id, string Title, bool Done);

public record NewTodo(string? Title);

public sealed class TodoStore
{
    private readonly ConcurrentDictionary<int, Todo> _items = new();
    private int _lastId;

    public IReadOnlyList<Todo> All() => _items.Values.OrderBy(t => t.Id).ToList();

    public Todo? Find(int id) => _items.TryGetValue(id, out var todo) ? todo : null;

    public Todo Add(string title)
    {
        var todo = new Todo(Interlocked.Increment(ref _lastId), title, false);
        _items[todo.Id] = todo;
        return todo;
    }

    public bool Remove(int id) => _items.TryRemove(id, out _);

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
