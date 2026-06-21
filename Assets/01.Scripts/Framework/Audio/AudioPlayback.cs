using JSAM;
using UnityEngine;

public static class AudioPlayback
{
    static AudioManager _cachedManager;

    public static bool IsReady => TryGetReadyManager(out _);

    public static float MusicVolumeOrDefault(float defaultValue) =>
        TryGetReadyManager(out _) ? AudioManager.MusicVolume : defaultValue;

    public static float SoundVolumeOrDefault(float defaultValue) =>
        TryGetReadyManager(out _) ? AudioManager.SoundVolume : defaultValue;

    public static void SetMusicVolume(float value)
    {
        if (!TryGetReadyManager(out _)) return;
        AudioManager.MusicVolume = value;
    }

    public static void SetSoundVolume(float value)
    {
        if (!TryGetReadyManager(out _)) return;
        AudioManager.SoundVolume = value;
    }

    public static void PlaySound(_AudioLibrarySounds sound)
    {
        if (!TryGetReadyManager(out _)) return;
        AudioManager.PlaySound(sound);
    }

    public static void PlayMusic(_AudioLibraryMusic music, bool stopCurrent = false)
    {
        if (!TryGetReadyManager(out _)) return;
        if (stopCurrent) AudioManager.StopAllMusic();
        AudioManager.PlayMusic(music);
    }

    public static void StopAllMusic()
    {
        if (!TryGetReadyManager(out _)) return;
        AudioManager.StopAllMusic();
    }

    static bool TryGetReadyManager(out AudioManager manager)
    {
        manager = GetManager();
        return manager != null && manager.Initialized;
    }

    static AudioManager GetManager()
    {
        if (_cachedManager != null)
            return _cachedManager;

#if UNITY_2023_1_OR_NEWER
        _cachedManager = UnityEngine.Object.FindFirstObjectByType<AudioManager>();
#else
        _cachedManager = UnityEngine.Object.FindObjectOfType<AudioManager>();
#endif
        return _cachedManager;
    }
}
