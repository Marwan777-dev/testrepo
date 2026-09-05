using FluentAssertions;
using Nabadat.UserManagement.Infrastructure.Crypto;
using Xunit;

namespace Nabadat.UserManagement.UnitTests.Auth;

public sealed class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void Verify_returns_true_when_password_matches_hash()
    {
        var hash = _hasher.Hash("CorrectHorse1!");

        _hasher.Verify("CorrectHorse1!", hash).Should().BeTrue();
    }

    [Fact]
    public void Verify_returns_false_when_password_does_not_match_hash()
    {
        var hash = _hasher.Hash("correct");

        _hasher.Verify("wrong", hash).Should().BeFalse();
    }

    [Fact]
    public void Hash_produces_distinct_outputs_for_same_input_due_to_salt()
    {
        var first = _hasher.Hash("SamePassword1!");
        var second = _hasher.Hash("SamePassword1!");

        first.Should().NotBe(second);
    }
}
