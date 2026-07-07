using System.ComponentModel.DataAnnotations;

namespace ClientRelationshipManagement.Web.Models.Account;

public sealed class LoginViewModel
{
    [Required]
    [Display(Name = "Username")]
    public string User { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Pass { get; set; } = string.Empty;

    public string ReturnUrl { get; set; } = "/";

    public string ErrorMessage { get; set; }
}
