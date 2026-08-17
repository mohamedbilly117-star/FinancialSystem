using ERP.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Services;

/// <summary>
/// See <see cref="INumberingSequenceService"/>'s remarks for the overall
/// contract. This implementation's correctness rests entirely on the
/// standard EF Core optimistic-concurrency retry pattern: catch
/// <see cref="DbUpdateConcurrencyException"/>, reload the conflicted
/// entity's current database values via
/// <see cref="IApplicationDbContext.ReloadAsync"/> (which also refreshes
/// its <c>RowVersion</c> concurrency token), then re-attempt the mutation
/// against the now-current values. <see cref="NumberingSequence.GenerateNext"/>
/// is called fresh on each attempt (never cached across attempts), so a
/// retry always advances from whatever the OTHER, already-committed
/// request left <c>CurrentValue</c> at - never re-issuing a number that
/// was already consumed.
/// </summary>
public sealed class NumberingSequenceService : INumberingSequenceService
{
    private const int MaxAttempts = 5;

    private readonly IApplicationDbContext _dbContext;

    public NumberingSequenceService(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<string> GenerateNextAsync(string sequenceKey, Guid? fiscalYearId = null, CancellationToken cancellationToken = default)
    {
        var sequence = await _dbContext.NumberingSequences
            .FirstOrDefaultAsync(s => s.SequenceKey == sequenceKey && s.FiscalYearId == fiscalYearId && s.IsActive, cancellationToken);

        if (sequence is null)
        {
            throw new InvalidOperationException(
                $"No active numbering sequence is configured for key '{sequenceKey}'" +
                (fiscalYearId is not null ? $" and fiscal year '{fiscalYearId}'." : "."));
        }

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var formattedNumber = sequence.GenerateNext();

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                return formattedNumber;
            }
            catch (DbUpdateConcurrencyException)
            {
                if (attempt >= MaxAttempts)
                {
                    throw new InvalidOperationException(
                        $"Failed to generate a number for sequence '{sequenceKey}' after {MaxAttempts} attempts due to sustained concurrent updates.");
                }

                // Another request advanced this same sequence first and
                // already committed. Reload discards our unsaved local
                // increment and refreshes CurrentValue/RowVersion to the
                // database's current (post-other-request) state, so the
                // NEXT loop iteration's GenerateNext() call correctly
                // advances from THAT value rather than repeating it.
                await _dbContext.ReloadAsync(sequence, cancellationToken);
            }
        }

        // Unreachable: every iteration above either returns on success or
        // throws (either the InvalidOperationException above on the final
        // attempt, or lets a non-concurrency exception propagate). Present
        // only because the compiler cannot statically prove that for a
        // general bounded loop.
        throw new InvalidOperationException(
            $"Failed to generate a number for sequence '{sequenceKey}'.");
    }
}
