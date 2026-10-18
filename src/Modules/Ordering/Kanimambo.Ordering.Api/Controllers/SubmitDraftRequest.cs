using Kanimambo.SharedKernel.Domain;

namespace Kanimambo.Ordering.Api.Controllers;

public sealed record SubmitDraftRequest(PaymentMethod PaymentMethod, string Phone, string? Nuit, string? DeliveryMethod = null, string? DeliveryDistrict = null, string? PickupPointId = null);
