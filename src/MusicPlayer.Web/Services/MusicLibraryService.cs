using MusicPlayer.Web.Models;
using Microsoft.Extensions.Logging;
namespace MusicPlayer.Web.Services;

/// <summary>
/// Gestion de la bibliothèque musicale : lecture des fichiers MP3 dans un répertoire dédié.
/// Aucune base de données n'est utilisée ; les fichiers sont détectés automatiquement sur le disque.
/// </summary>
public class MusicLibraryService
{
    private readonly string _rootPath;
    private readonly ILogger<MusicLibraryService> _logger;

    public MusicLibraryService(IConfiguration configuration, ILogger<MusicLibraryService> logger)
    {
        _logger = logger;
        // Chemin du répertoire MP3. Par défaut (production) : /var/lib/music-player/mp3/
        // En développement, ce chemin peut être surchargé via appsettings.Development.json.
        _rootPath = configuration["MusicLibrary:RootPath"] ?? "/var/lib/music-player/mp3";
    }

    /// <summary>
    /// Retourne la liste des fichiers MP3 présents dans le répertoire de la bibliothèque.
    /// </summary>
    public IEnumerable<Track> GetTracks()
    {
        _logger.LogInformation("Lecture de la liste des fichiers MP3 dans le répertoire : {RootPath}", _rootPath);

        if (!Directory.Exists(_rootPath))
        {
            _logger.LogWarning("Répertoire MP3 introuvable : {RootPath}", _rootPath);
            // Répertoire absent (cas courant en développement avant ajout de fichiers).
            return Enumerable.Empty<Track>();
        }

        return Directory
            .GetFiles(_rootPath, "*.mp3", SearchOption.TopDirectoryOnly)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .Select(fullPath =>
            {
                var fileName = Path.GetFileName(fullPath);
                var info = new FileInfo(fullPath);
                return new Track
                {
                    FileName = fileName,
                    Title = Path.GetFileNameWithoutExtension(fileName),
                    Url = $"/api/tracks/{Uri.EscapeDataString(fileName)}",
                    Size = info.Length
                };
            })
            .ToList();
    }

    /// <summary>
    /// Retourne le chemin physique complet d'un fichier MP3 autorisé, après vérification de sécurité.
    /// Retourne null si le fichier n'existe pas ou si une tentative de traversée de répertoire est détectée.
    /// </summary>
    public string? GetSafeFullPath(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        // Rejet des caractères interdits et des tentatives de traversée de répertoire.
        if (fileName.Contains('/') || fileName.Contains('\\') || fileName.Contains('\0'))
            return null;

        // Seuls les fichiers .mp3 sont autorisés.
        if (!fileName.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
            return null;

        // Résolution du chemin absolu et vérification qu'il reste bien dans le répertoire racine.
        var rootFullPath = Path.GetFullPath(_rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var candidateFullPath = Path.GetFullPath(Path.Combine(rootFullPath, fileName));

        if (!candidateFullPath.StartsWith(rootFullPath + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !candidateFullPath.Equals(rootFullPath, StringComparison.Ordinal))
        {
            return null;
        }

        return File.Exists(candidateFullPath) ? candidateFullPath : null;
    }
}
