using Nabadat.UserManagement.Api.Interfaces;
using Nabadat.UserManagement.Domain.ValueObjects;

namespace Nabadat.UserManagement.Api.Accessors;

/// <summary>Default scoped <see cref="ISessionContextAccessor"/> — one per request.</summary>
public sealed class SessionContextAccessor : ISessionContextAccessor
{
    public SessionContext? Current { get; set; }
}
