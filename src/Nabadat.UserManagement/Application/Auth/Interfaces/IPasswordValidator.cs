using Nabadat.UserManagement.Application.Auth.Dtos;

namespace Nabadat.UserManagement.Application.Auth.Interfaces;

/// <summary>Validates password complexity (min length + character-class requirements).</summary>
public interface IPasswordValidator
{
    PasswordValidationResult ValidatePassword(string password);
}
