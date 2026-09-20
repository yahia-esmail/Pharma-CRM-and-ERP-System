using PharmaERP.Domain.Enums;

namespace PharmaERP.Application.Territories;

public record TerritoryListItemDto(int Id, string Name, string? Region, TerritoryType Type,
    int? ParentTerritoryId, string? ParentTerritoryName, string? DistrictManagerName, int RepresentativeCount);

public record TerritoryDetailDto(int Id, string Name, string? Region, string? Description, TerritoryType Type,
    int? ParentTerritoryId, string? ParentTerritoryName, int? DistrictManagerId, string? DistrictManagerName);

public class TerritorySaveRequest
{
    public string Name { get; set; } = null!;
    public string? Region { get; set; }
    public string? Description { get; set; }
    public TerritoryType Type { get; set; } = TerritoryType.District;
    public int? ParentTerritoryId { get; set; }
    public int? DistrictManagerId { get; set; }
}
