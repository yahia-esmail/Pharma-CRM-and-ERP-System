using Microsoft.AspNetCore.Mvc;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Application.Notifications;

namespace PharmaERP.Web.Mvc.ViewComponents;

public class NotificationBellViewComponent(INotificationService notificationService, ICurrentUserService currentUser) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (currentUser.UserId is not { } userId)
            return Content(string.Empty);

        var recent = await notificationService.GetForUserAsync(userId, unreadOnly: false, take: 8);
        var unreadCount = await notificationService.GetUnreadCountAsync(userId);

        return View(new NotificationBellViewModel(unreadCount, recent));
    }
}

public record NotificationBellViewModel(int UnreadCount, IReadOnlyList<NotificationDto> Recent);
