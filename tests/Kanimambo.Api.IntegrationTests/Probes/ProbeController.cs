using System.Diagnostics.CodeAnalysis;
using Kanimambo.SharedKernel.Results;
using Kanimambo.SharedKernel.Web;
using Kanimambo.SharedKernel.Web.Idempotency;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kanimambo.Api.IntegrationTests.Probes;

/// <summary>Test-only endpoints that stand in for module controllers.</summary>
[Route("api/_probe")]
public sealed class ProbeController(ProbeState state) : ApiControllerBase
{
    [HttpPost("counter/{bucket}")]
    public IActionResult Increment(string bucket, CounterInput input) =>
        CreatedAtAction(nameof(GetCounter), new { bucket }, new CounterOutput(bucket, state.Increment(bucket), input.Note));

    [HttpGet("counter/{bucket}")]
    public IActionResult GetCounter(string bucket) => Ok(new CounterOutput(bucket, state.Count(bucket), null));

    [HttpPost("required/{bucket}")]
    [RequiresIdempotencyKey]
    public IActionResult Required(string bucket)
    {
        state.Increment(bucket);
        return NoContent();
    }

    [HttpPost("flaky/{bucket}")]
    public IActionResult Flaky(string bucket) =>
        state.Increment(bucket) == 1
            ? StatusCode(StatusCodes.Status503ServiceUnavailable)
            : Ok(new CounterOutput(bucket, state.Count(bucket), "recovered"));

    [HttpPost("slow/{bucket}")]
    public async Task<IActionResult> Slow(string bucket, CancellationToken cancellationToken)
    {
        state.Increment(bucket);
        await state.Gate(bucket).Task.WaitAsync(cancellationToken);
        return Ok(new CounterOutput(bucket, state.Count(bucket), "slow"));
    }

    [HttpPost("model")]
    public IActionResult Model(RequiredNoteInput input) => Ok(input);

    [HttpGet("boom")]
    [SuppressMessage("Performance", "CA1822", Justification = "MVC actions must be instance methods.")]
    public IActionResult Boom() => throw new InvalidOperationException("Host=prod-db;Password=s3cr3t");

    [HttpGet("not-found")]
    public IActionResult MissingProduct() =>
        ProblemFrom(Error.NotFound("catalog.product_not_found", "Product 42 does not exist."));

    [HttpGet("business-rule")]
    public IActionResult BelowMinimum() =>
        ProblemFrom(Error.BusinessRule("ordering.below_seller_minimum", "Order is below the seller minimum of 500.00 MZN."));

    [HttpGet("invalid")]
    public IActionResult Invalid() =>
        ProblemFrom(Error.Validation(
            new FieldError("nuit", "NUIT must have 9 digits."),
            new FieldError("phone", "Phone must use the 258XXXXXXXXX format.")));
}
