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

    private static StringContent Json(string json) => new(json, System.Text.Encoding.UTF8, "application/json");

    private static async Task<Todo> Create(HttpClient client, object body)
    {
        var response = await client.PostAsJsonAsync("/api/todos", body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<Todo>())!;
    }

    [Fact]
    public async Task Priority_defaults_to_media()
    {
        var todo = await Create(factory.CreateClient(), new { title = "x" });

        Assert.Equal("media", todo.Priority);
    }

    [Fact]
    public async Task Priority_can_be_set_on_creation()
    {
        var todo = await Create(factory.CreateClient(), new { title = "x", priority = "alta" });

        Assert.Equal("alta", todo.Priority);
    }

    [Theory]
    [InlineData("""{"title":"x","priority":"urgente"}""")]
    [InlineData("""{"title":"x","priority":null}""")]
    [InlineData("""{"title":"x","priority":2}""")]
    public async Task Invalid_priority_on_creation_is_rejected(string json)
    {
        var response = await factory.CreateClient().PostAsync("/api/todos", Json(json));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_priority_on_creation_reports_the_field_and_creates_nothing()
    {
        var client = factory.CreateClient();
        var before = (await client.GetFromJsonAsync<List<Todo>>("/api/todos"))!.Count;

        var response = await client.PostAsync("/api/todos", Json("""{"title":"mai creato","priority":"urgente"}"""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("\"priority\"", await response.Content.ReadAsStringAsync());
        Assert.Equal(before, (await client.GetFromJsonAsync<List<Todo>>("/api/todos"))!.Count);
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

    [Theory]
    [InlineData("""{"priority":"urgente"}""")]
    [InlineData("""{"priority":null}""")]
    [InlineData("""{}""")]
    public async Task Invalid_priority_change_is_rejected(string json)
    {
        var client = factory.CreateClient();
        var todo = await Create(client, new { title = "x" });

        var response = await client.PutAsync($"/api/todos/{todo.Id}/priority", Json(json));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Priority_change_on_unknown_todo_returns_not_found()
    {
        var response = await factory.CreateClient().PutAsJsonAsync("/api/todos/9999/priority", new { priority = "alta" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task List_can_be_filtered_by_priority()
    {
        var client = factory.CreateClient();
        var high = await Create(client, new { title = "filtro", priority = "alta" });
        await Create(client, new { title = "filtro", priority = "bassa" });

        var filtered = await client.GetFromJsonAsync<List<Todo>>("/api/todos?priority=alta");

        Assert.All(filtered!, t => Assert.Equal("alta", t.Priority));
        Assert.Contains(filtered!, t => t.Id == high.Id);
    }

    [Fact]
    public async Task Invalid_priority_filter_is_rejected()
    {
        var response = await factory.CreateClient().GetAsync("/api/todos?priority=urgente");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task List_stays_ordered_by_id()
    {
        var client = factory.CreateClient();
        await Create(client, new { title = "a", priority = "alta" });
        await Create(client, new { title = "b", priority = "bassa" });

        var all = await client.GetFromJsonAsync<List<Todo>>("/api/todos");

        Assert.Equal(all!.OrderBy(t => t.Id).Select(t => t.Id), all!.Select(t => t.Id));
    }
}
