using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Models;
using RaceDay.Mvc.Services;

namespace RaceDay.Mvc.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApiClient _api;
        private readonly UserSession _session;

        public AccountController(ApiClient api, UserSession session)
        {
            _api = api;
            _session = session;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (_session.IsAuthenticated)
                return RedirectToAction("Index", "Home");

            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var result = await _api.LoginAsync(model.Email.Trim(), model.Password);
            if (!result.IsSuccess)
            {
                ModelState.AddModelError(string.Empty, result.Error ?? "We couldn't log you in. Please try again.");
                return View(model);
            }

            TempData["Success"] = $"Welcome back, {FirstName(result.Data!.FullName)}!";

            // Only ever send people back to a page on this site
            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                return LocalRedirect(model.ReturnUrl);

            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult Register(string? role = null)
        {
            if (_session.IsAuthenticated)
                return RedirectToAction("Index", "Home");

            return View(new RegisterViewModel { Role = role == "Organiser" ? "Organiser" : "Participant" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var registered = await _api.RegisterAsync(model.Role, model.FullName.Trim(), model.Email.Trim(), model.Password);
            if (!registered.IsSuccess)
            {
                if (registered.FieldErrors.Count > 0)
                {
                    foreach (var (field, messages) in registered.FieldErrors)
                        foreach (var message in messages)
                            ModelState.AddModelError(ModelState.ContainsKey(field) ? field : string.Empty, message);
                }
                else
                {
                    ModelState.AddModelError(string.Empty, registered.Error ?? "We couldn't create your account. Please try again.");
                }

                return View(model);
            }

            // Log them straight in so they don't have to type it all again
            var login = await _api.LoginAsync(model.Email.Trim(), model.Password);
            if (!login.IsSuccess)
            {
                TempData["Success"] = "Your account is ready. Please log in.";
                return RedirectToAction(nameof(Login));
            }

            TempData["Success"] = $"Welcome to RaceDay, {FirstName(model.FullName)}!";
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _api.LogoutAsync();
            TempData["Success"] = "You've been logged out. See you at the start line!";
            return RedirectToAction("Index", "Home");
        }

        public IActionResult AccessDenied()
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return View();
        }

        private static string FirstName(string fullName) =>
            fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "there";
    }
}
