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
    public async Task Deleted_todo_is_returned_and_no_longer_readable()
    {
        var client = factory.CreateClient();
        var created = await client.PostAsJsonAsync("/api/todos", new { title = "da eliminare" });
        var todo = await created.Content.ReadFromJsonAsync<Todo>();

        var deleted = await client.DeleteAsync($"/api/todos/{todo!.Id}");

        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.Equal(todo, await deleted.Content.ReadFromJsonAsync<Todo>());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/todos/{todo.Id}")).StatusCode);
    }

    [Fact]
    public async Task Deleting_unknown_todo_returns_not_found()
    {
        var response = await factory.CreateClient().DeleteAsync("/api/todos/9999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_todo_returns_not_found()
    {
        var response = await factory.CreateClient().GetAsync("/api/todos/9999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
