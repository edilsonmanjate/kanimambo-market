namespace Kanimambo.Simulators.Wholesaler;

public sealed record WholesalerPageDto(int Page, int PageSize, int TotalPages, IReadOnlyList<WholesalerProductDto> Items, int? NextPage);
