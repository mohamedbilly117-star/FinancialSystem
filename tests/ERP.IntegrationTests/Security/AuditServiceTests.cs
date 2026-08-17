using ERP.Application.Common.Interfaces;
using ERP.Persistence.Context;
using ERP.Security.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ERP.IntegrationTests.Security;

public class AuditServiceTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;

    public AuditServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationDbContext(options);
    }

    public void Dispose() => _dbContext.Dispose();

    private sealed class FakeCurrentUserService : ICurrentUserService
    {
        public Guid? UserId { get; set; }

        public string? UserName { get; set; }

        public Guid? OfficeId { get; set; }

        public Guid? DepartmentId { get; set; }

        public bool IsAuthenticated => UserId is not null;
    }

    private sealed class FakeDateTimeService : IDateTimeService
    {
        public DateTime NowUtc { get; set; } = new(2026, 7, 15, 10, 0, 0, DateTimeKind.Utc);

        public DateOnly TodayLocal => DateOnly.FromDateTime(NowUtc);
    }

    [Fact]
    public async Task LogAuthenticationEventAsync_PersistsImmediatelyWithoutAnExplicitCallerSaveChanges()
    {
        var currentUser = new FakeCurrentUserService();
        var dateTime = new FakeDateTimeService();
        var service = new AuditService(_dbContext, currentUser, dateTime);

        await service.LogAuthenticationEventAsync(null, "unknown.user", "FailedLogin", "Invalid credentials");

        // A fresh query, with no explicit SaveChangesAsync call from this test - confirms the method persisted on its own.
        var saved = await _dbContext.AuditLogEntries.SingleAsync();
        saved.Action.Should().Be("FailedLogin");
        saved.UserName.Should().Be("unknown.user");
        saved.UserId.Should().BeNull();
        saved.Reason.Should().Be("Invalid credentials");
    }

    [Fact]
    public void LogEntityChange_OnlyStagesTheEntry_ChangeTrackerHasPendingChanges()
    {
        var currentUser = new FakeCurrentUserService { UserId = Guid.NewGuid(), UserName = "sara.ibrahim" };
        var dateTime = new FakeDateTimeService();
        var service = new AuditService(_dbContext, currentUser, dateTime);

        service.LogEntityChange("Post", "Accounting", "JournalEntry", Guid.NewGuid());

        // Staged (tracked, pending) - the defining behavior LogEntityChange must have, so it commits atomically with whatever business change the caller is about to save.
        _dbContext.ChangeTracker.HasChanges().Should().BeTrue();
    }

    [Fact]
    public async Task LogEntityChange_PersistsOnlyWhenCallerExplicitlySavesChanges()
    {
        var currentUser = new FakeCurrentUserService { UserId = Guid.NewGuid(), UserName = "sara.ibrahim" };
        var dateTime = new FakeDateTimeService();
        var service = new AuditService(_dbContext, currentUser, dateTime);
        var journalEntryId = Guid.NewGuid();

        service.LogEntityChange("Post", "Accounting", "JournalEntry", journalEntryId, reason: "Month-end run");
        await _dbContext.SaveChangesAsync();

        var saved = await _dbContext.AuditLogEntries.SingleAsync();
        saved.Action.Should().Be("Post");
        saved.Module.Should().Be("Accounting");
        saved.AffectedEntityType.Should().Be("JournalEntry");
        saved.AffectedEntityId.Should().Be(journalEntryId);
        saved.UserId.Should().Be(currentUser.UserId);
    }

    [Fact]
    public void LogEntityChange_WithNoAuthenticatedCurrentUser_Throws()
    {
        var currentUser = new FakeCurrentUserService(); // UserId left null - unauthenticated
        var dateTime = new FakeDateTimeService();
        var service = new AuditService(_dbContext, currentUser, dateTime);

        Action act = () => service.LogEntityChange("Post", "Accounting", "JournalEntry", Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task LogPermissionOrRoleChange_PersistsWithSecurityModuleAfterCallerSaves()
    {
        var currentUser = new FakeCurrentUserService { UserId = Guid.NewGuid(), UserName = "admin" };
        var dateTime = new FakeDateTimeService();
        var service = new AuditService(_dbContext, currentUser, dateTime);
        var roleId = Guid.NewGuid();

        service.LogPermissionOrRoleChange("PermissionGranted", "Role", roleId, newValuesJson: "{\"Permission\":\"JournalEntry.Post\"}");
        await _dbContext.SaveChangesAsync();

        var saved = await _dbContext.AuditLogEntries.SingleAsync();
        saved.Module.Should().Be("Security");
        saved.AffectedEntityType.Should().Be("Role");
        saved.AffectedEntityId.Should().Be(roleId);
    }

    [Fact]
    public void LogPermissionOrRoleChange_WithNoAuthenticatedCurrentUser_Throws()
    {
        var currentUser = new FakeCurrentUserService();
        var dateTime = new FakeDateTimeService();
        var service = new AuditService(_dbContext, currentUser, dateTime);

        Action act = () => service.LogPermissionOrRoleChange("PermissionGranted", "Role", Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>();
    }
}
