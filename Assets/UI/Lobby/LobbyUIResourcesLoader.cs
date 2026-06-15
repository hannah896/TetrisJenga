using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using VContainer.Unity;

/// <summary>
/// LobbyScene UI 스프라이트 SO를 Addressables로 로드해 LobbyUIController에 주입한다.
/// VContainer EntryPoint로 등록되어 씬 시작 시 StartAsync가 자동 실행된다.
/// (Framework.Lobby.LobbyResourcesLoader 패턴 차용)
/// </summary>
public class LobbyUIResourcesLoader : IAsyncStartable, IDisposable
{
    private const string ImagesKey = "LobbyUIImageLibrary";
    private const string SettingKey = "SettingUIImageLibrary";

    private readonly LobbyUIController _controller;
    private AsyncOperationHandle<LobbyUIImageLibrarySO> _imagesHandle;
    private AsyncOperationHandle<SettingUIImageLibrarySO> _settingHandle;

    public LobbyUIResourcesLoader(LobbyUIController controller)
    {
        _controller = controller;
    }

    public UniTask StartAsync(System.Threading.CancellationToken cancellation)
    {
        _imagesHandle = Addressables.LoadAssetAsync<LobbyUIImageLibrarySO>(ImagesKey);
        _settingHandle = Addressables.LoadAssetAsync<SettingUIImageLibrarySO>(SettingKey);

        var images = _imagesHandle.WaitForCompletion();
        var setting = _settingHandle.WaitForCompletion();

        if (_controller != null)
        {
            _controller.Initialize(images, setting);
        }

        Debug.Log("[LobbyUIResourcesLoader] 로비 UI 리소스 로드 완료");
        return UniTask.CompletedTask;
    }

    public void Dispose()
    {
        if (_imagesHandle.IsValid()) Addressables.Release(_imagesHandle);
        if (_settingHandle.IsValid()) Addressables.Release(_settingHandle);
    }
}
