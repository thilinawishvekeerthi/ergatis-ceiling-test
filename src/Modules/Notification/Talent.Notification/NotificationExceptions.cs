using Talent.Shared.Errors;

namespace Talent.Notification;

internal sealed class NotificationNotFoundException(string what, string id)
    : TalentProblemException(404, $"{what} {id} was not found.");

internal sealed class InvalidNotificationException(string message) : TalentProblemException(400, message);
