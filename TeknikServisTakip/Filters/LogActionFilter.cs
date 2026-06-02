using Entities.Concrete;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using TeknikServisTakip.Services;

namespace TeknikServisTakip.Filters
{
    public class LogActionFilter : IAsyncActionFilter
    {
        private readonly ILogService _logService;
        private readonly UserManager<AppUser> _userManager;
        private readonly LogSettings _logSettings;

        public LogActionFilter(ILogService logService, UserManager<AppUser> userManager, IOptions<LogSettings> logSettings)
        {
            _logService = logService;
            _userManager = userManager;
            _logSettings = logSettings.Value;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // ÖNCE İŞLEMİ YAP
            var result = await next();

            // SONRA LOGLA (SADECE POST İŞLEMLERİ İÇİN)
            if (context.HttpContext.Request.Method == "POST")
            {
                string actionName = context.ActionDescriptor.RouteValues["action"];
                string controllerName = context.ActionDescriptor.RouteValues["controller"];

                // ========== LOGLANMAYACAK İŞLEMLER ==========
                var ignoreActions = new[] { "Login", "Logout", "Register", "ForgotPassword", "ResetPassword", "ChangePassword" };

                // Account controller'daki bu action'ları loglama
                if (controllerName == "Account" && ignoreActions.Contains(actionName))
                {
                    return; // Login, Logout, Register vb. loglanmasın
                }

                string actionType = actionName.Contains("Add") ? "Create" :
                                   actionName.Contains("Create") ? "Create" :
                                   actionName.Contains("Edit") ? "Update" :
                                   actionName.Contains("Update") ? "Update" :
                                   actionName.Contains("Delete") ? "Delete" :
                                   actionName.Contains("Remove") ? "Delete" : "Read";

                // Sadece Create, Update, Delete işlemlerini logla
                if (actionType == "Create" || actionType == "Update" || actionType == "Delete")
                {
                    if (context.HttpContext.User.Identity.IsAuthenticated)
                    {
                        var userId = _userManager.GetUserId(context.HttpContext.User);
                        var user = await _userManager.FindByIdAsync(userId);

                        await _logService.LogAsync(
                            action: $"{controllerName}/{actionName}",
                            actionType: actionType,
                            entityName: controllerName,
                            entityId: null,
                            description: $"Kullanıcı {actionName} işlemini gerçekleştirdi",
                            oldValues: null,
                            newValues: null
                        );
                    }
                }
            }
        }
    }
}