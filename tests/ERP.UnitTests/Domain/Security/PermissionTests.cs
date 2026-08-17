using ERP.Domain.Entities.Security;
using FluentAssertions;
using Xunit;

namespace ERP.UnitTests.Domain.Security;

public class PermissionTests
{
    [Fact]
    public void Create_ValidInput_BuildsCodeAsResourceDotAction()
    {
        var permission = Permission.Create("Accounting", "JournalEntry", "Post", "ترحيل قيد محاسبي", "Post Journal Entry");

        permission.Code.Should().Be("JournalEntry.Post");
        permission.Module.Should().Be("Accounting");
        permission.Resource.Should().Be("JournalEntry");
        permission.Action.Should().Be("Post");
    }

    [Fact]
    public void Create_DefaultsToSystemPermission()
    {
        var permission = Permission.Create("Accounting", "JournalEntry", "View", "عرض", "View");

        permission.IsSystemPermission.Should().BeTrue();
    }

    [Fact]
    public void Create_WithIsSystemPermissionFalse_StoresAsNonSystemPermission()
    {
        var permission = Permission.Create("Accounting", "JournalEntry", "View", "عرض", "View", isSystemPermission: false);

        permission.IsSystemPermission.Should().BeFalse();
    }

    [Fact]
    public void Create_WithNullOrWhitespaceModule_Throws()
    {
        Action act = () => Permission.Create("   ", "JournalEntry", "Post", "ترحيل", "Post");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithNullOrWhitespaceResource_Throws()
    {
        Action act = () => Permission.Create("Accounting", "", "Post", "ترحيل", "Post");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithNullOrWhitespaceAction_Throws()
    {
        Action act = () => Permission.Create("Accounting", "JournalEntry", "", "ترحيل", "Post");

        act.Should().Throw<ArgumentException>();
    }
}

public class RolePermissionTests
{
    [Fact]
    public void Create_ValidInput_Succeeds()
    {
        var roleId = Guid.NewGuid();
        var permissionId = Guid.NewGuid();

        var grant = RolePermission.Create(roleId, permissionId);

        grant.RoleId.Should().Be(roleId);
        grant.PermissionId.Should().Be(permissionId);
        grant.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void Create_WithEmptyRoleId_Throws()
    {
        Action act = () => RolePermission.Create(Guid.Empty, Guid.NewGuid());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithEmptyPermissionId_Throws()
    {
        Action act = () => RolePermission.Create(Guid.NewGuid(), Guid.Empty);

        act.Should().Throw<ArgumentException>();
    }
}
