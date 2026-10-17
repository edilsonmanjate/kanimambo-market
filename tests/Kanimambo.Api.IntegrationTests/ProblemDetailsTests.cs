using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Kanimambo.Api.IntegrationTests;

public sealed class ProblemDetailsTests(KanimamboApiFactory factory)
{
    private const string ProblemJson = "application/problem+json";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Unhandled_exceptions_become_a_500_problem_without_leaking_details()
    {
        var response = await _client.GetAsync(new Uri("/api/_probe/boom", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType!.MediaType.ShouldBe(ProblemJson);

        var raw = await response.Content.ReadAsStringAsync(Ct);
        raw.ShouldNotContain("s3cr3t");

        var problem = JsonDocument.Parse(raw).RootElement;
        problem.GetProperty("code").GetString().ShouldBe("server.unexpected_error");
        problem.TryGetProperty("traceId", out _).ShouldBeTrue();
    }

    [Theory]
    [InlineData("not-found", HttpStatusCode.NotFound, "catalog.product_not_found")]
    [InlineData("business-rule", (HttpStatusCode)422, "ordering.below_seller_minimum")]
    public async Task Business_errors_map_to_status_codes_with_a_stable_code(string path, HttpStatusCode status, string code)
    {
        var response = await _client.GetAsync(new Uri($"/api/_probe/{path}", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(status);
        response.Content.Headers.ContentType!.MediaType.ShouldBe(ProblemJson);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        problem.GetProperty("code").GetString().ShouldBe(code);
        problem.GetProperty("status").GetInt32().ShouldBe((int)status);
        problem.GetProperty("detail").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Validation_errors_list_every_field()
    {
        var response = await _client.GetAsync(new Uri("/api/_probe/invalid", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        problem.GetProperty("code").GetString().ShouldBe("validation.failed");

        var errors = problem.GetProperty("errors");
        errors.GetProperty("nuit")[0].GetString().ShouldBe("NUIT must have 9 digits.");
        errors.TryGetProperty("phone", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Model_binding_failures_use_the_same_problem_shape()
    {
        var response = await _client.PostAsJsonAsync("/api/_probe/model", new { }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe(ProblemJson);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        problem.GetProperty("code").GetString().ShouldBe("validation.failed");
        problem.GetProperty("errors").TryGetProperty("Note", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Unknown_routes_return_a_problem_too()
    {
        var response = await _client.GetAsync(new Uri("/api/does-not-exist", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.ShouldBe(ProblemJson);
    }
}
