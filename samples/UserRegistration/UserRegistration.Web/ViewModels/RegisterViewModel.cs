using System.ComponentModel.DataAnnotations;

namespace UserRegistration.Web.ViewModels;

public class RegisterViewModel
{
    [Required, StringLength(100, MinimumLength = 2)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = "";

    [Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = "";

    [Required, StringLength(100, MinimumLength = 8)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [Required, DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = "";
}
