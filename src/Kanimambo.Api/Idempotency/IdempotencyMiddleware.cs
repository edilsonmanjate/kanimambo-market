using System.Security.Cryptography;
using System.Text;
using Kanimambo.SharedKernel.Web;
using Kanimambo.SharedKernel.Web.Idempotency;
using Microsoft.AspNetCore.Mvc;

namespace Kanimambo.Api.Idempotency;

/// <summary>
/// Makes write requests safe to retry over unstable mobile networks: the first request with a given
/// <c>Idempotency-Key</c> is executed and its response stored; retries get the stored response back
/// (with <c>Idempotent-Replayed: true</c>) instead of executing twice.
/// <list type="bullet">
/// <item>Same key + different payload → 422 (key reuse is a client bug).</item>
/// <item>Same key while the first request is still running → 409 (retry later).</item>
/// <item>5xx responses and exceptions are not stored, so the client can retry.</item>
/// </list>
/// </summary>
internal sealed class IdempotencyMiddleware(RequestDelegate next, IProblemDetailsService problemDetailsService)
{
    public async Task InvokeAsync(HttpContext context, IdempotencyStore store)
    {
        if (!IsWrite(context.Request.Method))
        {
            await next(context);
            return;
        }

        var keyRequired = context.GetEndpoint()?.Metadata.GetMetadata<RequiresIdempotencyKeyAttribute>() is not null;
        var keyValues = context.Request.Headers[IdempotencyHeaders.Key];

        if (keyValues.Count == 0)
        {
            if (keyRequired)
            {
                await WriteProblemAsync(context, StatusCodes.Status400BadRequest, "idempotency.key_missing",
                    $"This endpoint requires an '{IdempotencyHeaders.Key}' header.");
                return;
            }

            await next(context);
            return;
        }

        var key = keyValues.ToString();
        if (keyValues.Count > 1 || string.IsNullOrWhiteSpace(key) || key.Length > IdempotencyDbContext.MaxKeyLength)
        {
            await WriteProblemAsync(context, StatusCodes.Status400BadRequest, "idempotency.key_invalid",
                $"'{IdempotencyHeaders.Key}' must be a single value of 1 to {IdempotencyDbContext.MaxKeyLength} characters.");
            return;
        }

        // A key is scoped to the endpoint and the caller: two customers who happen to send the same key never get
        // each other's stored response. Anonymous callers (guest carts) share one scope, as before.
        var caller = context.User.Identity is { IsAuthenticated: true, Name: { Length: > 0 } name } ? name : "-";
        var scope = $"{context.Request.Method} {context.Request.Path} {caller}";
        var fingerprint = await ComputeFingerprintAsync(context.Request, context.RequestAborted);
        var begin = await store.TryBeginAsync(scope, key, fingerprint, context.RequestAborted);

        switch (begin.Outcome)
        {
            case BeginOutcome.FingerprintMismatch:
                await WriteProblemAsync(context, StatusCodes.Status422UnprocessableEntity, "idempotency.key_reused",
                    "This Idempotency-Key was already used with a different request payload.");
                return;
            case BeginOutcome.InFlight:
                await WriteProblemAsync(context, StatusCodes.Status409Conflict, "idempotency.request_in_progress",
                    "A request with this Idempotency-Key is still being processed. Retry later.");
                return;
            case BeginOutcome.Replay:
                await ReplayAsync(context, begin.Existing!);
                return;
            case BeginOutcome.Started:
            default:
                await ExecuteAndStoreAsync(context, store, scope, key);
                return;
        }
    }

    private async Task ExecuteAndStoreAsync(HttpContext context, IdempotencyStore store, string scope, string key)
    {
        var originalBody = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        var stored = false;
        try
        {
            await next(context);

            var response = context.Response;
            if (response.StatusCode < StatusCodes.Status500InternalServerError)
            {
                // Not tied to RequestAborted: a client that disconnected must still find the result on retry.
                await store.CompleteAsync(scope, key, response.StatusCode, response.ContentType,
                    response.Headers.Location.ToString() is { Length: > 0 } location ? location : null,
                    buffer.ToArray(), CancellationToken.None);
                stored = true;
            }
        }
        finally
        {
            context.Response.Body = originalBody;
            if (!stored)
            {
                await store.ReleaseAsync(scope, key, CancellationToken.None);
            }
        }

        buffer.Position = 0;
        await buffer.CopyToAsync(originalBody, context.RequestAborted);
    }

    private static async Task ReplayAsync(HttpContext context, IdempotencyRecord record)
    {
        var response = context.Response;
        response.StatusCode = record.ResponseStatusCode ?? StatusCodes.Status200OK;
        response.Headers[IdempotencyHeaders.Replayed] = "true";

        if (record.ResponseContentType is not null)
        {
            response.ContentType = record.ResponseContentType;
        }

        if (record.ResponseLocation is not null)
        {
            response.Headers.Location = record.ResponseLocation;
        }

        if (record.ResponseBody is { Length: > 0 } body)
        {
            await response.Body.WriteAsync(body, context.RequestAborted);
        }
    }

    private static async Task<string> ComputeFingerprintAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        request.EnableBuffering();

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes(request.QueryString.Value ?? string.Empty));

        var chunk = new byte[8192];
        int read;
        while ((read = await request.Body.ReadAsync(chunk, cancellationToken)) > 0)
        {
            hash.AppendData(chunk, 0, read);
        }

        request.Body.Position = 0;
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private async Task WriteProblemAsync(HttpContext context, int status, string code, string detail)
    {
        context.Response.StatusCode = status;
        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Detail = detail,
                Extensions = { [ApiControllerBase.ErrorCodeExtension] = code },
            },
        });
    }

    private static bool IsWrite(string method) =>
        HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method);
}
