namespace Nabadat.SurveyBuilder.Domain.Interfaces;

/// <summary>
/// Cross-module port M-01 consumes from the shared <b>file-storage adapter</b> to upload survey /
/// theme assets (logo, background image) and validate handles (research.md §4.7, data-model.md §4).
/// Uploads run through ClamAV + CMK envelope encryption on the concrete side. Published-interface
/// only; the byte payload is passed as a stream so Domain stays free of framework types.
/// <para><b>Declared here per T020;</b> the concrete implementation is supplied by the shared adapter
/// and wired in the host composition root.</para>
/// </summary>
public interface IFileStorageService
{
    /// <summary>Uploads <paramref name="content"/> and returns the opaque storage handle.</summary>
    Task<string> UploadAsync(Stream content, string fileName, string contentType, CancellationToken ct = default);

    /// <summary>Returns <c>true</c> when <paramref name="handle"/> refers to an existing stored object.</summary>
    Task<bool> ExistsAsync(string handle, CancellationToken ct = default);
}
