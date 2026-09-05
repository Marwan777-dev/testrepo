using Nabadat.CustomerJourneyManagement.Application.Interfaces;
using NSubstitute;

namespace Nabadat.CustomerJourneyManagement.UnitTests.TestSupport;

/// <summary>
/// Unit-test factory for a fake <see cref="ITenantDbContext"/> — the single transaction boundary
/// that replaced the old <c>ITransactionRunner</c>. <see cref="Immediate"/> returns a substitute
/// whose <c>ExecuteAsync</c> simply invokes the supplied unit-of-work delegate synchronously, so a
/// service's happy-path persistence + event publication run end-to-end without a real database
/// (the data-access services and the M-17 publisher are themselves NSubstitute mocks). The genuine
/// atomic commit/rollback against Postgres is proven by the integration suite.
/// </summary>
internal static class FakeTenantDb
{
    public static ITenantDbContext Immediate()
    {
        var db = Substitute.For<ITenantDbContext>();

        // Run the work delegate inline — the mocked data services/publisher record their calls.
        db.ExecuteAsync(Arg.Any<Func<Task>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ((Func<Task>)ci[0]).Invoke());

        return db;
    }
}
