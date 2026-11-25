using Microsoft.AspNetCore.Mvc;
using MusicPlayer.Web.Models;
using MusicPlayer.Web.Services;
using System.IO;

namespace MusicPlayer.Web.Controllers;

/// <summary>
/// API REST pour la bibliothèque musicale.
/// Endpoints :
///   GET  /api/tracks          -> liste des MP3 disponibles
///   GET/HEAD /api/tracks/{fileName} -> streaming d'un MP3 avec support des requêtes Range (HTTP 206)
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TracksController : ControllerBase
{
    private readonly MusicLibraryService _library;
    private readonly ILogger<TracksController> _logger;

    public TracksController(MusicLibraryService library, ILogger<TracksController> logger)
    {
        _library = library;
        _logger = logger;
    }

    /// <summary>
    /// Retourne la liste des morceaux MP3 disponibles.
    /// </summary>
    [HttpGet]
    public ActionResult<IEnumerable<Track>> GetTracks()
    {
        var tracks = _library.GetTracks().ToList();
        return Ok(tracks);
    }

    /// <summary>
    /// Retourne le fichier MP3 demandé en streaming.
    /// L'ASP.NET Core gère nativement les en-têtes Range (206 Partial Content),
    /// Accept-Ranges et Content-Type audio/mpeg sans charger le fichier en mémoire.
    /// </summary>
    [HttpGet("{fileName}")]
    [HttpHead("{fileName}")]
    public IActionResult GetTrack(string fileName)
    {
        var fullPath = _library.GetSafeFullPath(fileName);
        if (fullPath is null)
        {
            _logger.LogWarning("Fichier MP3 introuvable ou accès refusé : {FileName}", fileName);
            return NotFound();
        }

        try
        {
            // FileStream en lecture seule, partagé (le navigateur peut ouvrir plusieurs plages).
            var stream = new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true);

            var result = new FileStreamResult(stream, "audio/mpeg")
            {
                EnableRangeProcessing = true
            };

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la lecture du fichier MP3 : {FileName}", fileName);
            return StatusCode(500);
        }
    }

    /// <summary>
    /// Retourne la jaquette (album art) associée au morceau, si une image sidecar existe
    /// (même nom de base que le MP3, extension .jpg/.jpeg/.png/.webp).
    /// </summary>
    [HttpGet("{fileName}/cover")]
    public IActionResult GetCover(string fileName)
    {
        var fullPath = _library.GetSafeFullPath(fileName);
        if (fullPath is null)
        {
            _logger.LogWarning("Fichier MP3 introuvable ou accès refusé : {FileName}", fileName);
            return NotFound();
        }

        var directory = Path.GetDirectoryName(fullPath);
        if (directory is null)
        {
            return NotFound();
        }

        var baseName = Path.GetFileNameWithoutExtension(fullPath);
        foreach (var ext in new[] { ".jpg", ".jpeg", ".png", ".webp" })
        {
            var candidate = Path.Combine(directory, baseName + ext);
            if (File.Exists(candidate))
            {
                var contentType = ext switch
                {
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    ".webp" => "image/webp",
                    _ => "application/octet-stream"
                };
                return PhysicalFile(candidate, contentType, enableRangeProcessing: false);
            }
        }

        return NotFound();
    }
}
