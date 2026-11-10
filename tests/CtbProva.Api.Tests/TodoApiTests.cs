using System.Net;
using System.Net.Http.Json;
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

    private async Task<Todo> Create(object body)
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/todos", body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<Todo>())!;
    }

    [Fact]
    public async Task Todo_without_priority_defaults_to_media()
    {
        var todo = await Create(new { title = "x" });

        Assert.Equal("media", todo.Priority);
    }

    [Theory]
    [InlineData("alta", "alta")]
    [InlineData("BASSA", "bassa")]
    [InlineData("Media", "media")]
    public async Task Todo_is_created_with_priority_normalized_to_lowercase(string input, string expected)
    {
        var todo = await Create(new { title = "x", priority = input });

        Assert.Equal(expected, todo.Priority);
    }

    [Theory]
    [InlineData("urgente")]
    [InlineData("")]
    public async Task Invalid_priority_on_create_is_rejected(string priority)
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/todos", new { title = "x", priority });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("priority", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Invalid_priority_on_create_does_not_create_the_todo()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/todos", new { title = "mai-creato", priority = "urgente" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain(await client.GetFromJsonAsync<List<Todo>>("/api/todos") ?? [], t => t.Title == "mai-creato");
    }

    [Fact]
    public async Task Priority_is_present_on_get_and_done()
    {
        var client = factory.CreateClient();
        var todo = await Create(new { title = "x", priority = "alta" });

        var read = await client.GetFromJsonAsync<Todo>($"/api/todos/{todo.Id}");
        var done = await (await client.PostAsync($"/api/todos/{todo.Id}/done", null)).Content.ReadFromJsonAsync<Todo>();

        Assert.Equal("alta", read!.Priority);
        Assert.Equal("alta", done!.Priority);
    }

    [Fact]
    public async Task Priority_can_be_changed_and_only_priority_changes()
    {
        var client = factory.CreateClient();
        var todo = await Create(new { title = "x", priority = "bassa" });

        var response = await client.PutAsJsonAsync($"/api/todos/{todo.Id}/priority", new { priority = "ALTA" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<Todo>();
        Assert.Equal(todo with { Priority = "alta" }, updated);
        Assert.Equal("alta", (await client.GetFromJsonAsync<Todo>($"/api/todos/{todo.Id}"))!.Priority);
    }

    [Fact]
    public async Task Priority_change_on_unknown_todo_returns_not_found()
    {
        var response = await factory.CreateClient().PutAsJsonAsync("/api/todos/9999/priority", new { priority = "alta" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("urgente")]
    [InlineData(null)]
    public async Task Invalid_priority_change_is_rejected_and_todo_unchanged(string? priority)
    {
        var client = factory.CreateClient();
        var todo = await Create(new { title = "x", priority = "bassa" });

        var response = await client.PutAsJsonAsync($"/api/todos/{todo.Id}/priority", new { priority });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("priority", await response.Content.ReadAsStringAsync());
        Assert.Equal("bassa", (await client.GetFromJsonAsync<Todo>($"/api/todos/{todo.Id}"))!.Priority);
    }

    [Fact]
    public async Task List_can_be_filtered_by_priority()
    {
        var client = factory.CreateClient();
        var alta = await Create(new { title = "x", priority = "alta" });
        var bassa = await Create(new { title = "y", priority = "bassa" });

        var filtered = await client.GetFromJsonAsync<List<Todo>>("/api/todos?priority=ALTA");

        Assert.All(filtered!, t => Assert.Equal("alta", t.Priority));
        Assert.Contains(filtered!, t => t.Id == alta.Id);
        Assert.DoesNotContain(filtered!, t => t.Id == bassa.Id);
    }

    [Fact]
    public async Task List_with_invalid_priority_filter_is_rejected()
    {
        var response = await factory.CreateClient().GetAsync("/api/todos?priority=urgente");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task List_without_filter_is_ordered_by_id()
    {
        var client = factory.CreateClient();
        await Create(new { title = "x", priority = "bassa" });
        await Create(new { title = "y", priority = "alta" });

        var all = await client.GetFromJsonAsync<List<Todo>>("/api/todos");

        Assert.Equal(all!.OrderBy(t => t.Id).Select(t => t.Id), all!.Select(t => t.Id));
    }

    [Fact]
    public async Task Unknown_todo_returns_not_found()
    {
        var response = await factory.CreateClient().GetAsync("/api/todos/9999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
