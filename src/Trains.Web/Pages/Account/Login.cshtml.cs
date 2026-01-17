using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Trains.Web.Pages.Account;

public sealed class LoginModel : PageModel {
    private readonly SignInManager<IdentityUser> _signInManager;

    public LoginModel(SignInManager<IdentityUser> signInManager) {
        _signInManager = signInManager ?? throw new ArgumentNullException(nameof(signInManager));
    }

    [BindProperty]
    [Required]
    public string UserName { get; set; } = "";

    [BindProperty]
    [Required]
    public string Password { get; set; } = "";

    [BindProperty]
    public bool RememberMe { get; set; }

    public string? ErrorMessage { get; private set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken) {
        if (!ModelState.IsValid)
            return Page();

        var result = await _signInManager.PasswordSignInAsync(UserName, Password, RememberMe, lockoutOnFailure: false);
        if (!result.Succeeded) {
            ErrorMessage = "Invalid username or password.";
            return Page();
        }

        return Redirect("/");
    }
}

