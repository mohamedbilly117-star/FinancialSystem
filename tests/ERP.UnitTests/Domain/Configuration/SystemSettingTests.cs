using ERP.Domain.Entities.Configuration;
using FluentAssertions;
using Xunit;

namespace ERP.UnitTests.Domain.Configuration;

public class SystemSettingTests
{
    [Fact]
    public void Create_ValidInput_Succeeds()
    {
        var setting = SystemSetting.Create("Security.MaxLoginAttempts", "5", "Security", "Maximum failed login attempts before lockout");

        setting.Key.Should().Be("Security.MaxLoginAttempts");
        setting.Value.Should().Be("5");
        setting.Category.Should().Be("Security");
        setting.Description.Should().Be("Maximum failed login attempts before lockout");
    }

    [Fact]
    public void Create_WithNullOrWhitespaceKey_Throws()
    {
        Action act = () => SystemSetting.Create("   ", "5", "Security");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithKeyExceedingMaximumLength_Throws()
    {
        var overlyLongKey = new string('K', 101);

        Action act = () => SystemSetting.Create(overlyLongKey, "5", "Security");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithMaximumAllowedKeyLength_Succeeds()
    {
        var maxLengthKey = new string('K', 100);

        var setting = SystemSetting.Create(maxLengthKey, "5", "Security");

        setting.Key.Should().Be(maxLengthKey);
    }

    [Fact]
    public void Create_WithNullOrWhitespaceCategory_Throws()
    {
        Action act = () => SystemSetting.Create("Security.MaxLoginAttempts", "5", "  ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithEmptyValue_Succeeds()
    {
        // A deliberately blank setting is a legitimate state (e.g. an
        // optional value the administrator has not yet configured) -
        // Value is validated as non-null, not non-empty.
        var setting = SystemSetting.Create("Organization.LogoUrl", string.Empty, "Organization");

        setting.Value.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithoutDescription_LeavesDescriptionNull()
    {
        var setting = SystemSetting.Create("Security.MaxLoginAttempts", "5", "Security");

        setting.Description.Should().BeNull();
    }

    [Fact]
    public void UpdateValue_ChangesStoredValue()
    {
        var setting = SystemSetting.Create("Security.MaxLoginAttempts", "5", "Security");

        setting.UpdateValue("10");

        setting.Value.Should().Be("10");
    }

    [Fact]
    public void UpdateValue_DoesNotAffectKeyOrCategory()
    {
        var setting = SystemSetting.Create("Security.MaxLoginAttempts", "5", "Security");

        setting.UpdateValue("10");

        setting.Key.Should().Be("Security.MaxLoginAttempts");
        setting.Category.Should().Be("Security");
    }
}
