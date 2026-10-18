using Kanimambo.Invoicing.Application.Visitors;
using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Invoicing.Application;

public sealed record GetIvaSummary(string IssuerNuit, int Year, int Month) : IQuery<IvaSummary>;
