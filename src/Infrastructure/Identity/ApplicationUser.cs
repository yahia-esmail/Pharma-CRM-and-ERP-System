using Microsoft.AspNetCore.Identity;

namespace PharmaERP.Infrastructure.Identity;

/// <summary>
/// ASP.NET Core Identity user (spec 5.1/5.4). Linked 1:1 to a Representative record when the
/// account belongs to field-force staff; Admin/Management/Finance/etc. accounts have no link.
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = null!;
    public int? RepresentativeId { get; set; }
    public bool IsActive { get; set; } = true;
}
