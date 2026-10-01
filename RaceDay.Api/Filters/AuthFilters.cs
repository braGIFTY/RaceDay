using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace RaceDay.Api.Filters
{
    // Blocks the request unless someone is logged in, regardless of role.
    // Matches "Role Required: Any" from our Part 1 endpoint plan.
    public class RequireAuthAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var userId = context.HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                context.Result = new UnauthorizedObjectResult(
                    new { message = "You must be logged in to access this resource." });
            }
        }
    }

    // Blocks the request unless someone is logged in AND holds the specific
    // role passed in. Matches "Role Required: Organiser" / "Participant".
    public class RequireRoleAttribute : ActionFilterAttribute
    {
        private readonly string _role;

        public RequireRoleAttribute(string role)
        {
            _role = role;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var userId = context.HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                context.Result = new UnauthorizedObjectResult(
                    new { message = "You must be logged in to access this resource." });
                return;
            }

            var role = context.HttpContext.Session.GetString("Role");
            if (role != _role)
            {
                context.Result = new ObjectResult(
                    new { message = $"This action requires the {_role} role." })
                { StatusCode = 403 };
            }
        }
    }
}