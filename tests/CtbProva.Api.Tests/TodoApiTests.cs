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
    public async Task Created_todo_keeps_given_priority()
    {
        var created = await factory.CreateClient().PostAsJsonAsync("/api/todos", new { title = "urgente", priority = "alta" });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("alta", (await created.Content.ReadFromJsonAsync<Todo>())!.Priority);
    }

    [Fact]
    public async Task Created_todo_without_priority_is_media()
    {
        var client = factory.CreateClient();

        var missing = await client.PostAsJsonAsync("/api/todos", new { title = "senza" });
        var nullValue = await client.PostAsJsonAsync("/api/todos", new { title = "nulla", priority = (string?)null });

        Assert.Equal("media", (await missing.Content.ReadFromJsonAsync<Todo>())!.Priority);
        Assert.Equal("media", (await nullValue.Content.ReadFromJsonAsync<Todo>())!.Priority);
    }

    [Theory]
    [InlineData("Alta")]
    [InlineData("urgente")]
    [InlineData("")]
    public async Task Invalid_priority_on_create_is_rejected(string priority)
    {
        var client = factory.CreateClient();
        var title = $"rifiutato-{Guid.NewGuid()}";

        var response = await client.PostAsJsonAsync("/api/todos", new { title, priority });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("\"priority\"", await response.Content.ReadAsStringAsync());
        Assert.DoesNotContain(await client.GetFromJsonAsync<List<Todo>>("/api/todos") ?? [], t => t.Title == title);
    }

    [Fact]
    public async Task Priority_can_be_changed()
    {
        var client = factory.CreateClient();
        var todo = (await (await client.PostAsJsonAsync("/api/todos", new { title = "da cambiare" })).Content.ReadFromJsonAsync<Todo>())!;
        await client.PostAsync($"/api/todos/{todo.Id}/done", null);

        var response = await client.PutAsJsonAsync($"/api/todos/{todo.Id}/priority", new { priority = "bassa" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(todo with { Done = true, Priority = "bassa" }, await response.Content.ReadFromJsonAsync<Todo>());
        Assert.Equal("bassa", (await client.GetFromJsonAsync<Todo>($"/api/todos/{todo.Id}"))!.Priority);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"priority\":null}")]
    [InlineData("{\"priority\":\"Alta\"}")]
    public async Task Invalid_priority_change_is_rejected_and_todo_unchanged(string body)
    {
        var client = factory.CreateClient();
        var todo = (await (await client.PostAsJsonAsync("/api/todos", new { title = "fisso", priority = "alta" })).Content.ReadFromJsonAsync<Todo>())!;

        var response = await client.PutAsync($"/api/todos/{todo.Id}/priority", new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(todo, await client.GetFromJsonAsync<Todo>($"/api/todos/{todo.Id}"));
    }

    [Fact]
    public async Task Priority_change_on_unknown_todo_returns_not_found()
    {
        var response = await factory.CreateClient().PutAsJsonAsync("/api/todos/9999/priority", new { priority = "alta" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task List_can_be_filtered_by_priority_and_stays_ordered_by_id()
    {
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/todos", new { title = "a", priority = "alta" });
        await client.PostAsJsonAsync("/api/todos", new { title = "b", priority = "bassa" });
        await client.PostAsJsonAsync("/api/todos", new { title = "c", priority = "alta" });

        var all = (await client.GetFromJsonAsync<List<Todo>>("/api/todos"))!;
        var alta = (await client.GetFromJsonAsync<List<Todo>>("/api/todos?priority=alta"))!;

        Assert.Equal(all.OrderBy(t => t.Id).Select(t => t.Id), all.Select(t => t.Id));
        Assert.Contains(all, t => t.Priority == "bassa");
        Assert.Equal(all.Where(t => t.Priority == "alta").Select(t => t.Id), alta.Select(t => t.Id));
        Assert.True(alta.Count >= 2);
    }

    [Theory]
    [InlineData("Alta")]
    [InlineData("urgente")]
    public async Task Invalid_priority_filter_is_rejected(string priority)
    {
        var response = await factory.CreateClient().GetAsync($"/api/todos?priority={priority}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
