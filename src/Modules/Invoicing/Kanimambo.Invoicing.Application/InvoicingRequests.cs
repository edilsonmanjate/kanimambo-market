using Kanimambo.Catalog.Contracts;
using Kanimambo.Invoicing.Application.Regimes;
using Kanimambo.Invoicing.Application.Visitors;
using Kanimambo.Invoicing.Contracts;
using Kanimambo.Invoicing.Domain;
using Kanimambo.Ordering.Contracts;
using Kanimambo.SharedKernel.Features;
using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Results;
using Kanimambo.SharedKernel.Time;

namespace Kanimambo.Invoicing.Application;

public static class InvoicingModule
{
    public const string Name = "Invoicing";
}

/// <summary>
/// Invoicing settings from configuration. The marketplace itself issues the delivery invoice (it sells the delivery
/// service); <see cref="FallbackTaxCategory"/> applies to a product the catalog no longer knows.
/// </summary>
public sealed record InvoicingSettings(FiscalParty Marketplace, FiscalRegime MarketplaceRegime, string DeliveryTaxCategory, string DeliveryDescription,
    string FallbackTaxCategory);

public interface IFiscalDocumentRepository
{
    void Add(FiscalDocument document);

    Task<FiscalDocument?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<FiscalDocument>> ListByOrderAsync(Guid orderId, CancellationToken cancellationToken);

    Task<bool> IsCreditedAsync(Guid invoiceId, CancellationToken cancellationToken);

    /// <summary>Invoices and credit notes the issuer issued in the month.</summary>
    Task<IReadOnlyList<FiscalDocument>> ListForPeriodAsync(string issuerNuit, int year, int month, CancellationToken cancellationToken);
}

public interface ITaxpayerRepository
{
    void Add(TaxpayerProfile profile);

    Task<TaxpayerProfile?> GetAsync(Guid sellerId, CancellationToken cancellationToken);

    Task<TaxpayerProfile?> FindByNuitAsync(string nuit, CancellationToken cancellationToken);
}

public interface IInvoiceRequestRepository
{
    void Add(InvoiceRequest request);

    Task<IReadOnlyList<InvoiceRequest>> ListByOrderAsync(Guid orderId, CancellationToken cancellationToken);

    Task<IReadOnlyList<InvoiceRequest>> ListAwaitingAsync(Guid sellerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<InvoiceRequest>> ListByStatusAsync(InvoiceRequestStatus status, CancellationToken cancellationToken);
}

/// <summary>Gapless numbering: the counter is tracked by the unit of work and saved with the document.</summary>
public interface IDocumentNumbering
{
    Task<DocumentCounter> CounterAsync(string issuerNuit, string series, int year, CancellationToken cancellationToken);
}

public sealed record ExportedFile(string FileName, string ContentType, byte[] Content);

/// <summary>Monthly file for the AT's e-Declaração portal, one visitor per format.</summary>
public interface IMonthlyDeclarationExporter
{
    IReadOnlyCollection<string> Formats { get; }

    Result<ExportedFile> Export(FiscalPeriod period, string format);
}

public interface IFiscalDocumentPdfRenderer
{
    ExportedFile Render(FiscalDocument document);
}

public sealed record FiscalLineDto(int LineNumber, string Description, string? Sku, decimal Quantity, string TaxCategory, decimal Rate,
    decimal Discount, decimal Net, decimal Iva, decimal Gross, string? ExemptionReason);

public sealed record FiscalDocumentDto(
    Guid Id,
    string Type,
    string Title,
    string Number,
    string Regime,
    DateOnly IssueDate,
    string IssuerNuit,
    string IssuerName,
    string? BuyerNuit,
    string BuyerName,
    Guid OrderId,
    long OrderNumber,
    Guid? SellerId,
    decimal Net,
    decimal Iva,
    decimal Gross,
    string? LegalNotice,
    string? CreditedInvoiceNumber,
    IReadOnlyList<FiscalLineDto> Lines)
{
    public static FiscalDocumentDto From(FiscalDocument d)
    {
        ArgumentNullException.ThrowIfNull(d);
        return new(d.Id, d.Type.ToString(), d.Title, d.FullNumber, d.Regime.ToString(), d.IssueDate, d.Issuer.Nuit!, d.Issuer.Name, d.Buyer.Nuit, d.Buyer.Name,
            d.OrderId, d.OrderNumber, d.SellerId, d.Net, d.Iva, d.Gross, d.LegalNotice, (d as CreditNote)?.CreditedInvoiceNumber,
            [.. d.Lines.Select(l => new FiscalLineDto(l.LineNumber, l.Description, l.Sku, l.Quantity, l.TaxCategory, l.Rate, l.Discount, l.Net, l.Iva, l.Gross, l.ExemptionReason))]);
    }
}

public sealed record InvoiceRequestDto(Guid Id, Guid OrderId, long OrderNumber, Guid SellerId, string Status, Guid? InvoiceId, string? Problem);

public sealed record TaxpayerDto(Guid SellerId, string Nuit, string LegalName, string? Address, string Regime, int InvoicesIssuedNow);

/// <summary>Issue the invoices of a paid order: one per seller (goods) plus the marketplace's delivery invoice.</summary>
public sealed record IssueInvoicesForOrder(
    Guid IdempotencyKey,
    Guid OrderId,
    long OrderNumber,
    string CustomerId,
    string CustomerType,
    string CustomerNuit,
    string CustomerPhone,
    decimal DiscountMzn,
    decimal ShippingFeeMzn,
    string DeliveryMethod,
    IReadOnlyList<OrderLineContract> Lines) : IIdempotentCommand;

/// <summary>Credit every invoice of a refunded order; cancel the ones that were never issued.</summary>
public sealed record CreditRefundedOrder(Guid IdempotencyKey, Guid OrderId, string Reason) : IIdempotentCommand;

/// <summary>Register or update a seller's fiscal data; pending invoices of that seller are issued right away.</summary>
public sealed record RegisterTaxpayer(Guid SellerId, string Nuit, string LegalName, string? Address, string Regime) : ICommand<TaxpayerDto>;

public sealed record GetTaxpayer(Guid SellerId) : IQuery<TaxpayerDto>;

public sealed record ListDocumentsForOrder(Guid OrderId) : IQuery<IReadOnlyList<FiscalDocumentDto>>;

public sealed record GetDocument(Guid DocumentId) : IQuery<FiscalDocumentDto>;

public sealed record GetDocumentPdf(Guid DocumentId) : IQuery<ExportedFile>;

public sealed record ExportMonthlyDeclaration(string IssuerNuit, int Year, int Month, string Format) : IQuery<ExportedFile>;

public sealed record GetIvaSummary(string IssuerNuit, int Year, int Month) : IQuery<IvaSummary>;

public sealed record ListInvoiceRequests(string Status) : IQuery<IReadOnlyList<InvoiceRequestDto>>;

public sealed record ListTaxRates(DateOnly? On) : IQuery<IReadOnlyList<TaxRate>>;

/// <summary>Observer: a paid order gets its invoices (Ordering does not know Invoicing exists).</summary>
public sealed class InvoiceWhenOrderPaid(ISender sender) : INotificationHandler<OrderPaidIntegrationEvent>
{
    public async ValueTask Handle(OrderPaidIntegrationEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        var issued = await sender.Send(new IssueInvoicesForOrder(notification.EventId, notification.OrderId, notification.OrderNumber, notification.CustomerId,
            notification.CustomerType, notification.CustomerNuit, notification.CustomerPhone, notification.DiscountMzn, notification.ShippingFeeMzn,
            notification.DeliveryMethod, notification.Lines), cancellationToken);
        if (issued.IsFailure)
        {
            throw new InvalidOperationException($"Could not invoice order {notification.OrderNumber}: {issued.Error.Message}");
        }
    }
}

/// <summary>Observer: a refunded order gets credit notes.</summary>
public sealed class CreditWhenOrderRefunded(ISender sender) : INotificationHandler<OrderStatusChangedIntegrationEvent>
{
    public async ValueTask Handle(OrderStatusChangedIntegrationEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        if (notification.To != "Refunded")
        {
            return;
        }

        var credited = await sender.Send(new CreditRefundedOrder(notification.EventId, notification.OrderId, notification.Reason ?? "Encomenda reembolsada"), cancellationToken);
        if (credited.IsFailure)
        {
            throw new InvalidOperationException($"Could not credit order {notification.OrderNumber}: {credited.Error.Message}");
        }
    }
}

public sealed class InvoicingHandlers(
    IFiscalDocumentRepository documents,
    ITaxpayerRepository taxpayers,
    IInvoiceRequestRepository requests,
    IDocumentNumbering numbering,
    FiscalRegimeFactories regimes,
    InvoicingSettings settings,
    ICatalogReader catalog,
    ITaxRateTable taxRates,
    IMonthlyDeclarationExporter exporter,
    IFiscalDocumentPdfRenderer pdf,
    IFeatureFlags features,
    TimeProvider timeProvider) :
    IRequestHandler<IssueInvoicesForOrder, Result>,
    IRequestHandler<CreditRefundedOrder, Result>,
    IRequestHandler<RegisterTaxpayer, Result<TaxpayerDto>>,
    IRequestHandler<GetTaxpayer, Result<TaxpayerDto>>,
    IRequestHandler<ListDocumentsForOrder, Result<IReadOnlyList<FiscalDocumentDto>>>,
    IRequestHandler<GetDocument, Result<FiscalDocumentDto>>,
    IRequestHandler<GetDocumentPdf, Result<ExportedFile>>,
    IRequestHandler<ExportMonthlyDeclaration, Result<ExportedFile>>,
    IRequestHandler<GetIvaSummary, Result<IvaSummary>>,
    IRequestHandler<ListInvoiceRequests, Result<IReadOnlyList<InvoiceRequestDto>>>,
    IRequestHandler<ListTaxRates, Result<IReadOnlyList<TaxRate>>>
{
    public async ValueTask<Result> Handle(IssueInvoicesForOrder request, CancellationToken cancellationToken)
    {
        if ((await requests.ListByOrderAsync(request.OrderId, cancellationToken)).Count > 0)
        {
            return Result.Success();
        }

        var now = timeProvider.GetUtcNow();
        var business = request.CustomerType == "b2b";
        var buyer = business
            ? new FiscalParty(request.CustomerNuit is { Length: > 0 } nuit ? nuit : null, request.CustomerId, null)
            : FiscalParty.FinalConsumer(request.CustomerNuit);

        var profiles = (await catalog.GetTaxProfilesAsync([.. request.Lines.Select(l => l.ProductId)], cancellationToken)).ToDictionary(p => p.ProductId);
        var discounts = Allocation.Split(request.DiscountMzn, [.. request.Lines.Select(l => l.LineTotalMzn)]);
        var goods = request.Lines.Select((line, i) => (line.SellerId, Lines: ToPrice(line, discounts[i], profiles.GetValueOrDefault(line.ProductId))));

        var created = new List<InvoiceRequest>();
        foreach (var seller in goods.GroupBy(g => g.SellerId))
        {
            created.Add(InvoiceRequest.Create(request.OrderId, request.OrderNumber, seller.Key, buyer, business, request.CustomerPhone,
                [.. seller.SelectMany(s => s.Lines)], now));
        }

        if (request.ShippingFeeMzn > 0m)
        {
            created.Add(InvoiceRequest.Create(request.OrderId, request.OrderNumber, InvoiceRequest.Marketplace, buyer, business, request.CustomerPhone,
                [new LineToPrice($"{settings.DeliveryDescription} ({request.DeliveryMethod})", null, 1m, settings.DeliveryTaxCategory, request.ShippingFeeMzn)], now));
        }

        foreach (var invoiceRequest in created)
        {
            requests.Add(invoiceRequest);
            await TryIssueAsync(invoiceRequest, cancellationToken);
        }

        return Result.Success();
    }

    public async ValueTask<Result> Handle(CreditRefundedOrder request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        foreach (var invoiceRequest in await requests.ListByOrderAsync(request.OrderId, cancellationToken))
        {
            if (invoiceRequest.Status != InvoiceRequestStatus.Issued)
            {
                invoiceRequest.Cancel(now);
                continue;
            }

            if (await documents.GetAsync(invoiceRequest.InvoiceId!.Value, cancellationToken) is not Invoice invoice
                || await documents.IsCreditedAsync(invoice.Id, cancellationToken))
            {
                continue;
            }

            var kit = regimes.KitFor(invoice.Regime);
            var today = MozambiqueTime.Today(timeProvider);
            var counter = await numbering.CounterAsync(invoice.Issuer.Nuit!, kit.Series.For(DocumentType.CreditNote), today.Year, cancellationToken);
            var credit = CreditNote.ForRefund(invoice, kit, counter.Next, today, request.Reason, now);
            if (credit.IsFailure)
            {
                return credit.Error;
            }

            documents.Add(credit.Value);
        }

        return Result.Success();
    }

    public async ValueTask<Result<TaxpayerDto>> Handle(RegisterTaxpayer request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<FiscalRegime>(request.Regime, ignoreCase: true, out var regime) || !Enum.IsDefined(regime))
        {
            return Error.Validation(new FieldError("regime", $"Unknown regime '{request.Regime}'. Use IvaNormal or Ispc."));
        }

        if (await taxpayers.FindByNuitAsync(request.Nuit.Trim(), cancellationToken) is { } other && other.Id != request.SellerId)
        {
            return Error.Conflict("invoicing.nuit_taken", $"NUIT {request.Nuit} already belongs to another seller.");
        }

        var now = timeProvider.GetUtcNow();
        var profile = await taxpayers.GetAsync(request.SellerId, cancellationToken);
        if (profile is null)
        {
            var registered = TaxpayerProfile.Register(request.SellerId, request.Nuit, request.LegalName, request.Address, regime, now);
            if (registered.IsFailure)
            {
                return registered.Error;
            }

            profile = registered.Value;
            taxpayers.Add(profile);
        }
        else if (profile.Update(request.Nuit, request.LegalName, request.Address, regime, now) is { IsFailure: true } updated)
        {
            return updated.Error;
        }

        var issued = 0;
        foreach (var awaiting in await requests.ListAwaitingAsync(request.SellerId, cancellationToken))
        {
            if (await TryIssueAsync(awaiting, cancellationToken, profile))
            {
                issued++;
            }
        }

        return new TaxpayerDto(profile.Id, profile.Nuit, profile.LegalName, profile.Address, profile.Regime.ToString(), issued);
    }

    public async ValueTask<Result<TaxpayerDto>> Handle(GetTaxpayer request, CancellationToken cancellationToken) =>
        await taxpayers.GetAsync(request.SellerId, cancellationToken) is { } p
            ? new TaxpayerDto(p.Id, p.Nuit, p.LegalName, p.Address, p.Regime.ToString(), 0)
            : Error.NotFound("invoicing.taxpayer_not_found", $"Seller {request.SellerId} has no fiscal profile.");

    public async ValueTask<Result<IReadOnlyList<FiscalDocumentDto>>> Handle(ListDocumentsForOrder request, CancellationToken cancellationToken) =>
        (await documents.ListByOrderAsync(request.OrderId, cancellationToken)).Select(FiscalDocumentDto.From).ToList();

    public async ValueTask<Result<FiscalDocumentDto>> Handle(GetDocument request, CancellationToken cancellationToken) =>
        await documents.GetAsync(request.DocumentId, cancellationToken) is { } d ? FiscalDocumentDto.From(d) : NotFound(request.DocumentId);

    public async ValueTask<Result<ExportedFile>> Handle(GetDocumentPdf request, CancellationToken cancellationToken)
    {
        if (!features.IsEnabled(FeatureFlagNames.InvoicePdf))
        {
            return Error.NotFound("feature.disabled", "Invoice PDFs are not available yet.");
        }

        return await documents.GetAsync(request.DocumentId, cancellationToken) is { } d ? pdf.Render(d) : NotFound(request.DocumentId);
    }

    public async ValueTask<Result<ExportedFile>> Handle(ExportMonthlyDeclaration request, CancellationToken cancellationToken)
    {
        if (string.Equals(request.Format, "xml", StringComparison.OrdinalIgnoreCase) && !features.IsEnabled(FeatureFlagNames.EDeclaracaoXml))
        {
            return Error.NotFound("feature.disabled", "The XML e-Declaração file is not available yet; use csv or xlsx.");
        }

        var period = await PeriodAsync(request.IssuerNuit, request.Year, request.Month, cancellationToken);
        return period.IsFailure ? period.Error : exporter.Export(period.Value, request.Format);
    }

    public async ValueTask<Result<IvaSummary>> Handle(GetIvaSummary request, CancellationToken cancellationToken)
    {
        var period = await PeriodAsync(request.IssuerNuit, request.Year, request.Month, cancellationToken);
        if (period.IsFailure)
        {
            return period.Error;
        }

        var visitor = new IvaSummaryVisitor();
        period.Value.Accept(visitor);
        return visitor.Result();
    }

    public async ValueTask<Result<IReadOnlyList<InvoiceRequestDto>>> Handle(ListInvoiceRequests request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<InvoiceRequestStatus>(request.Status, ignoreCase: true, out var status) || !Enum.IsDefined(status))
        {
            return Error.Validation(new FieldError("status", $"Use one of: {string.Join(", ", Enum.GetNames<InvoiceRequestStatus>())}."));
        }

        return (await requests.ListByStatusAsync(status, cancellationToken))
            .Select(r => new InvoiceRequestDto(r.Id, r.OrderId, r.OrderNumber, r.SellerId, r.Status.ToString(), r.InvoiceId, r.Problem))
            .ToList();
    }

    public ValueTask<Result<IReadOnlyList<TaxRate>>> Handle(ListTaxRates request, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Result.Success(taxRates.RatesOn(request.On ?? MozambiqueTime.Today(timeProvider))));

    private static Error NotFound(Guid id) => Error.NotFound("invoicing.document_not_found", $"No fiscal document {id}.");

    /// <summary>A bundle mixing exempt and taxed goods becomes one invoice line per tax category (Composite → IVA per component).</summary>
    private IEnumerable<LineToPrice> ToPrice(OrderLineContract line, decimal discount, ProductTaxProfile? profile)
    {
        var shares = profile?.Shares is { Count: > 0 } s ? s : [new TaxShare(settings.FallbackTaxCategory, 1m)];
        if (shares.Count == 1)
        {
            yield return new LineToPrice(line.Name, line.Sku, line.Quantity, shares[0].Category, line.LineTotalMzn, discount);
            yield break;
        }

        var gross = Allocation.Split(line.LineTotalMzn, [.. shares.Select(x => x.Fraction)]);
        var discounts = Allocation.Split(discount, [.. shares.Select(x => x.Fraction)]);
        for (var i = 0; i < shares.Count; i++)
        {
            yield return new LineToPrice($"{line.Name} — {shares[i].Category}", line.Sku, line.Quantity, shares[i].Category, gross[i], discounts[i]);
        }
    }

    private async Task<Result<FiscalPeriod>> PeriodAsync(string nuit, int year, int month, CancellationToken cancellationToken)
    {
        if (month is < 1 or > 12 || year is < 2000 or > 2100)
        {
            return Error.Validation(new FieldError("month", "Give a valid year and month (1–12)."));
        }

        var issuerName = nuit == settings.Marketplace.Nuit
            ? settings.Marketplace.Name
            : (await taxpayers.FindByNuitAsync(nuit, cancellationToken))?.LegalName;
        if (issuerName is null)
        {
            return Error.NotFound("invoicing.taxpayer_not_found", $"No issuer with NUIT {nuit}.");
        }

        return new FiscalPeriod(nuit, issuerName, year, month, await documents.ListForPeriodAsync(nuit, year, month, cancellationToken));
    }

    /// <summary>Issues the request's invoice if the issuer is known and the data is valid; never throws for business reasons.</summary>
    private async Task<bool> TryIssueAsync(InvoiceRequest request, CancellationToken cancellationToken, TaxpayerProfile? knownProfile = null)
    {
        var now = timeProvider.GetUtcNow();
        FiscalParty issuer;
        FiscalRegime regime;
        if (request.SellerId == InvoiceRequest.Marketplace)
        {
            (issuer, regime) = (settings.Marketplace, settings.MarketplaceRegime);
        }
        else if ((knownProfile ?? await taxpayers.GetAsync(request.SellerId, cancellationToken)) is { } profile)
        {
            (issuer, regime) = (profile.AsIssuer, profile.Regime);
        }
        else
        {
            request.MarkAwaitingTaxpayer(now);
            return false;
        }

        var kit = regimes.KitFor(regime);
        var today = MozambiqueTime.Today(timeProvider);
        var counter = await numbering.CounterAsync(issuer.Nuit!, kit.Series.For(DocumentType.Invoice), today.Year, cancellationToken);
        var invoice = InvoiceBuilder.For(kit)
            .IssuedBy(issuer)
            .BilledTo(request.Buyer, nuitRequired: request.BuyerNuitRequired)
            .IssuedOn(today)
            .ForOrder(request.OrderId, request.OrderNumber, request.SellerId == InvoiceRequest.Marketplace ? null : request.SellerId, request.CustomerPhone)
            .AddLines(request.Lines)
            .Build(counter.Next, now);

        if (invoice.IsFailure)
        {
            request.MarkRejected(invoice.Error is ValidationError v ? string.Join("; ", v.Errors.Select(e => $"{e.Field}: {e.Message}")) : invoice.Error.Message, now);
            return false;
        }

        documents.Add(invoice.Value);
        request.MarkIssued(invoice.Value.Id, now);
        return true;
    }
}
