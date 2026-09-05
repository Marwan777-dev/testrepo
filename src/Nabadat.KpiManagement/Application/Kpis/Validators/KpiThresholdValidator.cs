using FluentValidation;
using Nabadat.KpiManagement.Domain.Entities;

namespace Nabadat.KpiManagement.Application.Kpis.Validators;

/// <summary>
/// Validates a <see cref="KpiThreshold"/>'s four band edges (data-model.md §1.2 / FR). The single
/// rule is the strictly-ascending invariant <c>LowerBound &lt; X &lt; Y &lt; UpperBound</c>, the same
/// constraint the SQL CHECK enforces at write time — this validator surfaces it as a friendly
/// pre-write error. NPS's negative range (e.g. <c>(-100, -50, 50, 100)</c>) is valid because the
/// rule is purely ordinal.
/// </summary>
public sealed class KpiThresholdValidator : AbstractValidator<KpiThreshold>
{
    public const string NotAscendingCode = "threshold.not_ascending";

    public KpiThresholdValidator()
    {
        RuleFor(t => t)
            .Must(t => t.LowerBound < t.X && t.X < t.Y && t.Y < t.UpperBound)
            .WithErrorCode(NotAscendingCode)
            .WithMessage("Threshold band edges must be strictly ascending: lower_bound < x < y < upper_bound.");
    }
}
