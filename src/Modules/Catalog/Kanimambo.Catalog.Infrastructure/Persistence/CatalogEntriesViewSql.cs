namespace Kanimambo.Catalog.Infrastructure.Persistence;

/// <summary>
/// SQL for the search read model. A recursive CTE expands every item (product or nested bundle) into its
/// leaf products with the total quantity needed per unit, then derives price, weight and available stock —
/// the same rules as the Composite in the domain, so search filters see the same numbers.
/// Suspended sellers are excluded from search.
/// </summary>
internal static class CatalogEntriesViewSql
{
    public const string Create = """
        CREATE VIEW catalog.catalog_entries AS
        WITH RECURSIVE tree (root_id, item_id, quantity) AS (
            SELECT i."Id", i."Id", 1::bigint
            FROM catalog."CatalogItems" i
            UNION ALL
            SELECT t.root_id, c."ComponentId", t.quantity * c."Quantity"
            FROM tree t
            JOIN catalog."BundleComponents" c ON c."BundleId" = t.item_id
        ),
        leaves AS (
            SELECT t.root_id, SUM(t.quantity) AS quantity,
                   leaf."Stock", leaf."PriceAmount", leaf."PriceCurrency", leaf."UnitWeightKg"
            FROM tree t
            JOIN catalog."CatalogItems" leaf ON leaf."Id" = t.item_id AND leaf."Kind" = 'Simple'
            GROUP BY t.root_id, leaf."Id", leaf."Stock", leaf."PriceAmount", leaf."PriceCurrency", leaf."UnitWeightKg"
        ),
        totals AS (
            SELECT root_id,
                   MIN("Stock" / quantity)::integer AS available_stock,
                   SUM("PriceAmount" * quantity) AS price,
                   MIN("PriceCurrency") AS currency,
                   SUM("UnitWeightKg" * quantity) AS weight_kg
            FROM leaves
            GROUP BY root_id
        )
        SELECT i."Id",
               i."SellerId",
               s."Name" AS "SellerName",
               s."City" AS "SellerCity",
               s."Rating" AS "SellerRating",
               s."DeliveryCities",
               i."Sku",
               i."Name",
               i."Category",
               i."Kind",
               t.price AS "Price",
               t.currency AS "Currency",
               t.available_stock AS "AvailableStock",
               t.weight_kg AS "WeightKg"
        FROM catalog."CatalogItems" i
        JOIN totals t ON t.root_id = i."Id"
        JOIN catalog."Sellers" s ON s."Id" = i."SellerId"
        WHERE s."Status" = 'Active';
        """;

    public const string Drop = "DROP VIEW IF EXISTS catalog.catalog_entries;";
}
