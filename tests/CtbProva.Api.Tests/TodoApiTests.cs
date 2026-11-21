using System.Net;
using System.Net.Http.Json;
using System.Text;
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
    public async Task Created_todo_has_given_priority()
    {
        var created = await factory.CreateClient().PostAsJsonAsync("/api/todos", new { title = "pane", priority = "alta" });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("alta", (await created.Content.ReadFromJsonAsync<Todo>())!.Priority);
    }

    [Fact]
    public async Task Created_todo_without_priority_defaults_to_media()
    {
        var created = await factory.CreateClient().PostAsJsonAsync("/api/todos", new { title = "pane" });

        Assert.Equal("media", (await created.Content.ReadFromJsonAsync<Todo>())!.Priority);
    }

    [Fact]
    public async Task Invalid_priority_on_create_is_rejected()
    {
        var client = factory.CreateClient();
        var before = (await client.GetFromJsonAsync<List<Todo>>("/api/todos"))!.Count;

        var response = await client.PostAsJsonAsync("/api/todos", new { title = "pane", priority = "urgente" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("\"priority\"", await response.Content.ReadAsStringAsync());
        Assert.Equal(before, (await client.GetFromJsonAsync<List<Todo>>("/api/todos"))!.Count);
    }

    [Fact]
    public async Task Priority_can_be_changed_keeping_title_and_done()
    {
        var client = factory.CreateClient();
        var todo = (await (await client.PostAsJsonAsync("/api/todos", new { title = "latte" })).Content.ReadFromJsonAsync<Todo>())!;
        await client.PostAsync($"/api/todos/{todo.Id}/done", null);

        var response = await client.PutAsJsonAsync($"/api/todos/{todo.Id}/priority", new { priority = "bassa" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<Todo>();
        Assert.Equal(new Todo(todo.Id, "latte", true, "bassa"), updated);
        Assert.Equal(updated, await client.GetFromJsonAsync<Todo>($"/api/todos/{todo.Id}"));
    }

    [Fact]
    public async Task Changing_priority_of_unknown_todo_returns_not_found()
    {
        var response = await factory.CreateClient().PutAsJsonAsync("/api/todos/9999/priority", new { priority = "alta" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("{\"priority\":\"urgente\"}")]
    [InlineData("{}")]
    public async Task Invalid_or_missing_priority_on_change_is_rejected(string body)
    {
        var client = factory.CreateClient();
        var todo = (await (await client.PostAsJsonAsync("/api/todos", new { title = "uova", priority = "alta" })).Content.ReadFromJsonAsync<Todo>())!;

        var response = await client.PutAsync($"/api/todos/{todo.Id}/priority", new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("alta", (await client.GetFromJsonAsync<Todo>($"/api/todos/{todo.Id}"))!.Priority);
    }

    [Fact]
    public async Task List_can_be_filtered_by_priority_keeping_id_order()
    {
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/todos", new { title = "a", priority = "alta" });
        await client.PostAsJsonAsync("/api/todos", new { title = "b", priority = "bassa" });
        await client.PostAsJsonAsync("/api/todos", new { title = "c", priority = "alta" });

        var all = (await client.GetFromJsonAsync<List<Todo>>("/api/todos"))!;
        var filtered = (await client.GetFromJsonAsync<List<Todo>>("/api/todos?priority=alta"))!;

        Assert.Contains(all, t => t.Priority == "bassa");
        Assert.Equal(all.Where(t => t.Priority == "alta"), filtered);
        Assert.Equal(filtered.OrderBy(t => t.Id), filtered);
    }

    [Fact]
    public async Task Invalid_priority_filter_is_rejected()
    {
        var response = await factory.CreateClient().GetAsync("/api/todos?priority=urgente");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
