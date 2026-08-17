using ERP.Application.Common.Interfaces;
using ERP.Domain.Entities.Configuration;
using ERP.Domain.Enums;
using ERP.Infrastructure.Services;
using ERP.Persistence.Context;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ERP.IntegrationTests.Configuration;

public class NumberingSequenceServiceTests : IDisposable
{
    private readonly DbContextOptions<ApplicationDbContext> _options;
    private readonly ApplicationDbContext _dbContext;

    public NumberingSequenceServiceTests()
    {
        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationDbContext(_options);
    }

    public void Dispose() => _dbContext.Dispose();

    private async Task SeedSequenceAsync(ApplicationDbContext context, string key, string prefix = "JV-", int paddingLength = 6, int startingValue = 0)
    {
        var sequence = NumberingSequence.Create(key, prefix, paddingLength, NumberingResetPolicy.Never, null, startingValue);
        context.NumberingSequences.Add(sequence);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Forces <see cref="DbUpdateConcurrencyException"/> on demand rather
    /// than relying on EF Core's InMemory provider to auto-generate and
    /// compare RowVersion values the way a real SQL Server ROWVERSION
    /// column does - that behavior is genuinely uncertain without a real
    /// build/test run, so this test double sidesteps it entirely and
    /// makes the conflict scenario fully deterministic and provider-
    /// independent. Each injected "conflict" also genuinely advances the
    /// sequence via a SEPARATE context against the same InMemory database
    /// name, so a subsequent successful retry's result correctly proves
    /// <see cref="IApplicationDbContext.ReloadAsync"/> picked up that real
    /// change - not just that the retry loop executed again.
    /// </summary>
    private sealed class ConflictInjectingDbContext : ApplicationDbContext
    {
        private readonly DbContextOptions<ApplicationDbContext> _options;
        private readonly string _targetSequenceKey;
        private int _remainingInjectedConflicts;

        public int SaveChangesAsyncCallCount { get; private set; }

        public ConflictInjectingDbContext(DbContextOptions<ApplicationDbContext> options, string targetSequenceKey, int conflictsToInject)
            : base(options)
        {
            _options = options;
            _targetSequenceKey = targetSequenceKey;
            _remainingInjectedConflicts = conflictsToInject;
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesAsyncCallCount++;

            if (_remainingInjectedConflicts > 0)
            {
                _remainingInjectedConflicts--;

                using var otherContext = new ApplicationDbContext(_options);
                var otherCopy = await otherContext.NumberingSequences
                    .SingleAsync(s => s.SequenceKey == _targetSequenceKey, cancellationToken);
                otherCopy.GenerateNext();
                await otherContext.SaveChangesAsync(cancellationToken);

                throw new DbUpdateConcurrencyException("Simulated: another request advanced this sequence first.");
            }

            return await base.SaveChangesAsync(cancellationToken);
        }
    }

    [Fact]
    public async Task GenerateNextAsync_SingleCall_ProducesFirstFormattedNumber()
    {
        await SeedSequenceAsync(_dbContext, "JOURNAL");
        var service = new NumberingSequenceService(_dbContext);

        var result = await service.GenerateNextAsync("JOURNAL");

        result.Should().Be("JV-000001");
    }

    [Fact]
    public async Task GenerateNextAsync_ConsecutiveCalls_ProduceIncreasingNumbers()
    {
        await SeedSequenceAsync(_dbContext, "JOURNAL");
        var service = new NumberingSequenceService(_dbContext);

        var first = await service.GenerateNextAsync("JOURNAL");
        var second = await service.GenerateNextAsync("JOURNAL");
        var third = await service.GenerateNextAsync("JOURNAL");

        first.Should().Be("JV-000001");
        second.Should().Be("JV-000002");
        third.Should().Be("JV-000003");
    }

    [Fact]
    public async Task GenerateNextAsync_SeparateSequenceKeys_RemainIndependent()
    {
        await SeedSequenceAsync(_dbContext, "JOURNAL", "JV-");
        await SeedSequenceAsync(_dbContext, "RECEIPT", "RC-");
        var service = new NumberingSequenceService(_dbContext);

        var journalNumber = await service.GenerateNextAsync("JOURNAL");
        var receiptNumber = await service.GenerateNextAsync("RECEIPT");
        var secondJournalNumber = await service.GenerateNextAsync("JOURNAL");

        journalNumber.Should().Be("JV-000001");
        receiptNumber.Should().Be("RC-000001");
        secondJournalNumber.Should().Be("JV-000002"); // unaffected by the interleaved RECEIPT call
    }

    [Fact]
    public async Task GenerateNextAsync_InactiveSequence_ThrowsInvalidOperationExceptionAsNotFound()
    {
        // The service's own query filters to IsActive == true directly,
        // so an inactive sequence is treated as "not configured" (a
        // service-level InvalidOperationException) rather than reaching
        // NumberingSequence.GenerateNext()'s own internal IsActive guard
        // (which throws DomainException) - that guard remains valid
        // defensive coverage for any OTHER caller that loads the entity
        // without going through this service's pre-filtered query.
        var sequence = NumberingSequence.Create("JOURNAL", "JV-", 6, NumberingResetPolicy.Never);
        sequence.Deactivate();
        _dbContext.NumberingSequences.Add(sequence);
        await _dbContext.SaveChangesAsync();
        var service = new NumberingSequenceService(_dbContext);

        Func<Task> act = () => service.GenerateNextAsync("JOURNAL");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GenerateNextAsync_NoSequenceConfiguredForKey_ThrowsInvalidOperationException()
    {
        var service = new NumberingSequenceService(_dbContext);

        Func<Task> act = () => service.GenerateNextAsync("NONEXISTENT");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GenerateNextAsync_OneSimulatedConflict_RetriesAndReflectsTheReloadedState()
    {
        await SeedSequenceAsync(_dbContext, "JOURNAL");
        using var conflictingContext = new ConflictInjectingDbContext(_options, "JOURNAL", conflictsToInject: 1);
        var service = new NumberingSequenceService(conflictingContext);

        var result = await service.GenerateNextAsync("JOURNAL");

        // The injected conflict genuinely advanced the sequence to 1 via a
        // separate context first. If ReloadAsync correctly picked up that
        // committed change, THIS call's own advancement lands on 2 - a
        // single assertion that proves the retry happened, ReloadAsync
        // was called, AND the retry used the reloaded (not stale) state.
        result.Should().Be("JV-000002");

        // One failed attempt + one successful attempt.
        conflictingContext.SaveChangesAsyncCallCount.Should().Be(2);
    }

    [Fact]
    public async Task GenerateNextAsync_ConflictsExceedMaxAttempts_ThrowsInvalidOperationExceptionNotRawConcurrencyException()
    {
        await SeedSequenceAsync(_dbContext, "JOURNAL");
        // Deliberately more than NumberingSequenceService's internal
        // MaxAttempts (5, not publicly exposed - hardcoded here since it
        // is a private implementation detail this test intentionally
        // exercises the outer limit of) so every single attempt conflicts.
        using var conflictingContext = new ConflictInjectingDbContext(_options, "JOURNAL", conflictsToInject: 10);
        var service = new NumberingSequenceService(conflictingContext);

        Func<Task> act = () => service.GenerateNextAsync("JOURNAL");

        var assertedException = await act.Should().ThrowAsync<InvalidOperationException>();
        assertedException.Which.Message.Should().Contain("after 5 attempts");

        // Exactly 5 attempts, no more - the raw DbUpdateConcurrencyException
        // from the underlying SaveChangesAsync calls never escaped past
        // the service boundary; only the intended InvalidOperationException did.
        conflictingContext.SaveChangesAsyncCallCount.Should().Be(5);
    }
}
