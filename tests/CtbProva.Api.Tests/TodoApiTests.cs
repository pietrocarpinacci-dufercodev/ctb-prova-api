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
        var response = await factory.CreateClient().PostAsJsonAsync("/api/todos", new { title = "urgente", priority = "alta" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var todo = await response.Content.ReadFromJsonAsync<Todo>();
        Assert.Equal("alta", todo!.Priority);
    }

    [Fact]
    public async Task Created_todo_defaults_to_media_priority()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/todos", new { title = "normale" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var todo = await response.Content.ReadFromJsonAsync<Todo>();
        Assert.Equal("media", todo!.Priority);
    }

    [Fact]
    public async Task Invalid_priority_on_create_is_rejected()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/todos", new { title = "priorita sbagliata", priority = "urgentissima" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("priority", await response.Content.ReadAsStringAsync());
        var all = await client.GetFromJsonAsync<List<Todo>>("/api/todos");
        Assert.DoesNotContain(all!, t => t.Title == "priorita sbagliata");
    }

    [Fact]
    public async Task Priority_can_be_changed()
    {
        var client = factory.CreateClient();
        var created = await (await client.PostAsJsonAsync("/api/todos", new { title = "da cambiare" })).Content.ReadFromJsonAsync<Todo>();

        var response = await client.PutAsJsonAsync($"/api/todos/{created!.Id}/priority", new { priority = "bassa" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("bassa", (await response.Content.ReadFromJsonAsync<Todo>())!.Priority);
        Assert.Equal("bassa", (await client.GetFromJsonAsync<Todo>($"/api/todos/{created.Id}"))!.Priority);
    }

    [Fact]
    public async Task Changing_priority_of_unknown_todo_returns_not_found()
    {
        var response = await factory.CreateClient().PutAsJsonAsync("/api/todos/9999/priority", new { priority = "alta" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("{\"priority\":\"massima\"}")]
    [InlineData("{}")]
    public async Task Invalid_or_missing_priority_on_change_is_rejected(string body)
    {
        var client = factory.CreateClient();
        var created = await (await client.PostAsJsonAsync("/api/todos", new { title = "fisso", priority = "alta" })).Content.ReadFromJsonAsync<Todo>();

        var response = await client.PutAsync($"/api/todos/{created!.Id}/priority", new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("alta", (await client.GetFromJsonAsync<Todo>($"/api/todos/{created.Id}"))!.Priority);
    }

    [Fact]
    public async Task Todos_can_be_filtered_by_priority()
    {
        var client = factory.CreateClient();
        var low = await (await client.PostAsJsonAsync("/api/todos", new { title = "piano", priority = "bassa" })).Content.ReadFromJsonAsync<Todo>();
        var high = await (await client.PostAsJsonAsync("/api/todos", new { title = "forte", priority = "alta" })).Content.ReadFromJsonAsync<Todo>();

        var filtered = (await client.GetFromJsonAsync<List<Todo>>("/api/todos?priority=bassa"))!;

        Assert.All(filtered, t => Assert.Equal("bassa", t.Priority));
        Assert.Contains(filtered, t => t.Id == low!.Id);
        Assert.DoesNotContain(filtered, t => t.Id == high!.Id);
        Assert.Equal(filtered.Select(t => t.Id).Order(), filtered.Select(t => t.Id));
    }

    [Fact]
    public async Task Todos_without_filter_include_every_priority()
    {
        var client = factory.CreateClient();
        var low = await (await client.PostAsJsonAsync("/api/todos", new { title = "tutti bassa", priority = "bassa" })).Content.ReadFromJsonAsync<Todo>();
        var high = await (await client.PostAsJsonAsync("/api/todos", new { title = "tutti alta", priority = "alta" })).Content.ReadFromJsonAsync<Todo>();

        var all = (await client.GetFromJsonAsync<List<Todo>>("/api/todos"))!;

        Assert.Contains(all, t => t.Id == low!.Id);
        Assert.Contains(all, t => t.Id == high!.Id);
        Assert.Equal(all.Select(t => t.Id).Order(), all.Select(t => t.Id));
    }

    [Fact]
    public async Task Invalid_priority_filter_is_rejected()
    {
        var response = await factory.CreateClient().GetAsync("/api/todos?priority=altissima");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
