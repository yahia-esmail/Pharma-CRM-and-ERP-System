using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Infrastructure.Persistence;
using PharmaERP.Infrastructure.Persistence.Interceptors;

namespace PharmaERP.Application.Tests;

/// <summary>In-memory ApplicationDbContext with the production audit interceptor, so audited entities get
/// their CreatedBy/CreatedAt stamps exactly as they do in the app.</summary>
internal static class TestDb
{
    public static ApplicationDbContext Create(string userId = "test-user") =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new AuditSaveChangesInterceptor(new FixedUser(userId), new HttpContextAccessor()))
            .Options);

    private sealed class FixedUser(string userId) : ICurrentUserService
    {
        public string? UserId => userId;
        public string? UserName => userId;
        public int? RepresentativeId => null;
        public int? TerritoryId => null;
        public bool IsInRole(string role) => false;
        public bool HasUnrestrictedAccess => true;
    }
}
