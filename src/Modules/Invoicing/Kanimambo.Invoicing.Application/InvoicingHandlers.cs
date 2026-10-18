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
