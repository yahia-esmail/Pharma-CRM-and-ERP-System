using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using PharmaERP.Application.Common;

namespace PharmaERP.Web.Api.Controllers;

/// <summary>Helpers for endpoints the field app calls through its offline outbox.</summary>
internal static class FieldRequest
{
    /// <summary>Set by the field app's outbox on every send attempt: the device clock at the moment the
    /// request left the phone. The server compares it with its own clock to correct device timestamps
    /// recorded offline (see IFieldVisit).</summary>
    public const string ClientSentAtHeader = "X-Client-Sent-At";

    public static DateTime? ClientSentAtUtc(this HttpRequest request) =>
        request.Headers.TryGetValue(ClientSentAtHeader, out var value)
        && DateTime.TryParse(value.ToString(), CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var sentAt)
            ? sentAt
            : null;

    public static int? RepresentativeId(this ControllerBase controller) =>
        int.TryParse(controller.User.FindFirst("RepresentativeId")?.Value, out var id) ? id : null;

    /// <summary>Runs a representative-only action and maps the application exceptions to HTTP results.</summary>
    public static async Task<ActionResult> ForRepresentativeAsync(this ControllerBase controller,
        Func<int, Task<ActionResult>> action)
    {
        if (controller.RepresentativeId() is not { } repId) return controller.Forbid();
        try
        {
            return await action(repId);
        }
        catch (ValidationFailedException ex)
        {
            return controller.BadRequest(new ProblemDetails { Title = ex.Message });
        }
        catch (NotFoundException ex)
        {
            return controller.NotFound(new ProblemDetails { Title = ex.Message });
        }
        catch (ForbiddenAccessException)
        {
            return controller.Forbid();
        }
    }
}
