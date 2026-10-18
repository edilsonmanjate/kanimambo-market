using Kanimambo.Ordering.Application.Checkout;
using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Ordering.Application;

/// <summary>The checkout command has exactly one collaborator: the facade.</summary>
public sealed class CheckoutHandlers(ICheckoutService checkout) :
    IRequestHandler<CheckoutCart, Result<CheckoutResult>>,
    IRequestHandler<SubmitDraftOrder, Result<CheckoutResult>>
{
    public async ValueTask<Result<CheckoutResult>> Handle(CheckoutCart request, CancellationToken cancellationToken) =>
        await checkout.CheckoutCartAsync(request.CartId, new CheckoutDetails(request.PaymentMethod, request.Phone, request.Nuit, request.Delivery ?? DeliveryChoice.Default), cancellationToken);

    public async ValueTask<Result<CheckoutResult>> Handle(SubmitDraftOrder request, CancellationToken cancellationToken) =>
        await checkout.SubmitDraftAsync(request.DraftId, new CheckoutDetails(request.PaymentMethod, request.Phone, request.Nuit, request.Delivery ?? DeliveryChoice.Default), cancellationToken);
}
