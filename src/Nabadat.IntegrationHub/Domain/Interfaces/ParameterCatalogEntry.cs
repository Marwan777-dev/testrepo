namespace Nabadat.IntegrationHub.Domain.Interfaces;

/// <summary>
/// One enabled parameter as published to other modules through
/// <see cref="IParameterCatalogReader"/>. Deliberately narrow: identity, bilingual display names, the wire
/// key, and the data type — no usage flags, no Range sub-configuration, no mapping table. Consumers that
/// need a parameter's known value set go through M-10's data scope (the real CMC-06 integration), not this
/// reader.
/// <para><see cref="DataType"/> is the snake_case wire value (<c>list</c>, <c>date_time</c>, …) rather than
/// M-13's internal enum, so a consumer never takes a compile-time dependency on this module's
/// <c>Domain.ValueObjects</c>.</para>
/// </summary>
public sealed record ParameterCatalogEntry(Guid Id, string NameEn, string NameAr, string ApiField, string DataType);
