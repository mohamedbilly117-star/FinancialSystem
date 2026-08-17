using ERP.Domain.Entities.Configuration;
using ERP.Domain.Enums;
using ERP.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace ERP.UnitTests.Domain.Configuration;

public class NumberingSequenceTests
{
    [Fact]
    public void Create_ValidInput_StartsWithConfiguredDefaults()
    {
        var sequence = NumberingSequence.Create("JOURNAL", "JV-", 6, NumberingResetPolicy.Never);

        sequence.SequenceKey.Should().Be("JOURNAL");
        sequence.Prefix.Should().Be("JV-");
        sequence.PaddingLength.Should().Be(6);
        sequence.CurrentValue.Should().Be(0);
        sequence.IsActive.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void Create_WithOutOfRangePaddingLength_Throws(int paddingLength)
    {
        Action act = () => NumberingSequence.Create("JOURNAL", "JV-", paddingLength, NumberingResetPolicy.Never);

        act.Should().Throw<DomainException>()
            .WithMessage("*padding length*");
    }

    [Fact]
    public void Create_WithYearlyResetPolicyButNoFiscalYearId_Throws()
    {
        Action act = () => NumberingSequence.Create("JOURNAL", "JV-", 6, NumberingResetPolicy.Yearly);

        act.Should().Throw<DomainException>()
            .WithMessage("*Yearly reset policy requires*");
    }

    [Fact]
    public void Create_WithYearlyResetPolicyAndFiscalYearId_Succeeds()
    {
        var fiscalYearId = Guid.NewGuid();

        var sequence = NumberingSequence.Create("JOURNAL", "JV-", 6, NumberingResetPolicy.Yearly, fiscalYearId);

        sequence.FiscalYearId.Should().Be(fiscalYearId);
    }

    [Fact]
    public void Create_WithNegativeStartingValue_Throws()
    {
        Action act = () => NumberingSequence.Create("JOURNAL", "JV-", 6, NumberingResetPolicy.Never, null, -1);

        act.Should().Throw<DomainException>()
            .WithMessage("*cannot be negative*");
    }

    [Fact]
    public void GenerateNext_FirstCall_ProducesOne()
    {
        var sequence = NumberingSequence.Create("JOURNAL", "JV-", 6, NumberingResetPolicy.Never);

        var result = sequence.GenerateNext();

        result.Should().Be("JV-000001");
        sequence.CurrentValue.Should().Be(1);
    }

    [Fact]
    public void GenerateNext_SequentialCalls_ProduceIncreasingNumbers()
    {
        var sequence = NumberingSequence.Create("JOURNAL", "JV-", 6, NumberingResetPolicy.Never);

        var first = sequence.GenerateNext();
        var second = sequence.GenerateNext();
        var third = sequence.GenerateNext();

        first.Should().Be("JV-000001");
        second.Should().Be("JV-000002");
        third.Should().Be("JV-000003");
    }

    [Fact]
    public void GenerateNext_StartingFromNonZero_ContinuesFromThatValue()
    {
        var sequence = NumberingSequence.Create("JOURNAL", "JV-", 6, NumberingResetPolicy.Never, null, 99);

        var result = sequence.GenerateNext();

        result.Should().Be("JV-000100");
    }

    [Fact]
    public void GenerateNext_WhenInactive_Throws()
    {
        var sequence = NumberingSequence.Create("JOURNAL", "JV-", 6, NumberingResetPolicy.Never);
        sequence.Deactivate();

        Action act = sequence.GenerateNext;

        act.Should().Throw<DomainException>()
            .WithMessage("*not active*");
    }

    [Fact]
    public void Format_WithoutGenerating_ReflectsCurrentValueAtZero()
    {
        var sequence = NumberingSequence.Create("JOURNAL", "JV-", 4, NumberingResetPolicy.Never);

        sequence.Format().Should().Be("JV-0000");
    }

    [Fact]
    public void ResetSequence_SetsCurrentValueBackToZero()
    {
        var sequence = NumberingSequence.Create("JOURNAL", "JV-", 6, NumberingResetPolicy.Yearly, Guid.NewGuid());
        sequence.GenerateNext();
        sequence.GenerateNext();

        sequence.ResetSequence();

        sequence.CurrentValue.Should().Be(0);
    }

    [Fact]
    public void Create_WithPrefixExceedingMaximumLength_Throws()
    {
        var overlyLongPrefix = new string('X', NumberingSequence.MaxPrefixLength + 1);

        Action act = () => NumberingSequence.Create("JOURNAL", overlyLongPrefix, 6, NumberingResetPolicy.Never);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithMaximumAllowedPrefixLength_Succeeds()
    {
        var maxLengthPrefix = new string('X', NumberingSequence.MaxPrefixLength);

        var sequence = NumberingSequence.Create("JOURNAL", maxLengthPrefix, 6, NumberingResetPolicy.Never);

        sequence.Prefix.Should().Be(maxLengthPrefix);
    }

    [Fact]
    public void Create_WithEmptyPrefix_Succeeds()
    {
        var sequence = NumberingSequence.Create("JOURNAL", string.Empty, 6, NumberingResetPolicy.Never);

        sequence.GenerateNext().Should().Be("000001");
    }

    [Fact]
    public void Activate_AfterDeactivate_AllowsGenerationAgain()
    {
        var sequence = NumberingSequence.Create("JOURNAL", "JV-", 6, NumberingResetPolicy.Never);
        sequence.Deactivate();
        sequence.Activate();

        var result = sequence.GenerateNext();

        result.Should().Be("JV-000001");
    }
}
