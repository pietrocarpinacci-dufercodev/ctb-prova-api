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

    [Theory]
    [InlineData("bassa")]
    [InlineData("media")]
    [InlineData("alta")]
    public async Task Created_todo_keeps_allowed_priority(string priority)
    {
        var created = await factory.CreateClient().PostAsJsonAsync("/api/todos", new { title = "pane", priority });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var todo = await created.Content.ReadFromJsonAsync<Todo>();
        Assert.Equal(priority, todo!.Priority);
    }

    [Fact]
    public async Task Created_todo_without_priority_defaults_to_media()
    {
        var created = await factory.CreateClient().PostAsJsonAsync("/api/todos", new { title = "pane" });

        var todo = await created.Content.ReadFromJsonAsync<Todo>();
        Assert.Equal("media", todo!.Priority);
    }

    [Theory]
    [InlineData("urgente")]
    [InlineData("Alta")]
    [InlineData("")]
    public async Task Create_with_invalid_priority_is_rejected(string priority)
    {
        var client = factory.CreateClient();
        var before = (await client.GetFromJsonAsync<List<Todo>>("/api/todos"))!.Count;

        var response = await client.PostAsJsonAsync("/api/todos", new { title = "pane", priority });

        await AssertPriorityProblem(response);
        Assert.Equal(before, (await client.GetFromJsonAsync<List<Todo>>("/api/todos"))!.Count);
    }

    [Fact]
    public async Task Priority_can_be_changed()
    {
        var client = factory.CreateClient();
        var created = await (await client.PostAsJsonAsync("/api/todos", new { title = "pane" })).Content.ReadFromJsonAsync<Todo>();
        await client.PostAsync($"/api/todos/{created!.Id}/done", null);

        var response = await client.PutAsJsonAsync($"/api/todos/{created.Id}/priority", new { priority = "alta" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<Todo>();
        Assert.Equal(new Todo(created.Id, "pane", true, "alta"), updated);
        Assert.Equal("alta", (await client.GetFromJsonAsync<Todo>($"/api/todos/{created.Id}"))!.Priority);
    }

    [Fact]
    public async Task Changing_priority_of_unknown_todo_returns_not_found()
    {
        var response = await factory.CreateClient().PutAsJsonAsync("/api/todos/9999/priority", new { priority = "alta" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("urgente")]
    [InlineData("Alta")]
    [InlineData(null)]
    public async Task Changing_priority_to_invalid_value_is_rejected(string? priority)
    {
        var client = factory.CreateClient();
        var created = await (await client.PostAsJsonAsync("/api/todos", new { title = "pane" })).Content.ReadFromJsonAsync<Todo>();

        var response = await client.PutAsJsonAsync($"/api/todos/{created!.Id}/priority", new { priority });

        await AssertPriorityProblem(response);
        Assert.Equal("media", (await client.GetFromJsonAsync<Todo>($"/api/todos/{created.Id}"))!.Priority);
    }

    [Fact]
    public async Task Todos_can_be_filtered_by_priority()
    {
        var client = factory.CreateClient();
        var high = await (await client.PostAsJsonAsync("/api/todos", new { title = "a", priority = "alta" })).Content.ReadFromJsonAsync<Todo>();
        await client.PostAsJsonAsync("/api/todos", new { title = "b", priority = "bassa" });
        var high2 = await (await client.PostAsJsonAsync("/api/todos", new { title = "c", priority = "alta" })).Content.ReadFromJsonAsync<Todo>();

        var todos = (await client.GetFromJsonAsync<List<Todo>>("/api/todos?priority=alta"))!;

        Assert.All(todos, t => Assert.Equal("alta", t.Priority));
        Assert.Contains(high!, todos);
        Assert.Contains(high2!, todos);
        Assert.Equal(todos.Select(t => t.Id).Order(), todos.Select(t => t.Id));
    }

    [Fact]
    public async Task Filter_with_invalid_priority_is_rejected()
    {
        var response = await factory.CreateClient().GetAsync("/api/todos?priority=Alta");

        await AssertPriorityProblem(response);
    }

    private static async Task AssertPriorityProblem(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.True(problem!.Errors.ContainsKey("priority"));
    }

    [Fact]
    public async Task Unknown_todo_returns_not_found()
    {
        var response = await factory.CreateClient().GetAsync("/api/todos/9999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
