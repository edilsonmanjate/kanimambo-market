namespace Kanimambo.Catalog.Application;

/// <summary>Module identity: used for Mediator registration and the module's keyed unit of work.</summary>
public static class CatalogModule
{
    public const string Name = "Catalog";
}

internal static class CatalogErrors
{
    public static SharedKernel.Results.Error SellerNotFound(Guid id) =>
        SharedKernel.Results.Error.NotFound("catalog.seller_not_found", $"Seller {id} does not exist.");

    public static SharedKernel.Results.Error SellerSuspended(string name) =>
        SharedKernel.Results.Error.BusinessRule("catalog.seller_suspended", $"Seller '{name}' is suspended.");

    public static SharedKernel.Results.Error ProductNotFound(Guid id) =>
        SharedKernel.Results.Error.NotFound("catalog.product_not_found", $"Product {id} does not exist.");
}
