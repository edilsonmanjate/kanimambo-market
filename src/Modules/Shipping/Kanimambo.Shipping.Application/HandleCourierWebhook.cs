using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Shipping.Application;

/// <summary>A courier's tracking webhook (picked up, in transit, delivered, failed).</summary>
public sealed record HandleCourierWebhook(string Courier, string Body, string? Signature) : ICommand;
