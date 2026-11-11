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

    [Fact]
    public async Task Unknown_todo_returns_not_found()
    {
        var response = await factory.CreateClient().GetAsync("/api/todos/9999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("bassa")]
    [InlineData("media")]
    [InlineData("alta")]
    public async Task Created_todo_keeps_valid_priority(string priority)
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/todos", new { title = "x", priority });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var todo = await response.Content.ReadFromJsonAsync<Todo>();
        Assert.Equal(priority, todo!.Priority);
    }

    [Fact]
    public async Task Created_todo_without_priority_is_media()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/todos", new { title = "x" });

        var todo = await response.Content.ReadFromJsonAsync<Todo>();
        Assert.Equal("media", todo!.Priority);
    }

    [Theory]
    [InlineData("Alta")]
    [InlineData("high")]
    [InlineData("")]
    public async Task Invalid_priority_on_create_is_rejected(string priority)
    {
        var client = factory.CreateClient();
        var title = $"rifiutato-{Guid.NewGuid()}";

        var response = await client.PostAsJsonAsync("/api/todos", new { title, priority });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.Contains("priority", problem!.Errors.Keys);
        var all = await client.GetFromJsonAsync<List<Todo>>("/api/todos");
        Assert.DoesNotContain(all!, t => t.Title == title);
    }

    [Fact]
    public async Task Priority_can_be_changed()
    {
        var client = factory.CreateClient();
        var todo = await (await client.PostAsJsonAsync("/api/todos", new { title = "x" })).Content.ReadFromJsonAsync<Todo>();

        var response = await client.PutAsJsonAsync($"/api/todos/{todo!.Id}/priority", new { priority = "alta" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("alta", (await response.Content.ReadFromJsonAsync<Todo>())!.Priority);
        Assert.Equal("alta", (await client.GetFromJsonAsync<Todo>($"/api/todos/{todo.Id}"))!.Priority);
    }

    [Fact]
    public async Task Priority_change_on_unknown_todo_returns_not_found()
    {
        var response = await factory.CreateClient().PutAsJsonAsync("/api/todos/9999/priority", new { priority = "alta" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"priority\":null}")]
    [InlineData("{\"priority\":\"Alta\"}")]
    [InlineData("{\"priority\":\"high\"}")]
    public async Task Invalid_priority_change_is_rejected_and_todo_unchanged(string body)
    {
        var client = factory.CreateClient();
        var todo = await (await client.PostAsJsonAsync("/api/todos", new { title = "x", priority = "bassa" })).Content.ReadFromJsonAsync<Todo>();

        var response = await client.PutAsync($"/api/todos/{todo!.Id}/priority", new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("bassa", (await client.GetFromJsonAsync<Todo>($"/api/todos/{todo.Id}"))!.Priority);
    }

    [Fact]
    public async Task List_can_be_filtered_by_priority()
    {
        var client = factory.CreateClient();
        var alta = await (await client.PostAsJsonAsync("/api/todos", new { title = "a", priority = "alta" })).Content.ReadFromJsonAsync<Todo>();
        var bassa = await (await client.PostAsJsonAsync("/api/todos", new { title = "b", priority = "bassa" })).Content.ReadFromJsonAsync<Todo>();

        var filtered = await client.GetFromJsonAsync<List<Todo>>("/api/todos?priority=alta");

        Assert.All(filtered!, t => Assert.Equal("alta", t.Priority));
        Assert.Contains(filtered!, t => t.Id == alta!.Id);
        Assert.DoesNotContain(filtered!, t => t.Id == bassa!.Id);
    }

    [Fact]
    public async Task List_without_filter_returns_all_ordered_by_id()
    {
        var client = factory.CreateClient();
        var alta = await (await client.PostAsJsonAsync("/api/todos", new { title = "a", priority = "alta" })).Content.ReadFromJsonAsync<Todo>();
        var bassa = await (await client.PostAsJsonAsync("/api/todos", new { title = "b", priority = "bassa" })).Content.ReadFromJsonAsync<Todo>();

        var all = await client.GetFromJsonAsync<List<Todo>>("/api/todos");

        Assert.Contains(all!, t => t.Id == alta!.Id);
        Assert.Contains(all!, t => t.Id == bassa!.Id);
        Assert.Equal(all!.Select(t => t.Id).Order(), all.Select(t => t.Id));
    }

    [Theory]
    [InlineData("Alta")]
    [InlineData("high")]
    [InlineData("")]
    public async Task Invalid_priority_filter_is_rejected(string priority)
    {
        var response = await factory.CreateClient().GetAsync($"/api/todos?priority={priority}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
