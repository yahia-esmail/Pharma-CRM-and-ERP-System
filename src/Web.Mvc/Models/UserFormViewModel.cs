using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace PharmaERP.Web.Mvc.Models;

public class UserListItemViewModel
{
    public string Id { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public bool IsActive { get; set; }
    public IReadOnlyList<string> Roles { get; set; } = [];
}

public class UserCreateViewModel
{
    [Required, StringLength(200)]
    public string FullName { get; set; } = null!;

    [Required, EmailAddress]
    public string Email { get; set; } = null!;

    [Required, DataType(DataType.Password), StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = null!;

    [Required]
    public string Role { get; set; } = null!;

    public int? RepresentativeId { get; set; }

    public IEnumerable<SelectListItem> AvailableRoles { get; set; } = [];
    public IEnumerable<SelectListItem> Representatives { get; set; } = [];
}
