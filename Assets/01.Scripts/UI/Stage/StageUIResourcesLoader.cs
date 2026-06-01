using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using VContainer.Unity;

/// <summary>
/// StageScene UI 스프라이트 SO를 Addressables로 로드해 StageUIController에 주입한다.
/// (Framework.Lobby.LobbyResourcesLoader 패턴 차용)
/// </summary>
public class StageUIResourcesLoader : IAsyncStartable, IDisposable
{
    private const string ImagesKey = "StageUIImageLibrary";
    private const string SettingKey = "SettingUIImageLibrary";

    private readonly StageUIController _controller;
    private AsyncOperationHandle<StageUIImageLibrarySO> _imagesHandle;
    private AsyncOperationHandle<SettingUIImageLibrarySO> _settingHandle;

    public StageUIResourcesLoader(StageUIController controller)
    {
        _controller = controller;
    }

    public UniTask StartAsync(System.Threading.CancellationToken cancellation)
    {
        _imagesHandle = Addressables.LoadAssetAsync<StageUIImageLibrarySO>(ImagesKey);
        _settingHandle = Addressables.LoadAssetAsync<SettingUIImageLibrarySO>(SettingKey);

        var images = _imagesHandle.WaitForCompletion();
        var setting = _settingHandle.WaitForCompletion();

        if (_controller != null)
        {
            _controller.Initialize(images, setting);
        }

        Debug.Log("[StageUIResourcesLoader] 스테이지 UI 리소스 로드 완료");
        return UniTask.CompletedTask;
    }

    public void Dispose()
    {
        if (_imagesHandle.IsValid()) Addressables.Release(_imagesHandle);
        if (_settingHandle.IsValid()) Addressables.Release(_settingHandle);
    }
}
