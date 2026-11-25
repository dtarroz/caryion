namespace MusicPlayer.Web.Models;

/// <summary>
/// Représente un morceau MP3 disponible dans la bibliothèque musicale.
/// Ce modèle est sérialisé en JSON et envoyé au frontend via l'API.
/// </summary>
public class Track
{
    /// <summary>
    /// Nom du fichier (avec extension), utilisé également pour construire l'URL de streaming.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Titre affiché à l'utilisateur. Dans cette version POC, il s'agit du nom du fichier
    /// (sans extension, pour un affichage plus lisible).
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// URL de streaming du morceau, appelée directement par l'élément <c>&lt;audio&gt;</c>.
    /// Exemple : /api/tracks/musique01.mp3
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Taille du fichier en octets.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Nom de l'artiste. Vide si non renseigné (non extrait des tags ID3 dans cette version POC).
    /// </summary>
    public string Artist { get; set; } = string.Empty;

    /// <summary>
    /// Nom de l'album. Vide si non renseigné (non extrait des tags ID3 dans cette version POC).
    /// </summary>
    public string Album { get; set; } = string.Empty;

    /// <summary>
    /// URL de la jaquette (album art) associée au morceau, si une image sidecar existe.
    /// </summary>
    public string? ArtworkUrl { get; set; }
}
