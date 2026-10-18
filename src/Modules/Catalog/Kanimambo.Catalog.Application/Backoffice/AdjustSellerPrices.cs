using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Catalog.Application.Backoffice;

public sealed record AdjustSellerPrices(Guid SellerId, decimal Percentage, string Reason) : ICommand<BackofficeOperationDto>;
