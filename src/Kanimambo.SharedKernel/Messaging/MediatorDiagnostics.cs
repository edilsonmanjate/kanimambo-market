using System.Diagnostics;

namespace Kanimambo.SharedKernel.Messaging;

public static class MediatorDiagnostics
{
    public const string ActivitySourceName = "Kanimambo.Mediator";

    internal static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
