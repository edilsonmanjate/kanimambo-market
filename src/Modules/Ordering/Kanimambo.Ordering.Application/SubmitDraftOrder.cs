using Kanimambo.Ordering.Application.Checkout;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Ordering.Application;

public sealed record SubmitDraftOrder(Guid DraftId, PaymentMethod PaymentMethod, string Phone, string? Nuit, DeliveryChoice? Delivery = null) : ICommand<CheckoutResult>;
