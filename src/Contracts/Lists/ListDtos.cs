using PharmaERP.Domain.Enums;

namespace PharmaERP.Application.Lists;

public record CustomerListSummaryDto(int Id, string Name, CustomerListType Type, CustomerListMode Mode,
    CustomerListStatus Status, string? OwnerRepresentativeName, int MemberCount);

/// <summary>One customer in a list's resolved membership — for a Static list, backed by a CustomerListItem row
/// (ListItemId set, removable); for a Dynamic list, computed live from the list's filter (ListItemId is null).</summary>
public record CustomerListMemberDto(int? ListItemId, string Kind, int EntityId, string Name, string? City,
    int? CurrentRepresentativeId, string? CurrentRepresentativeName);

public record CustomerListDetailDto(int Id, string Name, CustomerListType Type, CustomerListMode Mode,
    CustomerListStatus Status, int? OwnerRepresentativeId, string? OwnerRepresentativeName,
    int? FilterTerritoryId, string? FilterTerritoryName, int? FilterClassificationId, string? FilterClassificationName,
    string? FilterSegment, bool FilterActiveOnly, IReadOnlyList<CustomerListMemberDto> Members);

public class CustomerListSaveRequest
{
    public string Name { get; set; } = null!;
    public CustomerListType Type { get; set; }
    public CustomerListMode Mode { get; set; }
    public int? OwnerRepresentativeId { get; set; }
    public int? FilterTerritoryId { get; set; }
    public int? FilterClassificationId { get; set; }
    public string? FilterSegment { get; set; }
    public bool FilterActiveOnly { get; set; } = true;
}

public record CustomerTransferLogDto(int Id, string Kind, int EntityId, string EntityName,
    int FromRepresentativeId, string FromRepresentativeName, int ToRepresentativeId, string ToRepresentativeName,
    DateTime TransferDateUtc, string? Reason, string ApprovedByUserId);

public class CustomerTransferRequest
{
    public int? DoctorId { get; set; }
    public int? PharmacyId { get; set; }
    public int ToRepresentativeId { get; set; }
    public string? Reason { get; set; }
}

public record BulkAssignResultDto(int AssignedCount, int TransferLogCount);
