namespace Talent.Shared.Security;

/// <summary>The caller of the current request or job, built once per request.</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>Stable user id. Never a name or email.</summary>
    string UserId { get; }
}

/// <summary>The identity used by seeding and background jobs.</summary>
public sealed class SystemCurrentUser : ICurrentUser
{
    public const string Id = "system";

    public bool IsAuthenticated => true;

    public string UserId => Id;
}

/// <summary>The caller before login exists. Not authenticated, so nothing treats it as a real user.</summary>
public sealed class AnonymousCurrentUser : ICurrentUser
{
    public bool IsAuthenticated => false;

    public string UserId => "anonymous";
}
