using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kanimambo.Api.IntegrationTests.Probes;
using Kanimambo.SharedKernel.Web.Idempotency;
using Microsoft.Extensions.DependencyInjection;

namespace Kanimambo.Api.IntegrationTests;

public sealed class IdempotencyTests(KanimamboApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly HttpClient _client = factory.CreateClient();

    private ProbeState State => factory.Services.GetRequiredService<ProbeState>();

    [Fact]
    public async Task Retrying_with_the_same_key_replays_the_first_response_without_executing_again()
    {
        var bucket = NewBucket();
        var key = Guid.NewGuid().ToString();

        var first = await PostAsync($"counter/{bucket}", new CounterInput("cabaz do mês"), key);
        var retry = await PostAsync($"counter/{bucket}", new CounterInput("cabaz do mês"), key);

        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        retry.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await retry.Content.ReadAsStringAsync(Ct)).ShouldBe(await first.Content.ReadAsStringAsync(Ct));
        retry.Headers.Location.ShouldBe(first.Headers.Location);
        retry.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
        retry.Headers.GetValues(IdempotencyHeaders.Replayed).ShouldBe(["true"]);
        first.Headers.Contains(IdempotencyHeaders.Replayed).ShouldBeFalse();
        State.Count(bucket).ShouldBe(1);
    }

    [Fact]
    public async Task Without_a_key_every_request_executes()
    {
        var bucket = NewBucket();

        await PostAsync($"counter/{bucket}", new CounterInput(null));
        await PostAsync($"counter/{bucket}", new CounterInput(null));

        State.Count(bucket).ShouldBe(2);
    }

    [Fact]
    public async Task Reusing_a_key_with_a_different_payload_is_rejected()
    {
        var bucket = NewBucket();
        var key = Guid.NewGuid().ToString();

        var original = await PostAsync($"counter/{bucket}", new CounterInput("2 sacos"), key);
        var reused = await PostAsync($"counter/{bucket}", new CounterInput("5 sacos"), key);

        original.StatusCode.ShouldBe(HttpStatusCode.Created, await original.Content.ReadAsStringAsync(Ct));
        reused.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await CodeOf(reused)).ShouldBe("idempotency.key_reused");
        State.Count(bucket).ShouldBe(1);
    }

    [Fact]
    public async Task The_same_key_on_another_endpoint_is_independent()
    {
        var bucketA = NewBucket();
        var bucketB = NewBucket();
        var key = Guid.NewGuid().ToString();

        await PostAsync($"counter/{bucketA}", new CounterInput(null), key);
        var other = await PostAsync($"counter/{bucketB}", new CounterInput(null), key);

        other.StatusCode.ShouldBe(HttpStatusCode.Created);
        State.Count(bucketB).ShouldBe(1);
    }

    [Fact]
    public async Task The_same_key_from_two_customers_never_shares_a_response()
    {
        var bucket = NewBucket();
        var key = Guid.NewGuid().ToString();

        var ana = await PostAsync($"counter/{bucket}", new CounterInput("cabaz do mês"), key, factory.CreateClientAs("ana"));
        var rui = await PostAsync($"counter/{bucket}", new CounterInput("cabaz do mês"), key, factory.CreateClientAs("rui"));
        var anaAgain = await PostAsync($"counter/{bucket}", new CounterInput("cabaz do mês"), key, factory.CreateClientAs("ana"));

        rui.Headers.Contains(IdempotencyHeaders.Replayed).ShouldBeFalse();
        (await rui.Content.ReadAsStringAsync(Ct)).ShouldNotBe(await ana.Content.ReadAsStringAsync(Ct));
        anaAgain.Headers.GetValues(IdempotencyHeaders.Replayed).ShouldBe(["true"]);
        State.Count(bucket).ShouldBe(2);
    }

    [Fact]
    public async Task Endpoints_that_require_a_key_reject_requests_without_one()
    {
        var bucket = NewBucket();

        var missing = await PostAsync($"required/{bucket}", body: null);
        var withKey = await PostAsync($"required/{bucket}", body: null, key: Guid.NewGuid().ToString());

        missing.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await CodeOf(missing)).ShouldBe("idempotency.key_missing");
        withKey.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        State.Count(bucket).ShouldBe(1);
    }

    [Fact]
    public async Task Server_errors_are_not_stored_so_the_client_can_retry()
    {
        var bucket = NewBucket();
        var key = Guid.NewGuid().ToString();

        var failed = await PostAsync($"flaky/{bucket}", body: null, key);
        var retried = await PostAsync($"flaky/{bucket}", body: null, key);
        var replayed = await PostAsync($"flaky/{bucket}", body: null, key);

        failed.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        retried.StatusCode.ShouldBe(HttpStatusCode.OK);
        replayed.Headers.Contains(IdempotencyHeaders.Replayed).ShouldBeTrue();
        State.Count(bucket).ShouldBe(2);
    }

    [Fact]
    public async Task A_retry_while_the_first_request_is_still_running_gets_a_conflict()
    {
        var bucket = NewBucket();
        var key = Guid.NewGuid().ToString();

        var first = PostAsync($"slow/{bucket}", body: null, key);
        await WaitUntilAsync(() => State.Count(bucket) == 1);

        var concurrent = await PostAsync($"slow/{bucket}", body: null, key);
        State.Gate(bucket).SetResult();
        var completed = await first;

        concurrent.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CodeOf(concurrent)).ShouldBe("idempotency.request_in_progress");
        completed.StatusCode.ShouldBe(HttpStatusCode.OK);
        State.Count(bucket).ShouldBe(1);
    }

    [Fact]
    public async Task Keys_longer_than_the_limit_are_rejected()
    {
        var response = await PostAsync($"counter/{NewBucket()}", new CounterInput(null), new string('k', 129));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await CodeOf(response)).ShouldBe("idempotency.key_invalid");
    }

    private async Task<HttpResponseMessage> PostAsync(string path, object? body, string? key = null, HttpClient? client = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri($"/api/_probe/{path}", UriKind.Relative))
        {
            Content = body is null ? null : JsonContent.Create(body),
        };

        if (key is not null)
        {
            request.Headers.Add(IdempotencyHeaders.Key, key);
        }

        return await (client ?? _client).SendAsync(request, Ct);
    }

    private static async Task<string?> CodeOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString();

    private static string NewBucket() => Guid.NewGuid().ToString("N");

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        while (!condition())
        {
            await Task.Delay(20, timeout.Token);
        }
    }
}
