using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CtbProva.Api.Tests;

public class TodoApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Health_returns_ok()
    {
        var response = await factory.CreateClient().GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Created_todo_can_be_read_and_completed()
    {
        var client = factory.CreateClient();

        var created = await client.PostAsJsonAsync("/api/todos", new { title = "  comprare il pane  " });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var todo = await created.Content.ReadFromJsonAsync<Todo>();
        Assert.NotNull(todo);
        Assert.Equal("comprare il pane", todo.Title);
        Assert.False(todo.Done);

        var done = await client.PostAsync($"/api/todos/{todo.Id}/done", null);
        var completed = await done.Content.ReadFromJsonAsync<Todo>();
        Assert.True(completed!.Done);

        var read = await client.GetFromJsonAsync<Todo>($"/api/todos/{todo.Id}");
        Assert.True(read!.Done);
    }

    [Fact]
    public async Task Empty_title_is_rejected()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/todos", new { title = " " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_todo_returns_not_found()
    {
        var response = await factory.CreateClient().GetAsync("/api/todos/9999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DueDate_is_stored_and_read_back()
    {
        var client = factory.CreateClient();

        var created = await client.PostAsJsonAsync("/api/todos", new { title = "con scadenza", dueDate = "2030-05-01" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var todo = await created.Content.ReadFromJsonAsync<Todo>();
        Assert.Equal(new DateOnly(2030, 5, 1), todo!.DueDate);

        var read = await client.GetFromJsonAsync<Todo>($"/api/todos/{todo.Id}");
        Assert.Equal(new DateOnly(2030, 5, 1), read!.DueDate);
    }

    [Fact]
    public async Task Todo_without_due_date_has_null_due_date()
    {
        var created = await factory.CreateClient().PostAsJsonAsync("/api/todos", new { title = "senza scadenza" });
        var todo = await created.Content.ReadFromJsonAsync<Todo>();

        Assert.Null(todo!.DueDate);
    }

    [Fact]
    public async Task Invalid_due_date_is_rejected()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/todos", new { title = "x", dueDate = "non una data" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Overdue_lists_only_open_todos_due_before_today()
    {
        var today = new DateOnly(2031, 6, 15);
        var client = factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
            s.AddSingleton<TimeProvider>(new FixedTime(new DateTimeOffset(2031, 6, 15, 23, 30, 0, TimeSpan.Zero))))).CreateClient();

        async Task<Todo> Add(string title, DateOnly? due) =>
            (await (await client.PostAsJsonAsync("/api/todos", new { title, dueDate = due })).Content.ReadFromJsonAsync<Todo>())!;

        var past = await Add("passato", today.AddDays(-1));
        await Add("oggi", today);
        await Add("futuro", today.AddDays(1));
        await Add("senza data", null);
        var donePast = await Add("fatto", today.AddDays(-5));
        await client.PostAsync($"/api/todos/{donePast.Id}/done", null);
        var older = await Add("più vecchio, creato dopo", today.AddDays(-3));
        var samePastLater = await Add("stessa data, creato dopo", today.AddDays(-1));

        var overdue = await client.GetFromJsonAsync<List<Todo>>("/api/todos/overdue");

        // per scadenza crescente, a parità di data per id
        Assert.Equal([older.Id, past.Id, samePastLater.Id], overdue!.Select(t => t.Id));
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
