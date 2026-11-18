using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CtbProva.Api.Tests;

public class TodoApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

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
        var todo = await created.Content.ReadFromJsonAsync<Todo>(Json);
        Assert.NotNull(todo);
        Assert.Equal("comprare il pane", todo.Title);
        Assert.False(todo.Done);

        var done = await client.PostAsync($"/api/todos/{todo.Id}/done", null);
        var completed = await done.Content.ReadFromJsonAsync<Todo>(Json);
        Assert.True(completed!.Done);

        var read = await client.GetFromJsonAsync<Todo>($"/api/todos/{todo.Id}", Json);
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
    public async Task Todo_is_created_with_given_priority()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/todos", new { title = "urgente", priority = "high" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("high", body.GetProperty("priority").GetString());
    }

    [Fact]
    public async Task Todo_without_priority_defaults_to_medium()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/todos", new { title = "normale" });

        var todo = await response.Content.ReadFromJsonAsync<Todo>(Json);
        Assert.Equal(Priority.Medium, todo!.Priority);
    }

    [Theory]
    [InlineData("urgent")]
    [InlineData("1")]
    [InlineData("")]
    [InlineData("low,high")]
    public async Task Invalid_priority_on_create_is_rejected(string priority)
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/todos", new { title = "x", priority });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("priority", out _));
    }

    [Fact]
    public async Task Priority_can_be_changed()
    {
        var client = factory.CreateClient();
        var created = await (await client.PostAsJsonAsync("/api/todos", new { title = "da cambiare" })).Content.ReadFromJsonAsync<Todo>(Json);

        var response = await client.PutAsJsonAsync($"/api/todos/{created!.Id}/priority", new { priority = "low" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<Todo>(Json);
        Assert.Equal(Priority.Low, updated!.Priority);
        var read = await client.GetFromJsonAsync<Todo>($"/api/todos/{created.Id}", Json);
        Assert.Equal(Priority.Low, read!.Priority);
    }

    [Fact]
    public async Task Priority_change_on_unknown_todo_returns_not_found()
    {
        var response = await factory.CreateClient().PutAsJsonAsync("/api/todos/9999/priority", new { priority = "low" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("{\"priority\":\"urgent\"}")]
    [InlineData("{}")]
    public async Task Invalid_priority_change_is_rejected(string body)
    {
        var client = factory.CreateClient();
        var created = await (await client.PostAsJsonAsync("/api/todos", new { title = "x" })).Content.ReadFromJsonAsync<Todo>(Json);

        var response = await client.PutAsync($"/api/todos/{created!.Id}/priority", new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Todos_can_be_filtered_by_priority()
    {
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/todos", new { title = "alta", priority = "high" });
        await client.PostAsJsonAsync("/api/todos", new { title = "bassa", priority = "low" });

        var todos = await client.GetFromJsonAsync<List<Todo>>("/api/todos?priority=low", Json);

        Assert.NotEmpty(todos!);
        Assert.All(todos!, t => Assert.Equal(Priority.Low, t.Priority));
        Assert.Equal(todos!.OrderBy(t => t.Id).Select(t => t.Id), todos!.Select(t => t.Id));
    }

    [Fact]
    public async Task Invalid_priority_filter_is_rejected()
    {
        var response = await factory.CreateClient().GetAsync("/api/todos?priority=urgent");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
