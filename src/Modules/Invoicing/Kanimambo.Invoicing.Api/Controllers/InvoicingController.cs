using Kanimambo.Invoicing.Application;
using Kanimambo.Invoicing.Application.Visitors;
using Kanimambo.Invoicing.Contracts;
using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Results;
using Kanimambo.SharedKernel.Web;
using Kanimambo.SharedKernel.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kanimambo.Invoicing.Api.Controllers;

public sealed record TaxpayerRequest(string Nuit, string LegalName, string? Address, string Regime);

/// <summary>Fiscal documents and seller fiscal profiles (back-office).</summary>
[Authorize(Policy = AuthorizationPolicies.Backoffice)]
[Route("api/invoicing")]
public sealed class InvoicingController(IMediator mediator) : ApiControllerBase
{
    /// <summary>The invoices and credit notes of an order (one per seller, plus the delivery invoice).</summary>
    [HttpGet("documents")]
    [ProducesResponseType<IReadOnlyList<FiscalDocumentDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ForOrder([FromQuery] Guid orderId, CancellationToken cancellationToken) =>
        OkOrProblem(await mediator.Send(new ListDocumentsForOrder(orderId), cancellationToken));

    [HttpGet("documents/{id:guid}")]
    [ProducesResponseType<FiscalDocumentDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        OkOrProblem(await mediator.Send(new GetDocument(id), cancellationToken));

    [HttpGet("documents/{id:guid}/pdf")]
    [Produces("application/pdf")]
    public async Task<IActionResult> Pdf(Guid id, CancellationToken cancellationToken) =>
        FileOrProblem(await mediator.Send(new GetDocumentPdf(id), cancellationToken));

    /// <summary>The monthly file of one issuer for the AT's e-Declaração portal: <c>format</c> is csv, xlsx or xml.</summary>
    [HttpGet("e-declaracao/{nuit}/{year:int}/{month:int}")]
    public async Task<IActionResult> Declaration(string nuit, int year, int month, [FromQuery] string format = "csv", CancellationToken cancellationToken = default) =>
        FileOrProblem(await mediator.Send(new ExportMonthlyDeclaration(nuit, year, month, format), cancellationToken));

    /// <summary>IVA per rate in the month (credit notes subtracted).</summary>
    [HttpGet("iva-summary/{nuit}/{year:int}/{month:int}")]
    [ProducesResponseType<IvaSummary>(StatusCodes.Status200OK)]
    public async Task<IActionResult> IvaSummary(string nuit, int year, int month, CancellationToken cancellationToken) =>
        OkOrProblem(await mediator.Send(new GetIvaSummary(nuit, year, month), cancellationToken));

    /// <summary>Register a seller's NUIT and regime; invoices waiting for it are issued in the same call.</summary>
    [HttpPut("taxpayers/{sellerId:guid}")]
    [ProducesResponseType<TaxpayerDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> RegisterTaxpayer(Guid sellerId, TaxpayerRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OkOrProblem(await mediator.Send(new RegisterTaxpayer(sellerId, request.Nuit, request.LegalName, request.Address, request.Regime), cancellationToken));
    }

    [HttpGet("taxpayers/{sellerId:guid}")]
    [ProducesResponseType<TaxpayerDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Taxpayer(Guid sellerId, CancellationToken cancellationToken) =>
        OkOrProblem(await mediator.Send(new GetTaxpayer(sellerId), cancellationToken));

    /// <summary>Invoices that could not be issued yet (<c>AwaitingTaxpayer</c>) or need a human (<c>Rejected</c>).</summary>
    [HttpGet("requests")]
    [ProducesResponseType<IReadOnlyList<InvoiceRequestDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Requests([FromQuery] string status = "AwaitingTaxpayer", CancellationToken cancellationToken = default) =>
        OkOrProblem(await mediator.Send(new ListInvoiceRequests(status), cancellationToken));

    /// <summary>The IVA rates in force on a date (today by default), from the cached rate table.</summary>
    [AllowAnonymous]
    [HttpGet("tax-rates")]
    [ProducesResponseType<IReadOnlyList<TaxRate>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> TaxRates([FromQuery] DateOnly? on, CancellationToken cancellationToken) =>
        OkOrProblem(await mediator.Send(new ListTaxRates(on), cancellationToken));

    private IActionResult FileOrProblem(Result<ExportedFile> result) =>
        result.IsSuccess ? File(result.Value.Content, result.Value.ContentType, result.Value.FileName) : ProblemFrom(result.Error);
}
