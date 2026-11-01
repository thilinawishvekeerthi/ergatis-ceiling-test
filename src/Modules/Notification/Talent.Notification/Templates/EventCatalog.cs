namespace Talent.Notification.Templates;

/// <summary>An alert the system can send: its key, what it means, and the {placeholders} its templates may use.</summary>
internal sealed record EventDefinition(string Key, string Description, IReadOnlyList<string> Placeholders, string? DefaultLink);

/// <summary>
/// Every alert type. A template can only use the placeholders of its event, so a typo is caught when the template is saved
/// and not when someone's email arrives with a hole in it.
/// </summary>
internal static class EventCatalog
{
    public const string CycleOpened = "assessment.cycle-opened";
    public const string ProfileUpdated = "career.profile-updated";

    public static readonly IReadOnlyList<EventDefinition> All =
    [
        new(CycleOpened, "A review cycle opened and you have people to rate.", ["CycleName"], "/manager"),
        new(ProfileUpdated, "Your career profile (current job or competency levels) was updated.", [], "/employee"),
    ];

    public static EventDefinition? Find(string? key) => All.FirstOrDefault(e => string.Equals(e.Key, key, StringComparison.Ordinal));
}
