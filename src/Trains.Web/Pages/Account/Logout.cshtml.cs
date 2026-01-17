using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Trains.Web.Pages.Account;

public sealed class LogoutModel : PageModel {
    private readonly SignInManager<IdentityUser> _signInManager;

    public LogoutModel(SignInManager<IdentityUser> signInManager) {
        _signInManager = signInManager ?? throw new ArgumentNullException(nameof(signInManager));
    }

    public IActionResult OnGet() {
        if (!(User.Identity?.IsAuthenticated ?? false))
            return Redirect("/");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken) {
        await _signInManager.SignOutAsync();
        return Redirect("/");
    }
}

