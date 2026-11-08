using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RaceDay.Mvc.Services;

namespace RaceDay.Mvc.Filters
{
    // Put on a controller or action: anyone who isn't logged in is sent to the login page,
    // and brought back to where they were going once they have logged in.
    public class RequireLoginAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var session = context.HttpContext.RequestServices.GetRequiredService<UserSession>();

            if (!session.IsAuthenticated)
                context.Result = ToLogin(context);
        }

        internal static IActionResult ToLogin(ActionExecutingContext context)
        {
            var request = context.HttpContext.Request;
            var returnUrl = HttpMethods.IsGet(request.Method) ? request.Path + request.QueryString : null;
            return new RedirectToActionResult("Login", "Account", new { returnUrl });
        }
    }

    // Like RequireLogin, but also checks the role, so a participant can't open
    // organiser pages and an organiser can't open participant pages.
    public class RequireRoleAttribute : ActionFilterAttribute
    {
        private readonly string _role;

        public RequireRoleAttribute(string role)
        {
            _role = role;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var session = context.HttpContext.RequestServices.GetRequiredService<UserSession>();

            if (!session.IsAuthenticated)
            {
                context.Result = RequireLoginAttribute.ToLogin(context);
                return;
            }

            if (session.Role != _role)
                context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
        }
    }
}
