using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
public class AudioManager : MonoBehaviour
{
    private const string DefaultCueLibraryPath = "Audio/DefaultAudioCueLibrary";
    private static readonly HashSet<string> DisabledSfxCueIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "purgatory_lose",
        "purgatory_win",
        "boss_death"
    };
    private static readonly string[] DisabledSfxClipNameFragments =
    {
        "game over",
        "level completed",
        "you win"
    };

    private static AudioManager instance;

    [Header("Cue Library")]
    [SerializeField] private AudioCueLibrary cueLibrary;
    [SerializeField] private string resourcesCueLibraryPath = DefaultCueLibraryPath;

    [Header("Mix")]
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.42f;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.85f;
    [SerializeField, Min(1)] private int sfxSourcePoolSize = 4;

    [Header("Scene Music")]
    [SerializeField] private bool playSceneMusicOnLoad = true;

    private readonly Dictionary<string, AudioCueLibrary.AudioCueEntry> cueLookup =
        new Dictionary<string, AudioCueLibrary.AudioCueEntry>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> warnedMissingCueIds =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private AudioSource musicSource;
    private AudioSource[] sfxSources = Array.Empty<AudioSource>();
    private int nextSfxSourceIndex;
    private Coroutine musicFadeRoutine;
    private string currentMusicCueId = string.Empty;
    private bool warnedMissingLibrary;

    public static AudioManager Instance => GetOrCreateInstance();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void BootstrapOnLoad()
    {
        AudioManager manager = GetOrCreateInstance();
        if (manager != null)
            manager.ReloadCueLookup();
    }

    public static AudioManager GetOrCreateInstance()
    {
        if (instance != null)
            return instance;

        instance = FindFirstObjectByType<AudioManager>();
        if (instance != null)
            return instance;

        GameObject managerObject = new GameObject("AudioManager");
        instance = managerObject.AddComponent<AudioManager>();
        return instance;
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        EnsureAudioSources();
        ReloadCueLookup();

        if (playSceneMusicOnLoad)
            PlayMusicForScene(SceneManager.GetActiveScene().name);
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    public void PlayMusic(string cueId, bool restartIfSame = false)
    {
        if (string.IsNullOrWhiteSpace(cueId))
        {
            StopMusic();
            return;
        }

        EnsureAudioSources();
        if (!TryGetCue(cueId, out AudioCueLibrary.AudioCueEntry cue))
            return;

        if (!restartIfSame &&
            musicSource.isPlaying &&
            string.Equals(currentMusicCueId, cueId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        StopMusicFade();

        currentMusicCueId = cue.id;
        musicSource.clip = cue.clip;
        musicSource.loop = cue.loop;
        musicSource.pitch = 1f;
        musicSource.volume = Mathf.Clamp01(cue.defaultVolume) * musicVolume;
        musicSource.Play();
    }

    public void StopMusic(float fadeOut = 0f)
    {
        if (musicSource == null)
            return;

        if (fadeOut <= 0f)
        {
            StopMusicFade();
            musicSource.Stop();
            musicSource.clip = null;
            currentMusicCueId = string.Empty;
            return;
        }

        StopMusicFade();
        musicFadeRoutine = StartCoroutine(FadeOutMusicRoutine(fadeOut));
    }

    public void PlaySfx(string cueId, float volume = 1f, float pitch = 1f)
    {
        if (string.IsNullOrWhiteSpace(cueId))
            return;
        if (DisabledSfxCueIds.Contains(cueId))
            return;

        EnsureAudioSources();
        if (!TryGetCue(cueId, out AudioCueLibrary.AudioCueEntry cue))
            return;
        if (IsDisabledSfxClip(cue.clip))
            return;

        AudioSource source = GetNextSfxSource();
        if (source == null)
            return;

        source.loop = false;
        source.clip = cue.clip;
        source.pitch = cue.GetDefaultPitch() * Mathf.Max(0.01f, pitch);
        source.volume = Mathf.Clamp01(cue.defaultVolume * Mathf.Clamp01(volume)) * sfxVolume;
        source.Play();
    }

    private static bool IsDisabledSfxClip(AudioClip clip)
    {
        if (clip == null || string.IsNullOrWhiteSpace(clip.name))
            return false;

        for (int i = 0; i < DisabledSfxClipNameFragments.Length; i++)
        {
            if (clip.name.IndexOf(DisabledSfxClipNameFragments[i], StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);

        if (musicSource != null && musicSource.clip != null && !string.IsNullOrWhiteSpace(currentMusicCueId))
        {
            if (TryGetCue(currentMusicCueId, out AudioCueLibrary.AudioCueEntry cue))
                musicSource.volume = Mathf.Clamp01(cue.defaultVolume) * musicVolume;
        }
    }

    public void SetSfxVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
    }

    public static void TryPlaySfx(string cueId, float volume = 1f, float pitch = 1f)
    {
        if (string.IsNullOrWhiteSpace(cueId))
            return;

        GetOrCreateInstance().PlaySfx(cueId, volume, pitch);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode loadMode)
    {
        EnsureAudioSources();
        ReloadCueLookup();

        if (playSceneMusicOnLoad)
            PlayMusicForScene(scene.name);
    }

    private void PlayMusicForScene(string sceneName)
    {
        if (string.Equals(sceneName, "Purgatory", StringComparison.Ordinal))
        {
            StopMusic();
            return;
        }

        string cueId;

        switch (sceneName)
        {
            case "MainMenu":
                cueId = "menu_music";
                break;
            case "Zone1_Test":
                cueId = "zone1_music";
                break;
            case "Zone2_Mire":
                cueId = "zone2_music";
                break;
            case "Zone3_Nexus":
                cueId = "zone3_music";
                break;
            case "Credits":
                cueId = "menu_music";
                break;
            default:
                cueId = string.Empty;
                break;
        }

        if (string.IsNullOrWhiteSpace(cueId))
            return;

        PlayMusic(cueId);
    }

    private void EnsureCueLookup()
    {
        if (cueLookup.Count == 0)
            ReloadCueLookup();
    }

    private void ReloadCueLookup()
    {
        cueLookup.Clear();

        AudioCueLibrary loadedCueLibrary = string.IsNullOrWhiteSpace(resourcesCueLibraryPath)
            ? null
            : Resources.Load<AudioCueLibrary>(resourcesCueLibraryPath);
        if (loadedCueLibrary != null)
            cueLibrary = loadedCueLibrary;

        if (cueLibrary == null)
        {
            if (!warnedMissingLibrary)
            {
                Debug.LogWarning(
                    $"AudioManager could not load cue library at Resources/{resourcesCueLibraryPath}.",
                    this);
                warnedMissingLibrary = true;
            }

            return;
        }

        warnedMissingLibrary = false;

        AudioCueLibrary.AudioCueEntry[] cues = cueLibrary.Cues;
        for (int i = 0; i < cues.Length; i++)
        {
            AudioCueLibrary.AudioCueEntry cue = cues[i];
            if (string.IsNullOrWhiteSpace(cue.id) || cue.clip == null)
                continue;

            cueLookup[cue.id] = cue;
        }
    }

    private bool TryGetCue(string cueId, out AudioCueLibrary.AudioCueEntry cue)
    {
        EnsureCueLookup();

        if (cueLookup.TryGetValue(cueId, out cue))
            return true;

        if (warnedMissingCueIds.Add(cueId))
            Debug.LogWarning($"AudioManager is missing cue '{cueId}'.", this);

        return false;
    }

    private void EnsureAudioSources()
    {
        AudioSource[] existingSources = GetComponents<AudioSource>();
        int requiredSourceCount = Mathf.Max(2, sfxSourcePoolSize + 1);

        if (existingSources.Length < requiredSourceCount)
        {
            for (int i = existingSources.Length; i < requiredSourceCount; i++)
                gameObject.AddComponent<AudioSource>();

            existingSources = GetComponents<AudioSource>();
        }

        musicSource = existingSources[0];
        ConfigureMusicSource(musicSource);

        int requiredSfxSources = Mathf.Max(1, sfxSourcePoolSize);
        if (sfxSources.Length != requiredSfxSources)
            sfxSources = new AudioSource[requiredSfxSources];

        for (int i = 0; i < requiredSfxSources; i++)
        {
            AudioSource source = existingSources[i + 1];
            ConfigureSfxSource(source);
            sfxSources[i] = source;
        }
    }

    private void ConfigureMusicSource(AudioSource source)
    {
        if (source == null)
            return;

        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.priority = 96;
    }

    private void ConfigureSfxSource(AudioSource source)
    {
        if (source == null)
            return;

        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.priority = 64;
    }

    private AudioSource GetNextSfxSource()
    {
        if (sfxSources.Length == 0)
            return null;

        for (int i = 0; i < sfxSources.Length; i++)
        {
            int candidateIndex = (nextSfxSourceIndex + i) % sfxSources.Length;
            AudioSource candidate = sfxSources[candidateIndex];
            if (candidate != null && !candidate.isPlaying)
            {
                nextSfxSourceIndex = (candidateIndex + 1) % sfxSources.Length;
                return candidate;
            }
        }

        AudioSource fallback = sfxSources[nextSfxSourceIndex];
        nextSfxSourceIndex = (nextSfxSourceIndex + 1) % sfxSources.Length;
        return fallback;
    }

    private void StopMusicFade()
    {
        if (musicFadeRoutine == null)
            return;

        StopCoroutine(musicFadeRoutine);
        musicFadeRoutine = null;
    }

    private IEnumerator FadeOutMusicRoutine(float duration)
    {
        if (musicSource == null)
            yield break;

        float startVolume = musicSource.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            musicSource.volume = Mathf.Lerp(startVolume, 0f, t);
            yield return null;
        }

        musicSource.Stop();
        musicSource.clip = null;
        musicSource.volume = startVolume;
        currentMusicCueId = string.Empty;
        musicFadeRoutine = null;
    }
}
