namespace PharmaERP.Web.Api;

/// <summary>Rate-limit policy names (configured in Program.cs).</summary>
public static class RateLimits
{
    public const string SignIn = "sign-in";
    public const string PerUser = "per-user";
}
