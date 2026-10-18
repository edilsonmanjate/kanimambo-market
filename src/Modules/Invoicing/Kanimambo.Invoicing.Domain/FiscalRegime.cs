namespace Kanimambo.Invoicing.Domain;

/// <summary>
/// How a seller is taxed. IVA normal sellers charge and declare IVA; sellers in the simplified regime (ISPC,
/// Imposto Simplificado para Pequenos Contribuintes) pay a turnover tax instead and do not charge IVA.
/// </summary>
public enum FiscalRegime
{
    IvaNormal,
    Ispc,
}
