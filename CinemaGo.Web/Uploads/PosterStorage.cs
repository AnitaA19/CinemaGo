using CinemaGo.Domain.Exceptions;

namespace CinemaGo.Web.Uploads;

// Stores admin-uploaded poster images on disk. The file type is decided from the file's content, never from its name.
public sealed class PosterStorage
{
    public const long MaxBytes = 5 * 1024 * 1024;
    public const string RequestPath = "/uploads";
    private const string PosterFolder = "posters";

    public PosterStorage(string rootPath)
    {
        RootPath = Path.GetFullPath(rootPath);
        Directory.CreateDirectory(Path.Combine(RootPath, PosterFolder));
    }

    public string RootPath { get; }

    public async Task<string> SaveAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            throw new ValidationException("The uploaded file is empty.");
        }
        if (file.Length > MaxBytes)
        {
            throw new ValidationException("The poster must be 5 MB or smaller.");
        }

        var header = new byte[12];
        int read;
        await using (var probe = file.OpenReadStream())
        {
            read = await probe.ReadAsync(header, cancellationToken);
        }

        var extension = DetectExtension(header.AsSpan(0, read))
            ?? throw new ValidationException("The poster must be a JPEG, PNG or WebP image.");

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var path = Path.Combine(RootPath, PosterFolder, fileName);
        await using (var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
        {
            await file.CopyToAsync(target, cancellationToken);
        }

        return $"{RequestPath}/{PosterFolder}/{fileName}";
    }

    // Deletes a previously uploaded poster; ignores external URLs and anything outside the poster folder.
    public void Delete(string? posterUrl)
    {
        var prefix = $"{RequestPath}/{PosterFolder}/";
        if (string.IsNullOrEmpty(posterUrl) || !posterUrl.StartsWith(prefix, StringComparison.Ordinal))
        {
            return;
        }

        var path = Path.Combine(RootPath, PosterFolder, Path.GetFileName(posterUrl));
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static string? DetectExtension(ReadOnlySpan<byte> h)
    {
        if (h.Length >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF)
        {
            return ".jpg";
        }
        if (h.Length >= 8 && h[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return ".png";
        }
        if (h.Length >= 12 && h[..4].SequenceEqual("RIFF"u8) && h[8..12].SequenceEqual("WEBP"u8))
        {
            return ".webp";
        }
        return null;
    }
}
