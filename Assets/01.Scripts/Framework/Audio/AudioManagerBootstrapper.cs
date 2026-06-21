using Cysharp.Threading.Tasks;
using JSAM;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;

public class AudioManagerBootstrapper : MonoBehaviour
{
    [SerializeField] _AudioLibraryMusic bgm = _AudioLibraryMusic.EndlessBGM;
    [SerializeField] string audioManagerAddress = "Audio Manager Variant";
    [SerializeField] float audioReadyTimeoutSeconds = 5f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void BootstrapAudioForDirectSceneStart()
    {
        if (!Application.isPlaying || AudioPlayback.IsReady) return;
        if (!TryGetSceneBGM(SceneManager.GetActiveScene().name, out var sceneBgm)) return;

        var go = new GameObject(nameof(AudioManagerBootstrapper));
        var bootstrapper = go.AddComponent<AudioManagerBootstrapper>();
        bootstrapper.bgm = sceneBgm;
    }

    async void Start()
    {
        if (!AudioPlayback.IsReady)
            await BootstrapAudioManager();

        PlayBGM();
        Destroy(gameObject);
    }

    async UniTask BootstrapAudioManager()
    {
        var audioHandle = Addressables.InstantiateAsync(audioManagerAddress);
        var audioManager = audioHandle.WaitForCompletion();

        if (audioHandle.Status != AsyncOperationStatus.Succeeded || audioManager == null)
        {
            Debug.LogWarning($"[AudioManagerBootstrapper] AudioManager load failed: {audioManagerAddress}");
            return;
        }

        float deadline = Time.realtimeSinceStartup + audioReadyTimeoutSeconds;
        while (!AudioPlayback.IsReady && Time.realtimeSinceStartup < deadline)
        {
            await UniTask.Yield(PlayerLoopTiming.Update, destroyCancellationToken);
        }

        if (!AudioPlayback.IsReady)
            Debug.LogWarning("[AudioManagerBootstrapper] Timed out waiting for AudioManager initialization");
    }

    void PlayBGM()
    {
        AudioPlayback.PlayMusic(bgm, stopCurrent: true);
    }

    static bool TryGetSceneBGM(string sceneName, out _AudioLibraryMusic sceneBgm)
    {
        if (sceneName == "Endless")
        {
            sceneBgm = _AudioLibraryMusic.EndlessBGM;
            return true;
        }

        if (sceneName == "StageScene")
        {
            sceneBgm = _AudioLibraryMusic.StageBGM;
            return true;
        }

        const string levelPrefix = "Level";
        if (sceneName.StartsWith(levelPrefix) &&
            int.TryParse(sceneName.Substring(levelPrefix.Length), out int level) &&
            level >= 1 &&
            level <= 6)
        {
            sceneBgm = (_AudioLibraryMusic)((int)_AudioLibraryMusic.Stage1 + level - 1);
            return true;
        }

        sceneBgm = default;
        return false;
    }
}
