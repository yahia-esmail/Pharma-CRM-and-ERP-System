using PharmaERP.Application.Orders;
using PharmaERP.Application.Pharmacies;

namespace PharmaERP.Web.Mvc.Models;

public class PharmacyDetailsViewModel
{
    public PharmacyDetailDto Pharmacy { get; set; } = null!;
    public IReadOnlyList<PharmacyVisitDto> Visits { get; set; } = [];
    public PharmacyLedgerDto Ledger { get; set; } = null!;
    public IReadOnlyList<OrderListItemDto> Orders { get; set; } = [];
    public PharmacyVisitFormViewModel NewVisit { get; set; } = new();
}
