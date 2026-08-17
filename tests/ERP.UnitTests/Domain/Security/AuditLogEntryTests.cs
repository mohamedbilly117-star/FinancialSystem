using ERP.Domain.Entities.Security;
using FluentAssertions;
using Xunit;

namespace ERP.UnitTests.Domain.Security;

public class AuditLogEntryTests
{
    private static readonly DateTime Now = new(2026, 7, 15, 10, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void ForAuthenticationEvent_Login_StoresCoreFieldsAndLeavesEntityFieldsNull()
    {
        var userId = Guid.NewGuid();

        var entry = AuditLogEntry.ForAuthenticationEvent(userId, "ahmed.hassan", Now, "Login");

        entry.UserId.Should().Be(userId);
        entry.UserName.Should().Be("ahmed.hassan");
        entry.Action.Should().Be("Login");
        entry.OccurredAtUtc.Should().Be(Now);
        entry.Module.Should().BeNull();
        entry.AffectedEntityType.Should().BeNull();
        entry.AffectedEntityId.Should().BeNull();
    }

    [Fact]
    public void ForAuthenticationEvent_FailedLoginWithNoKnownUser_AllowsNullUserId()
    {
        // A failed login against a username that may not correspond to any
        // real account must still be recordable (Prompt 6: "Failed Login").
        var entry = AuditLogEntry.ForAuthenticationEvent(null, "unknown.user", Now, "FailedLogin", "Invalid credentials");

        entry.UserId.Should().BeNull();
        entry.UserName.Should().Be("unknown.user");
        entry.Action.Should().Be("FailedLogin");
        entry.Reason.Should().Be("Invalid credentials");
    }

    [Fact]
    public void ForAuthenticationEvent_WithNullOrWhitespaceUserName_Throws()
    {
        Action act = () => AuditLogEntry.ForAuthenticationEvent(null, "   ", Now, "FailedLogin");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ForEntityChange_ValidInput_StoresAllFields()
    {
        var userId = Guid.NewGuid();
        var officeId = Guid.NewGuid();
        var entityId = Guid.NewGuid();

        var entry = AuditLogEntry.ForEntityChange(
            userId,
            "sara.ibrahim",
            officeId,
            "Accounting Office",
            Now,
            "Post",
            "Accounting",
            "JournalEntry",
            entityId,
            "{\"Status\":\"Approved\"}",
            "{\"Status\":\"Posted\"}",
            "Month-end posting run",
            "Approved");

        entry.UserId.Should().Be(userId);
        entry.OfficeId.Should().Be(officeId);
        entry.RoleNamesSnapshot.Should().Be("Accounting Office");
        entry.Module.Should().Be("Accounting");
        entry.AffectedEntityType.Should().Be("JournalEntry");
        entry.AffectedEntityId.Should().Be(entityId);
        entry.OldValuesJson.Should().Contain("Approved");
        entry.NewValuesJson.Should().Contain("Posted");
        entry.ApprovalStatus.Should().Be("Approved");
    }

    [Fact]
    public void ForEntityChange_WithEmptyUserId_Throws()
    {
        Action act = () => AuditLogEntry.ForEntityChange(
            Guid.Empty, "sara.ibrahim", null, null, Now, "Post", "Accounting", "JournalEntry", Guid.NewGuid());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ForEntityChange_WithEmptyAffectedEntityId_Throws()
    {
        Action act = () => AuditLogEntry.ForEntityChange(
            Guid.NewGuid(), "sara.ibrahim", null, null, Now, "Post", "Accounting", "JournalEntry", Guid.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ForPermissionOrRoleChange_AlwaysSetsModuleToSecurity()
    {
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();

        var entry = AuditLogEntry.ForPermissionOrRoleChange(
            userId, "admin", Now, "PermissionGranted", "Role", roleId,
            oldValuesJson: null,
            newValuesJson: "{\"Permission\":\"JournalEntry.Post\"}");

        entry.Module.Should().Be("Security");
        entry.AffectedEntityType.Should().Be("Role");
        entry.AffectedEntityId.Should().Be(roleId);
        entry.OfficeId.Should().BeNull();
        entry.RoleNamesSnapshot.Should().BeNull();
    }

    [Fact]
    public void ForPermissionOrRoleChange_WithEmptyUserId_Throws()
    {
        Action act = () => AuditLogEntry.ForPermissionOrRoleChange(
            Guid.Empty, "admin", Now, "PermissionGranted", "Role", Guid.NewGuid());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithNullOrWhitespaceAction_Throws()
    {
        Action act = () => AuditLogEntry.Create(null, null, null, null, Now, "  ");

        act.Should().Throw<ArgumentException>();
    }
}
