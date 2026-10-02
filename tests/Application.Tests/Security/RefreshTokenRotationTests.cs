using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using PharmaERP.Infrastructure.Persistence;
using PharmaERP.Infrastructure.Security;

namespace PharmaERP.Application.Tests.Security;

public class RefreshTokenRotationTests : IDisposable
{
    private const string UserId = "rep-user";
    private readonly ApplicationDbContext _db = TestDb.Create();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 2, 8, 0, 0, TimeSpan.Zero));
    private readonly TokenService _tokens;

    public RefreshTokenRotationTests()
    {
        var settings = Options.Create(new JwtSettings { Key = "unused", Issuer = "t", Audience = "t", RefreshTokenDays = 14, RefreshReuseGraceSeconds = 120 });
        // Rotation never builds an access token, so no UserManager is needed.
        _tokens = new TokenService(null!, settings, _db, _clock);
    }

    private async Task<int> ActiveCountAsync() =>
        await _db.RefreshTokens.CountAsync(t => t.RevokedAtUtc == null);

    [Fact]
    public async Task Rotation_revokes_the_token_and_issues_a_successor()
    {
        var first = await _tokens.CreateRefreshTokenAsync(UserId);

        var rotation = await _tokens.RotateRefreshTokenAsync(first);

        Assert.NotNull(rotation);
        Assert.Equal(UserId, rotation.UserId);
        Assert.NotEqual(first, rotation.NewRefreshToken);
        Assert.Equal(1, await ActiveCountAsync());
        Assert.NotNull(await _tokens.RotateRefreshTokenAsync(rotation.NewRefreshToken));
    }

    [Fact]
    public async Task Lost_reply_within_the_grace_window_still_refreshes_and_leaves_one_live_token()
    {
        var first = await _tokens.CreateRefreshTokenAsync(UserId);
        var lost = await _tokens.RotateRefreshTokenAsync(first);   // reply never reached the phone
        _clock.Advance(TimeSpan.FromSeconds(30));

        var retry = await _tokens.RotateRefreshTokenAsync(first);   // next app start presents the old token

        Assert.NotNull(retry);
        Assert.Null(await _tokens.RotateRefreshTokenAsync(lost!.NewRefreshToken));   // the lost one is retired
        Assert.Equal(1, await ActiveCountAsync());
        Assert.NotNull(await _tokens.RotateRefreshTokenAsync(retry.NewRefreshToken));
    }

    [Fact]
    public async Task Rotated_token_replayed_after_the_grace_window_ends_the_whole_session()
    {
        var first = await _tokens.CreateRefreshTokenAsync(UserId);
        var second = await _tokens.RotateRefreshTokenAsync(first);
        _clock.Advance(TimeSpan.FromMinutes(10));
        var third = await _tokens.RotateRefreshTokenAsync(second!.NewRefreshToken);   // legitimate use

        Assert.Null(await _tokens.RotateRefreshTokenAsync(first));   // someone replays the old copy

        Assert.Null(await _tokens.RotateRefreshTokenAsync(third!.NewRefreshToken));   // the chain is revoked
        Assert.Equal(0, await ActiveCountAsync());
    }

    [Fact]
    public async Task Logged_out_token_is_never_honoured_even_within_the_grace_window()
    {
        var token = await _tokens.CreateRefreshTokenAsync(UserId);
        await _tokens.RevokeRefreshTokenAsync(token);

        Assert.Null(await _tokens.RotateRefreshTokenAsync(token));
    }

    [Fact]
    public async Task Expired_or_unknown_tokens_are_refused()
    {
        var token = await _tokens.CreateRefreshTokenAsync(UserId);
        _clock.Advance(TimeSpan.FromDays(15));

        Assert.Null(await _tokens.RotateRefreshTokenAsync(token));
        Assert.Null(await _tokens.RotateRefreshTokenAsync("not-a-token"));
    }

    public void Dispose() => _db.Dispose();
}
