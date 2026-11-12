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
    public async Task Created_todo_keeps_valid_priority()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/todos", new { title = "urgente", priority = "alta" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var todo = await response.Content.ReadFromJsonAsync<Todo>();
        Assert.Equal("alta", todo!.Priority);
    }

    [Fact]
    public async Task Created_todo_without_priority_is_media()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/todos", new { title = "normale" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var todo = await response.Content.ReadFromJsonAsync<Todo>();
        Assert.Equal("media", todo!.Priority);
    }

    [Theory]
    [InlineData("Alta")]
    [InlineData("high")]
    [InlineData("")]
    public async Task Invalid_priority_on_create_is_rejected_and_not_saved(string priority)
    {
        var client = factory.CreateClient();
        var title = $"non salvare {Guid.NewGuid()}";

        var response = await client.PostAsJsonAsync("/api/todos", new { title, priority });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("priority", await response.Content.ReadAsStringAsync());
        var all = await client.GetFromJsonAsync<List<Todo>>("/api/todos");
        Assert.DoesNotContain(all!, t => t.Title == title);
    }

    [Fact]
    public async Task Priority_endpoint_updates_only_priority()
    {
        var client = factory.CreateClient();
        var created = await (await client.PostAsJsonAsync("/api/todos", new { title = "da cambiare" })).Content.ReadFromJsonAsync<Todo>();

        var response = await client.PutAsJsonAsync($"/api/todos/{created!.Id}/priority", new { priority = "bassa" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(created with { Priority = "bassa" }, await response.Content.ReadFromJsonAsync<Todo>());
        Assert.Equal("bassa", (await client.GetFromJsonAsync<Todo>($"/api/todos/{created.Id}"))!.Priority);
    }

    [Theory]
    [InlineData("""{"priority":"urgente"}""")]
    [InlineData("""{"priority":"Alta"}""")]
    [InlineData("{}")]
    public async Task Priority_endpoint_rejects_invalid_or_missing_value(string body)
    {
        var client = factory.CreateClient();
        var created = await (await client.PostAsJsonAsync("/api/todos", new { title = "resta media" })).Content.ReadFromJsonAsync<Todo>();

        var response = await client.PutAsync($"/api/todos/{created!.Id}/priority", new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("media", (await client.GetFromJsonAsync<Todo>($"/api/todos/{created.Id}"))!.Priority);
    }

    [Fact]
    public async Task Priority_endpoint_returns_not_found_for_unknown_todo()
    {
        var response = await factory.CreateClient().PutAsJsonAsync("/api/todos/9999/priority", new { priority = "alta" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task List_can_be_filtered_by_priority()
    {
        var client = factory.CreateClient();
        var high = await (await client.PostAsJsonAsync("/api/todos", new { title = "filtro alta", priority = "alta" })).Content.ReadFromJsonAsync<Todo>();
        var low = await (await client.PostAsJsonAsync("/api/todos", new { title = "filtro bassa", priority = "bassa" })).Content.ReadFromJsonAsync<Todo>();

        var filtered = await client.GetFromJsonAsync<List<Todo>>("/api/todos?priority=alta");
        var all = await client.GetFromJsonAsync<List<Todo>>("/api/todos");

        // Lo store è condiviso tra i test: niente conteggi esatti.
        Assert.All(filtered!, t => Assert.Equal("alta", t.Priority));
        Assert.Contains(high, filtered!);
        Assert.DoesNotContain(low, filtered!);
        Assert.Contains(high, all!);
        Assert.Contains(low, all!);
        Assert.Equal(all!.Select(t => t.Id).Order(), all!.Select(t => t.Id));
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
