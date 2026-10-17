namespace Kanimambo.SharedKernel.Identity;

/// <summary>Who is performing the current operation (for audit trails). Implemented by the API host.</summary>
public interface ICurrentActor
{
    string Name { get; }

    bool IsAuthenticated { get; }
}
