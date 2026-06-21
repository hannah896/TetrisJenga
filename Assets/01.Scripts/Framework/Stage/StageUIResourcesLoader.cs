using System;
using Cysharp.Threading.Tasks;
using JSAM;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using VContainer.Unity;

/// <summary>
/// StageScene UI 에셋을 Addressables로 로드해 StageUIController에 주입한다.
/// Addressables 등록 키:
///   "StageUIImageLibrary"  → StageUIImageLibrarySO
///   "SettingUIImageLibrary" → SettingUIImageLibrarySO
///   "StageMapData"          → StageMapSO
/// </summary>
public class StageUIResourcesLoader : IAsyncStartable, IDisposable
{
    private const string ImagesKey = "StageUIImageLibrary";
    private const string SettingKey = "SettingUIImageLibrary";
    private const string MapKey = "StageMapData";

    private readonly StageUIController _controller;
    private AsyncOperationHandle<StageUIImageLibrarySO> _imagesHandle;
    private AsyncOperationHandle<SettingUIImageLibrarySO> _settingHandle;
    private AsyncOperationHandle<StageMapSO> _mapHandle;

    public StageUIResourcesLoader(StageUIController controller)
    {
        _controller = controller;
    }

    public UniTask StartAsync(System.Threading.CancellationToken cancellation)
    {
        _imagesHandle = Addressables.LoadAssetAsync<StageUIImageLibrarySO>(ImagesKey);
        _settingHandle = Addressables.LoadAssetAsync<SettingUIImageLibrarySO>(SettingKey);
        _mapHandle = Addressables.LoadAssetAsync<StageMapSO>(MapKey);

        var images = _imagesHandle.WaitForCompletion();
        var setting = _settingHandle.WaitForCompletion();
        var map = _mapHandle.WaitForCompletion();

        if (_controller != null)
        {
            _controller.Initialize(images, setting, map);
        }

        AudioPlayback.PlayMusic(_AudioLibraryMusic.StageBGM, stopCurrent: true);

        Debug.Log("[StageUIResourcesLoader] 스테이지 UI 리소스 로드 완료");
        return UniTask.CompletedTask;
    }

    public void Dispose()
    {
        if (_imagesHandle.IsValid()) Addressables.Release(_imagesHandle);
        if (_settingHandle.IsValid()) Addressables.Release(_settingHandle);
        if (_mapHandle.IsValid()) Addressables.Release(_mapHandle);
    }
}
