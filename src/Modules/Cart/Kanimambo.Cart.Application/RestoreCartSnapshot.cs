using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Cart.Application;

/// <summary>Goes back to snapshot N (discarding every later change), e.g. the last state a phone saw before losing signal.</summary>
public sealed record RestoreCartSnapshot(Guid CartId, int Version) : ICommand<CartDto>;
