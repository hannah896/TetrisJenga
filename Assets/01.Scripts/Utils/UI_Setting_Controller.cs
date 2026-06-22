using System;
using JSAM;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// 모든 씬이 공유하는 설정 팝업 로직. MonoBehaviour가 아니라 각 씬 컨트롤러가
/// 인스턴스로 보유하고 Initialize(root, ...)로 바인딩한다.
/// 설정 팝업 마크업(name="setting-popup" 등)은 각 씬 uxml에 포함되어 있어야 한다.
/// </summary>
public class UI_Setting_Controller
{
    private const string PresetGuideKey = "Settings.PresetGuide";
    private const string BlockNumberTextKey = "Settings.BlockNumberText";
    private const string ImageSimplificationKey = "Settings.ImageSimplification";
    private const float DefaultVolume = 1f;
    
    private VisualElement popup;
    private Slider bgmSlider;
    private Slider sfxSlider;
    private Toggle presetGuideToggle;
    private Toggle blockNumberToggle;
    private Toggle imageSimplificationToggle;
    private Label presetGuideLabel;
    private Label blockNumberLabel;
    private Label imageSimplificationLabel;
    private Label bgmLabel;
    private Label sfxLabel;
    private Button restartButton;
    private Button stageButton;
    private Button backButton;
    private SettingUIImageLibrarySO currentImages;
    private float timeScaleBeforePause = 1f;
    private bool pausedByPopup;
    
    private const float VolumeStep = 0.075f;



    public bool IsOpen => popup != null && !popup.ClassListContains("hidden");
    public static bool IsBlockNumberTextVisible => PlayerPrefs.GetInt(BlockNumberTextKey, 1) == 1;
    public static bool IsImageSimplificationEnabled => PlayerPrefs.GetInt(ImageSimplificationKey, 0) == 1;
    public static event Action<bool> BlockNumberTextVisibilityChanged;
    public static event Action<bool> ImageSimplificationChanged;

    /// <param name="root">UIDocument rootVisualElement</param>
    /// <param name="images">설정 스프라이트 SO (없으면 USS 기본 색 사용)</param>
    /// <param name="onRestart">Restart 버튼 동작 (씬마다 다름, null 허용)</param>
    public void Initialize(VisualElement root, SettingUIImageLibrarySO images)
    {
        UnbindCallbacks();

        popup = root.Q<VisualElement>("setting-popup");
        if (popup == null)
        {
            Debug.LogError("SettingPopupController: 'setting-popup' 요소를 찾을 수 없습니다. 씬 uxml에 설정 팝업 마크업이 있는지 확인하세요.");
            return;
        }

        // A UXML template instance creates a full-screen TemplateContainer around
        // the popup. Ignore picking on that transparent wrapper so it cannot block
        // the Lobby/Stage UI while the popup itself is hidden.
        if (popup.parent != null && popup.parent != root)
        {
            popup.parent.pickingMode = PickingMode.Ignore;
        }

        bgmSlider = root.Q<Slider>("bgm-slider");
        sfxSlider = root.Q<Slider>("sfx-slider");
        presetGuideToggle = root.Q<Toggle>("preset-guide-toggle");
        blockNumberToggle = root.Q<Toggle>("block-number-toggle");
        imageSimplificationToggle = root.Q<Toggle>("image-simplification-toggle");
        presetGuideLabel = root.Q<Label>("preset-guide-label");
        blockNumberLabel = root.Q<Label>("block-number-label");
        imageSimplificationLabel = root.Q<Label>("image-simplification-label");
        bgmLabel = root.Q<Label>("bgm-label");
        sfxLabel = root.Q<Label>("sfx-label");
        restartButton = root.Q<Button>("setting-restart-button");
        stageButton = root.Q<Button>("setting-stage-button");
        backButton = root.Q<Button>("setting-back-button");
        currentImages = images;
        LoadState();
        ApplySprites(images);
        EnableInteraction();

        bgmSlider?.RegisterValueChangedCallback(HandleBgmChanged);
        sfxSlider?.RegisterValueChangedCallback(HandleSfxChanged);
        presetGuideToggle?.RegisterValueChangedCallback(HandlePresetGuideChanged);
        blockNumberToggle?.RegisterValueChangedCallback(HandleBlockNumberChanged);
        imageSimplificationToggle?.RegisterValueChangedCallback(HandleImageSimplificationChanged);
        if (restartButton != null) restartButton.clicked += HandleRestartClicked;
        if (backButton != null) backButton.clicked += Hide;
        if (stageButton != null) stageButton.clicked += HandleStageClicked;

        Hide();
    }

    public void Show()
    {
        if (popup == null) return;
        PauseGame();
        EnableInteraction();
        popup.BringToFront();
        popup.RemoveFromClassList("hidden");
    }

    public void Hide()
    {
        popup?.AddToClassList("hidden");
        ResumeGame();
    }

    public void Dispose()
    {
        UnbindCallbacks();
        ResumeGame();
    }

    public void Toggle()
    {
        if (IsOpen)
        {
            Hide();
        }
        else
        {
            Show();
        }
    }

    private void LoadState()
    {
        bgmSlider?.SetValueWithoutNotify(ReadMusicVolume());
        sfxSlider?.SetValueWithoutNotify(ReadSoundVolume());
        presetGuideToggle?.SetValueWithoutNotify(PlayerPrefs.GetInt(PresetGuideKey, 1) == 1);
        blockNumberToggle?.SetValueWithoutNotify(IsBlockNumberTextVisible);
        imageSimplificationToggle?.SetValueWithoutNotify(IsImageSimplificationEnabled);
    }

    private void EnableInteraction()
    {
        if (popup == null) return;
        popup.pickingMode = PickingMode.Position;
        popup.SetEnabled(true);
        popup.Query<Button>().ForEach(button =>
        {
            button.pickingMode = PickingMode.Position;
            button.SetEnabled(true);
        });
        popup.Query<Slider>().ForEach(slider =>
        {
            slider.pickingMode = PickingMode.Position;
            slider.SetEnabled(true);
        });
        popup.Query<Toggle>().ForEach(toggle =>
        {
            toggle.pickingMode = PickingMode.Position;
            toggle.SetEnabled(true);
        });
    }

    private void UnbindCallbacks()
    {
        bgmSlider?.UnregisterValueChangedCallback(HandleBgmChanged);
        sfxSlider?.UnregisterValueChangedCallback(HandleSfxChanged);
        presetGuideToggle?.UnregisterValueChangedCallback(HandlePresetGuideChanged);
        blockNumberToggle?.UnregisterValueChangedCallback(HandleBlockNumberChanged);
        imageSimplificationToggle?.UnregisterValueChangedCallback(HandleImageSimplificationChanged);
        if (restartButton != null) restartButton.clicked -= HandleRestartClicked;
        if (backButton != null) backButton.clicked -= Hide;
        if (stageButton != null) stageButton.clicked -= HandleStageClicked;
    }

    private void HandleBgmChanged(ChangeEvent<float> evt) => ApplyMusicVolume(evt.newValue);

    private void HandleSfxChanged(ChangeEvent<float> evt) => ApplySoundVolume(evt.newValue);

    private void HandlePresetGuideChanged(ChangeEvent<bool> evt)
    {
        PlayerPrefs.SetInt(PresetGuideKey, evt.newValue ? 1 : 0);
        PlayerPrefs.Save();
        ApplyToggleSprite(currentImages, evt.newValue);
    }

    private void HandleBlockNumberChanged(ChangeEvent<bool> evt)
    {
        PlayerPrefs.SetInt(BlockNumberTextKey, evt.newValue ? 1 : 0);
        PlayerPrefs.Save();
        ApplyToggleSprite(currentImages, blockNumberToggle, evt.newValue);
        BlockNumberTextVisibilityChanged?.Invoke(evt.newValue);
    }

    private void HandleImageSimplificationChanged(ChangeEvent<bool> evt)
    {
        PlayerPrefs.SetInt(ImageSimplificationKey, evt.newValue ? 1 : 0);
        PlayerPrefs.Save();
        ApplyToggleSprite(currentImages, imageSimplificationToggle, evt.newValue);
        ImageSimplificationChanged?.Invoke(evt.newValue);
    }

    private void HandleRestartClicked()
    {
        Hide();
        AudioPlayback.StopAllMusic();
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid())
            SceneManager.LoadScene(activeScene.buildIndex);
    }

    private void HandleStageClicked()
    {
        Hide();
        AudioPlayback.StopAllMusic();
        SceneManager.LoadScene("StageScene");
    }

    private void PauseGame()
    {
        if (pausedByPopup) return;
        timeScaleBeforePause = Time.timeScale;
        Time.timeScale = 0f;
        pausedByPopup = true;
    }

    private void ResumeGame()
    {
        if (!pausedByPopup) return;
        Time.timeScale = timeScaleBeforePause;
        pausedByPopup = false;
    }

    private void ApplySprites(SettingUIImageLibrarySO images)
    {
        if (images == null)
        {
            return;
        }

        UISprites.Apply(popup.Q<VisualElement>(className: "setting-panel"), images.panel);
        UISprites.Apply(restartButton, images.restartButton);
        UISprites.Apply(stageButton, images.restartButton);
        UISprites.Apply(backButton, images.backButton);
        ApplyToggleSprite(images, presetGuideToggle?.value ?? false);
        ApplyToggleSprite(images, blockNumberToggle, blockNumberToggle?.value ?? true);
        ApplyToggleSprite(images, imageSimplificationToggle, imageSimplificationToggle?.value ?? false);
        ApplySliderSprites(bgmSlider, images.bgmSliderTrack, images.bgmSliderHandle);
        ApplySliderSprites(sfxSlider, images.sfxSliderTrack, images.sfxSliderHandle);
        UISprites.Apply(presetGuideLabel, images.presetGuideLabel);
        UISprites.Apply(blockNumberLabel, images.presetGuideLabel);
        UISprites.Apply(imageSimplificationLabel, images.presetGuideLabel);
        UISprites.Apply(bgmLabel, images.bgmLabel);
        UISprites.Apply(sfxLabel, images.sfxLabel);
    }

    private void ApplySliderSprites(Slider slider, Sprite track, Sprite handle)
    {
        if (slider == null)
        {
            return;
        }

        UISprites.Apply(slider.Q<VisualElement>(className: "unity-base-slider__tracker"), track);
        UISprites.Apply(slider.Q<VisualElement>(className: "unity-base-slider__dragger"), handle);
    }

    private void ApplyToggleSprite(SettingUIImageLibrarySO images, bool isOn)
    {
        ApplyToggleSprite(images, presetGuideToggle, isOn);
    }

    private static void ApplyToggleSprite(SettingUIImageLibrarySO images, Toggle toggle, bool isOn)
    {
        if (images == null || toggle == null) return;
        var checkmark = toggle.Q<VisualElement>(className: "unity-toggle__checkmark");
        if (checkmark == null) return;
        Sprite sprite = isOn ? images.toggleOn : images.toggleOff;
        if (sprite == null) return;
        // backgroundColor는 USS에서 관리 — UISprites.Apply 사용 시 Clear되므로 직접 설정
        checkmark.style.backgroundImage = new StyleBackground(sprite);
    }

    // ── JSAM AudioManager 볼륨 연동 ──────────────────────────────────────
    // AudioManager가 씬에 없거나(InternalInstance == null) 비재생 상태면 볼륨 조작을 건너뛴다.
    // 매니저가 한 번 잡히면 InternalInstance가 static 캐시되어 이후 부수효과(FindObjectOfType)가 없다.
    // JSAM은 볼륨 set 시 자체적으로 PlayerPrefs에 저장/로드하므로 별도 영속화는 두지 않는다.
    private static bool AudioReady => Application.isPlaying && AudioPlayback.IsReady;

    private float ReadMusicVolume() => AudioPlayback.MusicVolumeOrDefault(DefaultVolume);

    private float ReadSoundVolume() => AudioPlayback.SoundVolumeOrDefault(DefaultVolume);

    private void ApplyMusicVolume(float value)
    {
        value = Mathf.Round(value / VolumeStep) * VolumeStep;

        bgmSlider?.SetValueWithoutNotify(value);

        if (!AudioReady)
            return;

        AudioPlayback.SetMusicVolume(value);
    }

    private void ApplySoundVolume(float value)
    {
        value = Mathf.Round(value / VolumeStep) * VolumeStep;

        sfxSlider?.SetValueWithoutNotify(value);

        if (!AudioReady)
            return;

        AudioPlayback.SetSoundVolume(value);
    }
}
