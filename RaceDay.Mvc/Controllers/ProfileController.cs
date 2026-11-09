using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Filters;
using RaceDay.Mvc.Models;
using RaceDay.Mvc.Services;

namespace RaceDay.Mvc.Controllers
{
    // One profile page for both roles: view and update details, and upload a profile picture
    [RequireLogin]
    public class ProfileController : AppController
    {
        private readonly ApiClient _api;
        private readonly UserSession _session;

        public ProfileController(ApiClient api, UserSession session)
        {
            _api = api;
            _session = session;
        }

        public async Task<IActionResult> Index()
        {
            var result = await _api.GetAsync<ProfileDto>("api/users/me");
            if (SessionEnded(result, out var ended)) return ended;

            if (!result.IsSuccess)
            {
                TempData["Error"] = result.Error;
                return RedirectToAction("Index", "Home");
            }

            var p = result.Data!;
            return View(new ProfileViewModel
            {
                FullName = p.FullName,
                Email = p.Email,
                Phone = p.Phone,
                ProfileImageUrl = p.ProfileImageUrl,
                Role = p.Role
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(ProfileViewModel model)
        {
            if (!ModelState.IsValid)
                return View("Index", model);

            var result = await _api.PutAsync<ProfileDto>("api/users/me", new
            {
                fullName = model.FullName.Trim(),
                phone = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim(),
                email = model.Email.Trim()
            });
            if (SessionEnded(result, out var ended)) return ended;

            if (!result.IsSuccess)
            {
                AddApiErrors(result);
                return View("Index", model);
            }

            // Keep the name shown in the menu up to date
            _session.SignIn(_session.UserId!.Value, _session.Role!, result.Data!.FullName, _session.ApiCookie!);

            TempData["Success"] = "Your details were updated.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadPicture(IFormFile? file)
        {
            // The picture goes to the API, which stores it in Azure (the website never talks to Azure)
            var result = await _api.PostFileAsync<ProfileDto>("api/users/me/profile-image", file);
            if (SessionEnded(result, out var ended)) return ended;

            if (result.IsSuccess)
                TempData["Success"] = "Profile picture updated.";
            else
                TempData["Error"] = result.Error;

            return RedirectToAction(nameof(Index));
        }
    }
}
