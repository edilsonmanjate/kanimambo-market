using Kanimambo.SharedKernel.Domain;

namespace Kanimambo.Ordering.Application.Checkout;

public sealed record CheckoutDetails(PaymentMethod PaymentMethod, string Phone, string? Nuit, DeliveryChoice Delivery);
