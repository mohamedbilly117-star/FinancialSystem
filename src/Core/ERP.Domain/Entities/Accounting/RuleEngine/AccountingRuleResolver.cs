using ERP.Domain.Exceptions;

namespace ERP.Domain.Entities.Accounting.RuleEngine;

/// <summary>
/// Prompt 5 - Accounting Rule Engine. Given a set of candidate
/// <see cref="AccountingRule"/>s (typically pre-filtered by
/// <c>SourceModuleCode</c> at the database level by an Application-layer
/// repository query - this resolver re-filters defensively regardless) and
/// a transaction's field values, determines the single rule that actually
/// applies.
///
/// Deliberately a pure, stateless Domain Service rather than a method on
/// <see cref="AccountingRule"/> itself: resolution inherently operates
/// across MULTIPLE rules (a single rule cannot know about its siblings),
/// so it does not belong to any one aggregate. It takes no dependencies
/// (no database, no DI) and is therefore trivial to unit test exhaustively
/// - exactly why "automatic template selection" was deferred to the
/// Application layer for <see cref="Distribution.DistributionTemplate"/>
/// (that lookup needs a database query to even ASSEMBLE the candidate
/// list) while this resolution step, GIVEN an already-loaded candidate
/// list, does not.
/// </summary>
public static class AccountingRuleResolver
{
    /// <summary>
    /// Returns the single applicable rule, or <see langword="null"/> if no
    /// candidate rule applies (the caller must then decide what happens -
    /// e.g. reject the transaction, or fall back to manual posting; this
    /// resolver never guesses).
    /// </summary>
    /// <param name="candidateRules">Rules to consider - filtered internally to Active, in-date-range, matching <paramref name="sourceModuleCode"/> rules whose Match conditions are satisfied and Exception conditions are not.</param>
    /// <param name="sourceModuleCode">The business event/process being posted, e.g. "REVENUE_COLLECTION".</param>
    /// <param name="context">The transaction's field values (e.g. "Amount" -&gt; "15000.00", "OfficeCode" -&gt; "BANK-01") that every condition is evaluated against.</param>
    /// <param name="asOfDate">The transaction's effective date, used to select the correct version among a rule's lineage.</param>
    /// <exception cref="DomainException">Thrown when two or more surviving candidates share the same lowest (highest-precedence) Priority - an ambiguous configuration that must be resolved by an administrator, never silently guessed at.</exception>
    public static AccountingRule? Resolve(
        IEnumerable<AccountingRule> candidateRules,
        string sourceModuleCode,
        IReadOnlyDictionary<string, string> context,
        DateOnly asOfDate)
    {
        var survivors = candidateRules
            .Where(r => r.SourceModuleCode == sourceModuleCode)
            .Where(r => r.IsActive)
            .Where(r => r.EffectiveFrom <= asOfDate && (r.EffectiveTo is null || r.EffectiveTo >= asOfDate))
            .Where(r => r.MatchConditions.All(c => c.IsSatisfiedBy(context)))
            .Where(r => !r.Exceptions.Any(c => c.IsSatisfiedBy(context)))
            .OrderBy(r => r.Priority)
            .ToList();

        if (survivors.Count == 0)
        {
            return null;
        }

        var topPriority = survivors[0].Priority;
        var topSurvivors = survivors.Where(r => r.Priority == topPriority).ToList();

        if (topSurvivors.Count > 1)
        {
            var ruleCodes = string.Join(", ", topSurvivors.Select(r => r.Code));
            throw new DomainException(
                $"Ambiguous accounting rule resolution for source module '{sourceModuleCode}': " +
                $"{topSurvivors.Count} active rules share the same top priority ({topPriority}) and all match the given context. " +
                $"Rule codes: {ruleCodes}. Assign each a distinct Priority to resolve the ambiguity.");
        }

        return topSurvivors[0];
    }
}
