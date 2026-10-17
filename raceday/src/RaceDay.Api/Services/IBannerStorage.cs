namespace RaceDay.Api.Services;

/// <summary>
/// Storage abstraction for event banners. Part 2 ships <see cref="LocalBannerStorage"/>;
/// Part 3 registers an Azure Blob implementation in Program.cs without touching the controller.
/// </summary>
public interface IBannerStorage
{
    Task<string> SaveBannerAsync(int eventId, Stream content, string contentType, string extension, CancellationToken cancellationToken = default);
}
