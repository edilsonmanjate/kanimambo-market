namespace Kanimambo.SharedKernel.Domain;

/// <summary>How a customer pays. Shared vocabulary for promotions, checkout and payments.</summary>
public enum PaymentMethod
{
    /// <summary>Not chosen yet (e.g. while browsing the cart).</summary>
    None,

    /// <summary>Vodacom M-Pesa (prefixes 84/85).</summary>
    MPesa,

    /// <summary>Movitel e-Mola (prefixes 86/87).</summary>
    EMola,

    /// <summary>Tmcel mKesh (prefixes 82/83).</summary>
    MKesh,

    /// <summary>Card through the SIMO network / POS.</summary>
    Card,

    CashOnDelivery,
}
