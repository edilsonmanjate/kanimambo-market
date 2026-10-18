namespace Kanimambo.Invoicing.Domain;

/// <summary>Every product of a regime family says which regime it belongs to.</summary>
public interface IRegimeComponent
{
    FiscalRegime Regime { get; }
}
