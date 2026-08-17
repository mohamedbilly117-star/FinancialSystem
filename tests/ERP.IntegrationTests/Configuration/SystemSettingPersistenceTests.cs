using ERP.Domain.Entities.Configuration;
using ERP.Persistence.Context;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ERP.IntegrationTests.Configuration;

/// <summary>
/// Duplicate-key prevention for <see cref="SystemSetting"/> is enforced by
/// the unique index on <c>Key</c> in
/// <c>SystemSettingConfiguration</c> - <c>SystemSetting.Create</c> itself
/// has no way to know about sibling rows without a database query, so this
/// is correctly a persistence-level concern, tested here rather than in
/// the Domain-level <c>SystemSettingTests</c>.
/// </summary>
public class SystemSettingPersistenceTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;

    public SystemSettingPersistenceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationDbContext(options);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task SaveChanges_WithDuplicateKey_ThrowsOnTheSecondInsert()
    {
        var first = SystemSetting.Create("Security.MaxLoginAttempts", "5", "Security");
        _dbContext.SystemSettings.Add(first);
        await _dbContext.SaveChangesAsync();

        var duplicate = SystemSetting.Create("Security.MaxLoginAttempts", "10", "Security");
        _dbContext.SystemSettings.Add(duplicate);

        Func<Task> act = () => _dbContext.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task SaveChanges_WithDifferentKeys_BothSucceed()
    {
        _dbContext.SystemSettings.Add(SystemSetting.Create("Security.MaxLoginAttempts", "5", "Security"));
        _dbContext.SystemSettings.Add(SystemSetting.Create("Organization.NameEn", "Ministry of Finance", "Organization"));

        Func<Task> act = () => _dbContext.SaveChangesAsync();

        await act.Should().NotThrowAsync();
    }
}
