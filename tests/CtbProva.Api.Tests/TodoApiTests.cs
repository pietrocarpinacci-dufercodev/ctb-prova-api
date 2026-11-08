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
    public async Task Priority_defaults_to_media_and_can_be_set_on_creation_case_insensitive()
    {
        var client = factory.CreateClient();

        var plain = await (await client.PostAsJsonAsync("/api/todos", new { title = "a" })).Content.ReadFromJsonAsync<Todo>();
        Assert.Equal("media", plain!.Priority);

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
    public async Task Priority_can_be_changed_with_patch()
    {
        var client = factory.CreateClient();
        var todo = await (await client.PostAsJsonAsync("/api/todos", new { title = "c" })).Content.ReadFromJsonAsync<Todo>();

        var ok = await client.PatchAsJsonAsync($"/api/todos/{todo!.Id}/priority", new { priority = "Bassa" });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var changed = await ok.Content.ReadFromJsonAsync<Todo>();
        Assert.Equal("bassa", changed!.Priority);
        Assert.Equal("c", changed.Title);
        Assert.Equal("bassa", (await client.GetFromJsonAsync<Todo>($"/api/todos/{todo.Id}"))!.Priority);

        var bad = await client.PatchAsJsonAsync($"/api/todos/{todo.Id}/priority", new { priority = "boh" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

        var missing = await client.PatchAsJsonAsync("/api/todos/9999/priority", new { priority = "alta" });
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task List_can_be_filtered_by_priority_and_stays_ordered_by_id()
    {
        var client = factory.CreateClient();
        var first = await (await client.PostAsJsonAsync("/api/todos", new { title = "f1", priority = "alta" })).Content.ReadFromJsonAsync<Todo>();
        await client.PostAsJsonAsync("/api/todos", new { title = "f2", priority = "bassa" });
        var third = await (await client.PostAsJsonAsync("/api/todos", new { title = "f3", priority = "alta" })).Content.ReadFromJsonAsync<Todo>();

        var high = await client.GetFromJsonAsync<List<Todo>>("/api/todos?priority=ALTA");
        Assert.All(high!, t => Assert.Equal("alta", t.Priority));
        var ids = high!.Select(t => t.Id).ToList();
        Assert.Contains(first!.Id, ids);
        Assert.Contains(third!.Id, ids);
        Assert.Equal(ids.OrderBy(i => i), ids);

        var all = await client.GetFromJsonAsync<List<Todo>>("/api/todos");
        Assert.True(all!.Count > high.Count);

        var bad = await client.GetAsync("/api/todos?priority=boh");
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }

    [Fact]
    public async Task Unknown_todo_returns_not_found()
    {
        var response = await factory.CreateClient().GetAsync("/api/todos/9999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
