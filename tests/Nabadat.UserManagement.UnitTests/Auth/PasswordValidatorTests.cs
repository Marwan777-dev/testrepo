using FluentAssertions;
using Nabadat.UserManagement.Application.Auth;
using Xunit;

namespace Nabadat.UserManagement.UnitTests.Auth;

public sealed class PasswordValidatorTests
{
    private readonly PasswordValidator _validator = new();

    [Fact]
    public void ValidatePassword_returns_invalid_when_shorter_than_min_length()
    {
        var result = _validator.ValidatePassword("short1!");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("min_length");
    }

    [Fact]
    public void ValidatePassword_returns_invalid_when_missing_uppercase()
    {
        var result = _validator.ValidatePassword("alllowercase1!");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("missing_uppercase");
    }

    [Fact]
    public void ValidatePassword_returns_invalid_when_missing_lowercase()
    {
        var result = _validator.ValidatePassword("ALLUPPERCASE1!");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("missing_lowercase");
    }

    [Fact]
    public void ValidatePassword_returns_invalid_when_missing_special_character()
    {
        var result = _validator.ValidatePassword("NoSpecialChar1");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("missing_special");
    }

    [Fact]
    public void ValidatePassword_returns_valid_when_all_requirements_met()
    {
        var result = _validator.ValidatePassword("ValidP@ss1");

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }
}
