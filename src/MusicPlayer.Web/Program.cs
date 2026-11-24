using MusicPlayer.Web.Services;
using NLog.Web;

var builder = WebApplication.CreateBuilder(args);

// Journalisation : NLog remplace les providers par défaut de Microsoft.Extensions.Logging.
// La configuration est lue depuis nlog.config (copié dans le répertoire de sortie).
// Le code applicatif continue d'utiliser ILogger<T> via l'injection de dépendances.
builder.Logging.ClearProviders();
builder.Host.UseNLog();

// Configuration du répertoire MP3 via appsettings (défaut : /var/lib/music-player/mp3/).
// Surcharge possible par variable d'environnement, ex. MusicLibrary__RootPath.

builder.Services.AddControllers();
builder.Services.AddSingleton<MusicLibraryService>();
builder.Services.AddLogging();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UsePathBase(app.Configuration["path-base"]);

// Fichiers statiques (index.html, CSS, JS) servis depuis wwwroot.
app.UseStaticFiles();

// Toute route non API renvoie index.html (single page, utile après un rechargement).
app.MapFallbackToFile("index.html");

app.MapControllers();

app.Run();
