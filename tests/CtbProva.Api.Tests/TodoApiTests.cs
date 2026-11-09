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

    [Fact]
    public async Task Priority_defaults_to_media_and_can_be_set_on_creation()
    {
        var client = factory.CreateClient();

        var def = await (await client.PostAsJsonAsync("/api/todos", new { title = "a" })).Content.ReadFromJsonAsync<Todo>();
        Assert.Equal("media", def!.Priority);

        var high = await (await client.PostAsJsonAsync("/api/todos", new { title = "b", priority = "ALTA" })).Content.ReadFromJsonAsync<Todo>();
        Assert.Equal("alta", high!.Priority);
    }

    [Fact]
    public async Task Invalid_priority_is_rejected_on_creation()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/todos", new { title = "x", priority = "urgente" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Priority_can_be_changed_with_dedicated_endpoint()
    {
        var client = factory.CreateClient();
        var todo = await (await client.PostAsJsonAsync("/api/todos", new { title = "c" })).Content.ReadFromJsonAsync<Todo>();

        var ok = await client.PutAsJsonAsync($"/api/todos/{todo!.Id}/priority", new { priority = "bassa" });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal("bassa", (await client.GetFromJsonAsync<Todo>($"/api/todos/{todo.Id}"))!.Priority);

        var bad = await client.PutAsJsonAsync($"/api/todos/{todo.Id}/priority", new { priority = "boh" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

        var missing = await client.PutAsJsonAsync("/api/todos/9999/priority", new { priority = "alta" });
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task List_can_be_filtered_by_priority()
    {
        var client = factory.CreateClient();
        var low = await (await client.PostAsJsonAsync("/api/todos", new { title = "d", priority = "bassa" })).Content.ReadFromJsonAsync<Todo>();

        var filtered = await client.GetFromJsonAsync<List<Todo>>("/api/todos?priority=bassa");
        Assert.NotEmpty(filtered!);
        Assert.All(filtered, t => Assert.Equal("bassa", t.Priority));
        Assert.Contains(filtered, t => t.Id == low!.Id);

        var bad = await client.GetAsync("/api/todos?priority=boh");
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }
}
