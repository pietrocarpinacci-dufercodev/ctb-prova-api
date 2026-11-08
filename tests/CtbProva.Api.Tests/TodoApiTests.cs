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

    [Fact]
    public async Task Unknown_todo_returns_not_found()
    {
        var response = await factory.CreateClient().GetAsync("/api/todos/9999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<Todo> Create(HttpClient client, object body) =>
        (await (await client.PostAsJsonAsync("/api/todos", body)).Content.ReadFromJsonAsync<Todo>())!;

    [Fact]
    public async Task Todo_without_priority_gets_default_media()
    {
        var todo = await Create(factory.CreateClient(), new { title = "x" });

        Assert.Equal("media", todo.Priority);
    }

    [Fact]
    public async Task Todo_is_created_with_given_priority()
    {
        var todo = await Create(factory.CreateClient(), new { title = "x", priority = "alta" });

        Assert.Equal("alta", todo.Priority);
    }

    [Theory]
    [InlineData("urgente")]
    [InlineData("Alta")]
    [InlineData("")]
    public async Task Invalid_priority_is_rejected_on_create(string priority)
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/todos", new { title = "x", priority });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Priority_can_be_changed()
    {
        var client = factory.CreateClient();
        var todo = await Create(client, new { title = "x" });

        var response = await client.PutAsJsonAsync($"/api/todos/{todo.Id}/priority", new { priority = "bassa" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("bassa", (await response.Content.ReadFromJsonAsync<Todo>())!.Priority);
        Assert.Equal("bassa", (await client.GetFromJsonAsync<Todo>($"/api/todos/{todo.Id}"))!.Priority);
    }

    [Fact]
    public async Task Invalid_priority_is_rejected_on_change()
    {
        var client = factory.CreateClient();
        var todo = await Create(client, new { title = "x" });

        var response = await client.PutAsJsonAsync($"/api/todos/{todo.Id}/priority", new { priority = "boh" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Changing_priority_of_unknown_todo_returns_not_found()
    {
        var response = await factory.CreateClient().PutAsJsonAsync("/api/todos/9999/priority", new { priority = "alta" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task List_can_be_filtered_by_priority()
    {
        var client = factory.CreateClient();
        var high = await Create(client, new { title = "h", priority = "alta" });
        await Create(client, new { title = "l", priority = "bassa" });

        var filtered = await client.GetFromJsonAsync<List<Todo>>("/api/todos?priority=alta");

        Assert.Contains(filtered!, t => t.Id == high.Id);
        Assert.All(filtered!, t => Assert.Equal("alta", t.Priority));
    }

    [Fact]
    public async Task Invalid_priority_filter_is_rejected()
    {
        var response = await factory.CreateClient().GetAsync("/api/todos?priority=boh");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
