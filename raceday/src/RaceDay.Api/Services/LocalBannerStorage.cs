namespace RaceDay.Api.Services;

/// <summary>Writes banners to wwwroot/uploads/banners and returns a relative URL served by UseStaticFiles.</summary>
public sealed class LocalBannerStorage : IBannerStorage
{
    private readonly string _folder;

    public LocalBannerStorage(IWebHostEnvironment environment)
    {
        var webRoot = environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot");
        _folder = Path.Combine(webRoot, "uploads", "banners");
    }

    public async Task<string> SaveBannerAsync(int eventId, Stream content, string contentType, string extension, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_folder);
        var fileName = $"event-{eventId}-{Guid.NewGuid():N}{extension}";
        await using var target = File.Create(Path.Combine(_folder, fileName));
        await content.CopyToAsync(target, cancellationToken);
        return $"/uploads/banners/{fileName}";
    }
}
