using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;

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
        Assert.Equal("media", todo.Priority);

        var done = await client.PostAsync($"/api/todos/{todo.Id}/done", null);
        var completed = await done.Content.ReadFromJsonAsync<Todo>();
        Assert.True(completed!.Done);
        Assert.Equal("media", completed.Priority);

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
    public async Task Created_todo_keeps_valid_priority()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/todos", new { title = "urgente", priority = "alta" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var todo = await response.Content.ReadFromJsonAsync<Todo>();
        Assert.Equal("alta", todo!.Priority);
    }

    [Theory]
    [InlineData("Alta")]
    [InlineData("urgente")]
    [InlineData("")]
    public async Task Invalid_priority_on_create_is_rejected_and_not_stored(string priority)
    {
        var client = factory.CreateClient();
        var title = $"invalid-{Guid.NewGuid()}";

        var response = await client.PostAsJsonAsync("/api/todos", new { title, priority });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.Contains("priority", problem!.Errors.Keys);
        var all = await client.GetFromJsonAsync<List<Todo>>("/api/todos");
        Assert.DoesNotContain(all!, t => t.Title == title);
    }

    [Fact]
    public async Task Non_string_priority_on_create_is_rejected_and_not_stored()
    {
        var client = factory.CreateClient();
        var title = $"invalid-{Guid.NewGuid()}";

        var response = await client.PostAsJsonAsync("/api/todos", new { title, priority = 5 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var all = await client.GetFromJsonAsync<List<Todo>>("/api/todos");
        Assert.DoesNotContain(all!, t => t.Title == title);
    }

    [Fact]
    public async Task Priority_endpoint_updates_only_priority()
    {
        var client = factory.CreateClient();
        var todo = await Create(client, "cambia priorità", "bassa");
        await client.PostAsync($"/api/todos/{todo.Id}/done", null);

        var response = await client.PostAsJsonAsync($"/api/todos/{todo.Id}/priority", new { priority = "alta" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<Todo>();
        Assert.Equal(todo with { Done = true, Priority = "alta" }, updated);
        Assert.Equal(updated, await client.GetFromJsonAsync<Todo>($"/api/todos/{todo.Id}"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("ALTA")]
    [InlineData("urgente")]
    public async Task Priority_endpoint_rejects_missing_or_invalid_priority(string? priority)
    {
        var client = factory.CreateClient();
        var todo = await Create(client, "resta uguale", "bassa");

        var response = await client.PostAsJsonAsync($"/api/todos/{todo.Id}/priority", new { priority });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(todo, await client.GetFromJsonAsync<Todo>($"/api/todos/{todo.Id}"));
    }

    [Fact]
    public async Task Priority_endpoint_returns_not_found_for_unknown_todo()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/todos/9999/priority", new { priority = "alta" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task List_can_be_filtered_by_priority()
    {
        var client = factory.CreateClient();
        var low = await Create(client, "filtro bassa", "bassa");
        await Create(client, "filtro alta", "alta");

        var list = await client.GetFromJsonAsync<List<Todo>>("/api/todos?priority=bassa");

        Assert.Contains(low, list!);
        Assert.All(list!, t => Assert.Equal("bassa", t.Priority));
        Assert.Equal(list!.OrderBy(t => t.Id), list);
    }

    [Theory]
    [InlineData("Bassa")]
    [InlineData("nessuna")]
    public async Task List_rejects_invalid_priority_filter(string priority)
    {
        var response = await factory.CreateClient().GetAsync($"/api/todos?priority={priority}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task List_without_filter_returns_all_ordered_by_id()
    {
        var client = factory.CreateClient();
        var low = await Create(client, "tutti bassa", "bassa");
        var high = await Create(client, "tutti alta", "alta");

        var list = await client.GetFromJsonAsync<List<Todo>>("/api/todos");

        Assert.Contains(low, list!);
        Assert.Contains(high, list!);
        Assert.Equal(list!.OrderBy(t => t.Id), list);
    }

    private static async Task<Todo> Create(HttpClient client, string title, string priority)
    {
        var response = await client.PostAsJsonAsync("/api/todos", new { title, priority });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Todo>())!;
    }
}
