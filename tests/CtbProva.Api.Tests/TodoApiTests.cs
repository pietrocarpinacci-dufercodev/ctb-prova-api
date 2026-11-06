using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CtbProva.Api.Tests;

public class TodoApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    private static async Task<Todo> Create(HttpClient client, string json)
    {
        var response = await client.PostAsync("/api/todos", Json(json));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<Todo>())!;
    }

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

        var created = await client.PostAsJsonAsync("/api/todos", new { title = "  comprare il pane  ", priority = "high" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var todo = await created.Content.ReadFromJsonAsync<Todo>();
        Assert.NotNull(todo);
        Assert.Equal("comprare il pane", todo.Title);
        Assert.False(todo.Done);
        Assert.Equal("high", todo.Priority);

        var done = await client.PostAsync($"/api/todos/{todo.Id}/done", null);
        var completed = await done.Content.ReadFromJsonAsync<Todo>();
        Assert.True(completed!.Done);

        var read = await client.GetFromJsonAsync<Todo>($"/api/todos/{todo.Id}");
        Assert.True(read!.Done);
    }

    [Fact]
    public async Task Empty_title_is_rejected()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/todos", new { title = " ", priority = "low" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_todo_returns_not_found()
    {
        var response = await factory.CreateClient().GetAsync("/api/todos/9999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_accepts_null_priority()
    {
        var todo = await Create(factory.CreateClient(), """{"title":"a","priority":null}""");

        Assert.Null(todo.Priority);
    }

    [Theory]
    [InlineData("""{"title":"a"}""")]
    [InlineData("""{"priority":"low"}""")]
    [InlineData("""{"title":"a","priority":"urgent"}""")]
    [InlineData("""{"title":"a","priority":3}""")]
    [InlineData("""{"title":null,"priority":"low"}""")]
    public async Task Post_rejects_missing_or_invalid_fields(string json)
    {
        var client = factory.CreateClient();
        var before = (await client.GetFromJsonAsync<List<Todo>>("/api/todos"))!.Count;

        var response = await client.PostAsync("/api/todos", Json(json));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(before, (await client.GetFromJsonAsync<List<Todo>>("/api/todos"))!.Count);
    }

    [Fact]
    public async Task Patch_updates_only_present_fields()
    {
        var client = factory.CreateClient();
        var todo = await Create(client, """{"title":"a","priority":"low"}""");

        var onlyPriority = await client.PatchAsync($"/api/todos/{todo.Id}", Json("""{"priority":"high"}"""));
        var p = await onlyPriority.Content.ReadFromJsonAsync<Todo>();
        Assert.Equal("a", p!.Title);
        Assert.Equal("high", p.Priority);

        var onlyTitle = await client.PatchAsync($"/api/todos/{todo.Id}", Json("""{"title":" b "}"""));
        var t = await onlyTitle.Content.ReadFromJsonAsync<Todo>();
        Assert.Equal("b", t!.Title);
        Assert.Equal("high", t.Priority);

        var empty = await client.PatchAsync($"/api/todos/{todo.Id}", Json("{}"));
        Assert.Equal(t, await empty.Content.ReadFromJsonAsync<Todo>());
    }

    [Fact]
    public async Task Patch_null_priority_removes_it_and_works_on_done_todos()
    {
        var client = factory.CreateClient();
        var todo = await Create(client, """{"title":"a","priority":"low"}""");
        await client.PostAsync($"/api/todos/{todo.Id}/done", null);

        var response = await client.PatchAsync($"/api/todos/{todo.Id}", Json("""{"priority":null}"""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var patched = await response.Content.ReadFromJsonAsync<Todo>();
        Assert.Null(patched!.Priority);
        Assert.Equal("a", patched.Title);
        Assert.True(patched.Done);
    }

    [Theory]
    [InlineData("""{"title":null}""")]
    [InlineData("""{"title":"  "}""")]
    [InlineData("""{"priority":"urgent"}""")]
    [InlineData("""{"title":"x","priority":"urgent"}""")]
    public async Task Patch_rejects_invalid_values_without_changes(string json)
    {
        var client = factory.CreateClient();
        var todo = await Create(client, """{"title":"a","priority":"low"}""");

        var response = await client.PatchAsync($"/api/todos/{todo.Id}", Json(json));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(todo, await client.GetFromJsonAsync<Todo>($"/api/todos/{todo.Id}"));
    }

    [Fact]
    public async Task Patch_unknown_todo_returns_not_found()
    {
        var response = await factory.CreateClient().PatchAsync("/api/todos/9999", Json("""{"priority":"low"}"""));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
