using ERP.Application.Common.Interfaces;

namespace ERP.Infrastructure.DateTime;

/// <summary>
/// Straightforward server-clock implementation of <see cref="IDateTimeService"/>.
/// Deliberately trivial - the value of the abstraction is in every module
/// depending on the interface (making fiscal-year/period-closing logic
/// unit-testable with a fixed clock) rather than in this implementation's
/// complexity.
/// </summary>
public sealed class DateTimeService : IDateTimeService
{
    public DateTimeOffset NowUtcOffset => DateTimeOffset.UtcNow;

    public global::System.DateTime NowUtc => global::System.DateTime.UtcNow;

    public DateOnly TodayLocal => DateOnly.FromDateTime(global::System.DateTime.Now);
}
