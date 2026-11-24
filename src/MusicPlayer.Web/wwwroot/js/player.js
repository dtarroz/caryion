/* Music Player – POC streaming MP3 */
/* Logique du lecteur : HTML5 <audio> unique, playlist, Media Session API, streaming HTTP. */

(function () {
  'use strict';

  const API_TRACKS = 'api/tracks';

  // --- Éléments DOM ---
  const audioPlayer = document.getElementById('audioPlayer');
  const btnPlay = document.getElementById('btnPlay');
  const btnPrev = document.getElementById('btnPrev');
  const btnNext = document.getElementById('btnNext');
  const sldProgress = document.getElementById('sldProgress');
  const txtCurrent = document.getElementById('txtCurrent');
  const txtDuration = document.getElementById('txtDuration');
  const npTitle = document.getElementById('npTitle');
  const npArtist = document.getElementById('npArtist');
  const npAlbum = document.getElementById('npAlbum');
  const trackList = document.getElementById('trackList');
  const countEl = document.getElementById('count');
  const emptyMsg = document.getElementById('emptyMsg');
  const statusEl = document.getElementById('status');

  // --- État ---
  let tracks = [];          // liste des morceaux (depuis api/tracks)
  let currentIndex = -1;    // index du morceau courant (-1 = aucun)
  let isUserSeeking = false; // vrai pendant que l'utilisateur déplace la barre de progression
  let playbackStarted = false; // vrai après le premier play() (pour ne pas relancer l'autoplay)

  // --- Initialisation ---
  async function init() {
    await loadTracks();
    bindEvents();
    initMediaSession();
    setStatus('Prêt. Sélectionnez un morceau et appuyez sur Lecture.');
  }
  // --- Chargement de la playlist ---
  async function loadTracks() {
    try {
      const response = await fetch(API_TRACKS, { cache: 'no-cache' });
      if (!response.ok) throw new Error(`HTTP ${response.status}`);
      tracks = await response.json();

      renderPlaylist();
      countEl.textContent = tracks.length;

      if (tracks.length === 0) {
        emptyMsg.hidden = false;
        setStatus('Aucun fichier MP3 trouvé sur le serveur.');
        return;
      }

      emptyMsg.hidden = true;
      // Sélectionner le premier morceau par défaut (sans lancer la lecture : pas d'autoplay).
      selectTrack(0);
      setStatus(`${tracks.length} morceau(s) détecté(s). Appuyez sur Lecture.`);
    } catch (err) {
      console.error(err);
      emptyMsg.hidden = false;
      setStatus('Impossible de charger la liste des morceaux.');
    }
  }

  // --- Sélection d'un morceau ---
  function selectTrack(index) {
    if (index < 0 || index >= tracks.length) return;

    currentIndex = index;
    const track = tracks[currentIndex];

    // Mise à jour de l'URL de l'élément audio (streaming HTTP direct).
    // Un seul élément audio est conservé pour toute la session (pas de recréation).
    audioPlayer.src = track.url;

    // Mise à jour de l'interface "now playing".
    // Note : le JSON de l'API ASP.NET Core utilise la casse camelCase (title, url, size...).
    npTitle.textContent = track.title;
    npArtist.textContent = '—';
    npAlbum.textContent = '—';

    // Réinitialiser la progression visuelle.
    txtCurrent.textContent = '0:00';
    txtDuration.textContent = '0:00';
    sldProgress.value = 0;

    // Mise à jour de la sélection visuelle dans la playlist.
    renderPlaylist();

    // Mise à jour des métadonnées Media Session (verrouillage d'écran / centre de contrôle).
    updateMediaSessionMetadata(track);

    setStatus(`Morceau sélectionné : ${track.title}.`);
  }

  // --- Lecture / Pause ---
  async function togglePlay() {
    if (currentIndex < 0) return;

    try {
      if (audioPlayer.paused) {
        await audioPlayer.play();
        playbackStarted = true;
      } else {
        audioPlayer.pause();
      }
    } catch (err) {
      console.error('Erreur de lecture :', err);
      setStatus('Impossible de démarrer la lecture.');
    }

    updatePlayButton();
    updateMediaSessionPlaybackState();
  }

  function updatePlayButton() {
    btnPlay.textContent = audioPlayer.paused ? '▶ Lecture' : '⏸ Pause';
  }

  // --- Morceau précédent / suivant ---
  function prevTrack() {
    const next = (currentIndex - 1 + tracks.length) % tracks.length;
    selectTrack(next);
    if (!audioPlayer.paused || playbackStarted) {
      audioPlayer.play().catch(() => {});
    }
  }

  function nextTrack() {
    const next = (currentIndex + 1) % tracks.length;
    selectTrack(next);
    if (!audioPlayer.paused || playbackStarted) {
      audioPlayer.play().catch(() => {});
    }
  }

  // --- Enchaînement automatique à la fin d'un morceau ---
  function handleEnded() {
    // Retour au premier morceau à la fin de la liste (boucle infinie).
    nextTrack();
  }

  // --- Mise à jour de la barre de progression ---
  function updateProgress() {
    if (isUserSeeking || isNaN(audioPlayer.duration)) return;

    const current = audioPlayer.currentTime || 0;
    const duration = audioPlayer.duration || 0;

    txtCurrent.textContent = formatTime(current);
    if (duration > 0) {
      txtDuration.textContent = formatTime(duration);
      sldProgress.max = duration;
      sldProgress.value = current;
    }
  }

  // --- Recherche (seek) sur la barre de progression ---
  sldProgress.addEventListener('input', () => {
    isUserSeeking = true;
    txtCurrent.textContent = formatTime(parseFloat(sldProgress.value));
  });

  sldProgress.addEventListener('change', () => {
    const targetTime = parseFloat(sldProgress.value);
    if (!isNaN(targetTime)) {
      audioPlayer.currentTime = targetTime;
    }
    isUserSeeking = false;
  });

  // --- Formatage du temps (mm:ss) ---
  function formatTime(seconds) {
    const s = Math.max(0, Math.floor(seconds || 0));
    const m = Math.floor(s / 60);
    const r = s % 60;
    return `${m}:${String(r).padStart(2, '0')}`;
  }

  // --- Taille des fichiers (lecture humaine) ---
  function formatSize(bytes) {
    if (bytes == null) return '';
    const units = ['o', 'Ko', 'Mo', 'Go'];
    let size = bytes;
    let u = 0;
    while (size >= 1024 && u < units.length - 1) {
      size /= 1024;
      u++;
    }
    return `${size.toFixed(u === 0 ? 0 : 1)} ${units[u]}`;
  }

  // --- Rendu de la playlist ---
  function renderPlaylist() {
    trackList.innerHTML = '';
    tracks.forEach((track, index) => {
      const li = document.createElement('li');
      li.className = 'track-item' + (index === currentIndex ? ' active' : '');
      li.innerHTML = `
        <span class="track-number">${index + 1}</span>
        <span class="track-title">${escapeHtml(track.title)}</span>
        <span class="track-size">${formatSize(track.size)}</span>
      `;
      li.addEventListener('click', () => {
        selectTrack(index);
        // Ne lance pas la lecture automatiquement : l'utilisateur doit appuyer sur Lecture.
      });
      trackList.appendChild(li);
    });
  }

  // --- Échappement HTML (sécurité d'affichage) ---
  function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text ?? '';
    return div.innerHTML;
  }

  // --- Statut ---
  function setStatus(message) {
    if (statusEl) statusEl.textContent = message;
  }

  // --- Événements de l'élément audio ---
  function bindMediaEvents() {
    audioPlayer.addEventListener('loadedmetadata', () => {
      txtDuration.textContent = formatTime(audioPlayer.duration || 0);
      sldProgress.max = audioPlayer.duration || 0;
    });

    audioPlayer.addEventListener('timeupdate', updateProgress);

    audioPlayer.addEventListener('play', () => {
      updatePlayButton();
      updateMediaSessionPlaybackState();
    });

    audioPlayer.addEventListener('pause', () => {
      updatePlayButton();
      updateMediaSessionPlaybackState();
    });

    audioPlayer.addEventListener('ended', handleEnded);

    audioPlayer.addEventListener('error', () => {
      console.error('Erreur audio :', audioPlayer.error);
      setStatus('Erreur lors de la lecture du fichier.');
      updatePlayButton();
      updateMediaSessionPlaybackState();
    });
  }

  function bindEvents() {
    btnPlay.addEventListener('click', togglePlay);
    btnPrev.addEventListener('click', prevTrack);
    btnNext.addEventListener('click', nextTrack);
    bindMediaEvents();
  }

  // --- Media Session API (verrouillage d'écran / centre de contrôle iOS) ---
  function initMediaSession() {
    if (!('mediaSession' in navigator)) return;

    try {
      navigator.mediaSession.setActionHandler('play', togglePlay);
      navigator.mediaSession.setActionHandler('pause', togglePlay);
      navigator.mediaSession.setActionHandler('previoustrack', prevTrack);
      navigator.mediaSession.setActionHandler('nexttrack', nextTrack);
    } catch (err) {
      console.warn('Media Session handlers non pris en charge :', err);
    }
  }

  function updateMediaSessionMetadata(track) {
    if (!('mediaSession' in navigator)) return;
    try {
      navigator.mediaSession.metadata = new MediaMetadata({
        title: track.Title,
        artist: '—',
        album: '—',
        artwork: [] // pas de pochette dans cette version POC
      });
    } catch (err) {
      console.warn('Media Session metadata non pris en charge :', err);
    }
  }

  function updateMediaSessionPlaybackState() {
    if (!('mediaSession' in navigator)) return;
    try {
      navigator.mediaSession.playbackState = audioPlayer.paused ? 'paused' : 'playing';
    } catch (err) {
      // playbackState peut ne pas être pris en charge partout.
    }
  }

  // Démarrage.
  init();

})();

