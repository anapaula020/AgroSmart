using Api.Models;
using Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[Route("[action]")]
public class LoginController(
    UserManager<IdentityUser> userManager,
    SignInManager<IdentityUser> signInManager,
    TokenService tokenService,
    IConfiguration config) : Controller
{
    [HttpGet("/login")]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return Redirect("/");
        ViewData["ReturnUrl"] = returnUrl;
        ViewData["Title"]     = "Login";
        return View();
    }

    [HttpPost("/login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string email, string password, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        ViewData["Title"]     = "Login";

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            ViewData["Error"] = "Email ou senha inválidos.";
            return View();
        }

        var result = await signInManager.PasswordSignInAsync(user, password, isPersistent: true, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            ViewData["Error"] = "Conta bloqueada. Tente novamente em 15 minutos.";
            return View();
        }
        if (!result.Succeeded)
        {
            ViewData["Error"] = "Email ou senha inválidos.";
            return View();
        }

        // Gera JWT e salva em cookie httponly + expõe via ViewData para o JS salvar no localStorage
        var roles = await userManager.GetRolesAsync(user);
        var token = await tokenService.GenerateAsync(user, roles);
        Response.Cookies.Append("jwt", token, new CookieOptions
        {
            HttpOnly = false, // precisa ser false para o JS ler e salvar no localStorage
            Secure   = false, // true em prod com HTTPS
            SameSite = SameSiteMode.Strict,
            Expires  = DateTimeOffset.UtcNow.AddHours(8)
        });

        ViewData["Token"] = token;

        return Redirect(returnUrl ?? "/");
    }

    [HttpGet("/register")]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true) return Redirect("/");
        ViewData["Title"] = "Criar Conta";
        return View();
    }

    [HttpPost("/register")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(string email, string password, string confirmPassword)
    {
        ViewData["Title"] = "Criar Conta";

        if (password != confirmPassword)
        {
            ViewData["Error"] = "As senhas não coincidem.";
            return View();
        }

        var user   = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
        var result = await userManager.CreateAsync(user, password);

        if (!result.Succeeded)
        {
            ViewData["Error"] = string.Join(" ", result.Errors.Select(e => e.Description));
            return View();
        }

        await userManager.AddToRoleAsync(user, "User");
        await signInManager.SignInAsync(user, isPersistent: true);

        var roles = await userManager.GetRolesAsync(user);
        var token = await tokenService.GenerateAsync(user, roles);
        Response.Cookies.Append("jwt", token, new CookieOptions
        {
            HttpOnly = false,
            Secure   = false,
            SameSite = SameSiteMode.Strict,
            Expires  = DateTimeOffset.UtcNow.AddHours(8)
        });

        return Redirect("/");
    }

    [HttpPost("/logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        Response.Cookies.Delete("jwt");
        return Redirect("/login");
    }

    [HttpGet("/logout")]
    public async Task<IActionResult> LogoutGet()
    {
        await signInManager.SignOutAsync();
        Response.Cookies.Delete("jwt");
        return Redirect("/login");
    }
}
