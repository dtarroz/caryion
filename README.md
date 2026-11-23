# Music Player – POC Lecteur MP3 en streaming (ASP.NET Core .NET 10)

Projet de preuve de concept (POC) d'un lecteur MP3 personnel sous forme de site web, conçu prioritairement pour **Safari sur iPhone**.

L'objectif de ce POC est de valider, sur un véritable iPhone, que Safari est capable de :

1. Lire des MP3 directement depuis un serveur distant en **streaming HTTP**.
2. Continuer la lecture lorsque l'iPhone est **verrouillé**.
3. **Enchaîner automatiquement** plusieurs fichiers MP3.
4. Poursuivre la lecture sur une durée prolongée (30 à 60 min).
5. Afficher les informations du morceau sur l'**écran verrouillé**.
6. Contrôler la lecture depuis l'**écran verrouillé / Centre de contrôle iOS**.

Les fichiers MP3 restent sur le serveur. Le navigateur les lit directement via leur URL HTTP, sans aucun téléchargement volontaire côté téléphone (pas d'IndexedDB, pas de Cache API pour l'audio, pas de Service Worker de mise en cache, pas de Blob URL).

> ⚠️ La lecture en arrière-plan et le déverrouillage des commandes sur écran verrouillé dépendent de Safari/iOS. Ce comportement **doit être validé sur un vrai iPhone** ; ce POC ne prétend pas le garantir.

## Architecture

```
MusicPlayer/
├── MusicPlayer.sln
├── README.md
├── .gitignore
└── MusicPlayer.Web/
    ├── Program.cs
    ├── appsettings.json
    ├── appsettings.Development.json
    ├── MusicPlayer.Web.csproj
    ├── Controllers/
    │   └── TracksController.cs        # API REST (/api/tracks)
    ├── Services/
    │   └── MusicLibraryService.cs     # Lecture des fichiers MP3 + sécurité
    ├── Models/
    │   └── Track.cs                   # Modèle de données (sérialisé en JSON)
    └── wwwroot/
        ├── index.html                 # Interface unique (single page)
        ├── css/
        │   └── style.css
        └── js/
            └── player.js              # Lecteur HTML5 + Media Session API
```

- Backend : C# / ASP.NET Core 10, injection de dépendances, pas de base de données.
- Frontend : HTML5, CSS3, JavaScript natif (aucun framework).
- Élément `<audio>` unique conservé pendant toute la session (pas de recréation entre les morceaux).

## Prérequis

### Développement (macOS / Windows / Linux)
- [.NET 10 SDK](https://dotnet.microsoft.com/download) (runtime `Microsoft.AspNetCore.App 10.0`).
- Un terminal.
- Optionnel : un ou plusieurs fichiers MP3 pour tester.

### Déploiement (VPS Debian 13)
- Debian 13.
- .NET 10 Runtime (ASP.NET Core) sur le VPS.
- Un nom de domaine pointant vers le VPS.
- Caddy ou Nginx (reverse proxy + HTTPS).
- Accès SSH/SFTP pour déposer les MP3.
- Au moins 2 Go de RAM recommandés.

## Installation en développement

1. Cloner le dépôt :
   ```bash
   git clone <votre-depot>
   cd audio-web
   ```

2. (Optionnel) Ajouter des fichiers MP3 pour tester en local :
   ```bash
   mkdir -p MusicPlayer.Web/mp3
   # Déposer vos .mp3 dans MusicPlayer.Web/mp3/
   ```
   Le chemin par défaut en développement est `./mp3/` (voir `appsettings.Development.json`).

3. Lancer l'application :
   ```bash
   dotnet run --project MusicPlayer.Web
   ```

4. Ouvrir le navigateur sur `http://localhost:5000`.

## Compilation

```bash
dotnet build MusicPlayer.Web/MusicPlayer.Web.csproj -c Release
```

Sortie : `MusicPlayer.Web/bin/Release/net10.0/`.

## Publication

### Publication framework-dependent (recommandée avec runtime installé sur le VPS)
```bash
dotnet publish MusicPlayer.Web/MusicPlayer.Web.csproj -c Release -o ./publish
```

### Publication autonome (self-contained — Debian x64)
```bash
dotnet publish MusicPlayer.Web/MusicPlayer.Web.csproj -c Release -o ./publish -r linux-x64 --self-contained false
```


## Déploiement sur Debian 13

La procédure ci-dessous installe le runtime .NET 10, publie l'application, configure un service systemd, crée le répertoire MP3, et met en place HTTPS avec Caddy (ou Nginx).

### 1. Installer le runtime ASP.NET Core 10 sur Debian 13

```bash
# Ajouter la clé et le dépôt Microsoft
wget https://packages.microsoft.com/config/debian/13/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

sudo apt-get update
sudo apt-get install -y aspnetcore-runtime-10
```

Vérifier l'installation :
```bash
dotnet --list-runtimes | grep aspnetcore
```

### 2. Publier et déployer l'application

Depuis votre machine de développement :
```bash
dotnet publish MusicPlayer.Web/MusicPlayer.Web.csproj -c Release -o ./publish
```

Transférer les fichiers publiés sur le VPS (ex. `/opt/musicplayer`) :
```bash
sudo mkdir -p /opt/musicplayer
sudo rsync -av --delete publish/ /opt/musicplayer/
```

### 3. Créer le répertoire MP3 et configurer les permissions

```bash
sudo mkdir -p /var/lib/music-player/mp3
sudo chown -R www-data:www-data /var/lib/music-player/mp3
sudo chmod 750 /var/lib/music-player/mp3
```

Déposer vos fichiers MP3 dans `/var/lib/music-player/mp3/` par SSH/SFTP.

### 4. Configurer l'application (chemin des MP3)

Créer ou adapter `/opt/musicplayer/appsettings.json` (ou utiliser une variable d'environnement) :

```json
{
  "MusicLibrary": {
    "RootPath": "/var/lib/music-player/mp3/"
  },
  "AllowedHosts": "*"
}
```

La variable d'environnement `MusicLibrary__RootPath` peut aussi surcharger ce chemin.

### 5. Créer le service systemd

Fichier `/etc/systemd/system/musicplayer.service` :

```ini
[Unit]
Description=Music Player (ASP.NET Core .NET 10)
After=network.target

[Service]
Type=exec
User=www-data
WorkingDirectory=/opt/musicplayer
ExecStart=/usr/bin/dotnet /opt/musicplayer/MusicPlayer.Web.dll
Environment=ASPNETCORE_URLS=http://127.0.0.1:5000
Environment=ASPNETCORE_ENVIRONMENT=Production
Restart=always
RestartSec=5

[Install]
WantedBy=multi-user.target
```

Activer et démarrer :
```bash
sudo systemctl daemon-reload
sudo systemctl enable --now musicplayer
sudo systemctl status musicplayer
```

Vérifier que l'API répond :
```bash
curl -s http://127.0.0.1:5000/api/tracks
```

### 6. Configurer le reverse proxy HTTPS

#### Option A : Caddy

Fichier `/etc/caddy/Caddyfile` :

```caddy
mon-domaine.fr

reverse_proxy 127.0.0.1:5000
```

Puis :
```bash
sudo systemctl enable --now caddy
sudo caddy reload
```

Caddy gère automatiquement le certificat Let's Encrypt. Les requêtes Range sont transmises de manière transparente.

#### Option B : Nginx

Fichier `/etc/nginx/sites-available/musicplayer` :

```nginx
server {
    listen 80;
    server_name mon-domaine.fr;
    return 301 https://$host$request_uri;
}

server {
    listen 443 ssl http2;
    server_name mon-domaine.fr;

    ssl_certificate /etc/letsencrypt/live/mon-domaine.fr/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/mon-domaine.fr/privkey.pem;

    location / {
        proxy_pass http://127.0.0.1:5000;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_buffering off;
        proxy_request_buffering off;
        add_header Accept-Ranges bytes;
    }
}
```

Activer et recharger :
```bash
sudo ln -s /etc/nginx/sites-available/musicplayer /etc/sites-enabled/
sudo nginx -t
sudo systemctl enable --now nginx
```

### 7. Redémarrer l'application si nécessaire

```bash
sudo systemctl restart musicplayer
```


## Configuration du répertoire MP3

- Répertoire de production : `/var/lib/music-player/mp3/`.
- Déposer les fichiers manuellement par SSH/SFTP (pas d'interface d'importation).
- Seuls les fichiers `.mp3` sont détectés et servis.
- Le backend détecte automatiquement les fichiers présents à chaque appel de `GET /api/tracks`.

### Sécurité

Le backend applique les vérifications suivantes :
- Rejet des noms de fichier contenant `/`, `\` ou `\0`.
- Rejet de toute extension autre que `.mp3`.
- Résolution du chemin absolu et vérification qu'il reste dans le répertoire racine (protection contre la traversée de répertoire / path traversal).
- Aucun listage du système de fichiers en dehors du répertoire musical.

## Ajout de nouveaux fichiers par SSH

```bash
# Exemple : transférer un MP3 depuis votre ordinateur
scp musique02.mp3 user@mon-domaine.fr:/tmp/
ssh user@mon-domaine.fr "sudo mv /tmp/musique02.mp3 /var/lib/music-player/mp3/ && sudo chown www-data:www-data /var/lib/music-player/mp3/musique02.mp3"
```

Le nouveau morceau apparaît automatiquement dans la liste sans redémarrage de l'application.

## Utilisation du lecteur

1. Ouvrir `https://mon-domaine.fr` sur Safari (iPhone).
2. La liste des MP3 disponibles s'affiche.
3. Sélectionner un morceau (il est chargé mais pas joué).
4. Appuyer sur **Lecture** (obligatoire : les navigateurs bloquent l'autoplay).
5. À la fin du morceau, le suivant démarre automatiquement.
6. En fin de liste, la playlist revient au premier morceau.

### Contrôles depuis l'écran verrouillé (iOS)
- Une fois la lecture lancée, verrouiller l'iPhone : les commandes Lecture/Pause, Précédent/Suivant et le titre s'affichent sur l'écran verrouillé et dans le Centre de contrôle (via l'API Media Session lorsque prise en charge par iOS).

> Rappel : le démarrage de la lecture doit impérativement résulter d'une action explicite de l'utilisateur.

## Procédure de test sur iPhone

### Test 1 – Fonctionnement classique
- Ouvrir le site sur Safari.
- Vérifier que les MP3 sont détectés.
- Lancer un morceau et vérifier la lecture.
- Tester les boutons Précédent et Suivant.

### Test 2 – Enchaînement automatique
- Lancer le premier morceau.
- Attendre la fin du morceau.
- Vérifier que le suivant démarre automatiquement.
- Vérifier que la playlist boucle correctement.

### Test 3 – Écran verrouillé
- Démarrer une musique.
- Verrouiller l'iPhone.
- Attendre plusieurs minutes.
- Vérifier que la lecture continue et que le morceau suivant démarre.

### Test 4 – Commandes iOS
Vérifier depuis l'écran verrouillé / Centre de contrôle :
- Lecture / Pause.
- Morceau suivant.
- Morceau précédent.
- Affichage du titre du morceau.

### Test 5 – Endurance (30 à 60 minutes)
- Lancer une playlist de plusieurs morceaux.
- Vérifier :
  - Aucune interruption inexpliquée.
  - Aucun blocage lors des changements de morceaux.
  - Aucun rechargement de page.
  - Aucun arrêt de la playlist.
  - Fonctionnement stable écran verrouillé.

### Test 6 – Streaming indépendant du stockage local
- Ne pas utiliser IndexedDB (le POC n'en utilise pas).
- Vider les données de site Safari avant le test.
- Vérifier que les MP3 sont lus depuis le VPS.
- Vérifier les requêtes HTTP Range (ex. avec les outils de développement de Safari ou un proxy) : le serveur répond `206 Partial Content` avec `Accept-Ranges: bytes` et `Content-Range`.

> Le cache interne de Safari peut subsister indépendamment de l'application. Ce comportement est normal ; le POC ne conserve volontairement aucun fichier complet.

## Limites connues de Safari et iOS

| Point | Détail |
|-------|--------|
| Autoplay | Safari bloque la lecture automatique. L'utilisateur doit appuyer sur **Lecture** au moins une fois. |
| Media Session API | Le support de iOS Safari est partiel. Les commandes d'écran verrouillé reposent principalement sur l'élément `<audio>` en cours de lecture. |
| Arrière-plan | La lecture continue généralement si l'élément audio joue et que la page n'est pas rechargée, mais iOS peut suspendre les onglets inactifs selon son gestionnaire de mémoire. À valider sur un vrai iPhone. |
| Durée longue (30-60 min) | Non garanti par le POC ; dépend d'iOS, de l'état de la batterie, du mode économie d'énergie et du réseau. |
| Mixage audio | D'autres apps (appels, vidéos) peuvent interrompre la lecture, comportement propre à iOS. |

Si un critère ne peut pas être satisfait en raison d'une limitation d'iOS, il doit être documenté ici plutôt que présenté comme fonctionnel.

## Critères de validation du POC

Le POC est réussi lorsque :
1. Les MP3 sont déposés manuellement sur le VPS.
2. Le site détecte les fichiers (`GET /api/tracks`).
3. Le lecteur lit les fichiers directement en streaming HTTP.
4. Les requêtes HTTP Range fonctionnent (`206 Partial Content`, `Accept-Ranges: bytes`).
5. L'enchaînement automatique des morceaux fonctionne.
6. L'iPhone peut être verrouillé pendant la lecture.
7. La musique continue d'être diffusée écran verrouillé.
8. Les morceaux s'enchaînent sans intervention.
9. Les commandes de lecture iOS fonctionnent lorsque prises en charge.
10. Un test de 30 à 60 minutes se déroule sans interruption inexpliquée.

## Licence

Usage personnel. POC sans garantie.

