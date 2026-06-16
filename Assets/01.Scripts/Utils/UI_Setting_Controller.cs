using System;
using JSAM;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 모든 씬이 공유하는 설정 팝업 로직. MonoBehaviour가 아니라 각 씬 컨트롤러가
/// 인스턴스로 보유하고 Initialize(root, ...)로 바인딩한다.
/// 설정 팝업 마크업(name="setting-popup" 등)은 각 씬 uxml에 포함되어 있어야 한다.
/// </summary>
public class UI_Setting_Controller
{
    private const string PresetGuideKey = "Settings.PresetGuide";
    private const float DefaultVolume = 1f;
    
    private VisualElement popup;
    private Slider bgmSlider;
    private Slider sfxSlider;
    private Toggle presetGuideToggle;
    private Label presetGuideLabel;
    private Label bgmLabel;
    private Label sfxLabel;
    private Button restartButton;
    private Button backButton;
    
    private const float VolumeStep = 0.075f;



    public bool IsOpen => popup != null && !popup.ClassListContains("hidden");

    /// <param name="root">UIDocument rootVisualElement</param>
    /// <param name="images">설정 스프라이트 SO (없으면 USS 기본 색 사용)</param>
    /// <param name="onRestart">Restart 버튼 동작 (씬마다 다름, null 허용)</param>
    public void Initialize(VisualElement root, SettingUIImageLibrarySO images, Action onRestart)
    {
        popup = root.Q<VisualElement>("setting-popup");
        if (popup == null)
        {
            Debug.LogError("SettingPopupController: 'setting-popup' 요소를 찾을 수 없습니다. 씬 uxml에 설정 팝업 마크업이 있는지 확인하세요.");
            return;
        }

        bgmSlider = root.Q<Slider>("bgm-slider");
        sfxSlider = root.Q<Slider>("sfx-slider");
        presetGuideToggle = root.Q<Toggle>("preset-guide-toggle");
        presetGuideLabel = root.Q<Label>("preset-guide-label");
        bgmLabel = root.Q<Label>("bgm-label");
        sfxLabel = root.Q<Label>("sfx-label");
        restartButton = root.Q<Button>("setting-restart-button");
        backButton = root.Q<Button>("setting-back-button");

        ApplySprites(images);
        LoadState();

        bgmSlider?.RegisterValueChangedCallback(evt => ApplyMusicVolume(evt.newValue));
        sfxSlider?.RegisterValueChangedCallback(evt => ApplySoundVolume(evt.newValue));
        presetGuideToggle?.RegisterValueChangedCallback(evt =>
        {
            PlayerPrefs.SetInt(PresetGuideKey, evt.newValue ? 1 : 0);
            PlayerPrefs.Save();
            ApplyToggleSprite(images, evt.newValue);
        });

        if (restartButton != null)
        {
            restartButton.clicked += () =>
            {
                Hide();
                onRestart?.Invoke();
            };
        }

        if (backButton != null)
        {
            backButton.clicked += Hide;
        }

        Hide();
    }

    public void Show()
    {
        popup?.RemoveFromClassList("hidden");
    }

    public void Hide()
    {
        popup?.AddToClassList("hidden");
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
    }

    private void ApplySprites(SettingUIImageLibrarySO images)
    {
        if (images == null)
        {
            return;
        }

        UISprites.Apply(popup.Q<VisualElement>(className: "setting-panel"), images.panel);
        UISprites.Apply(restartButton, images.restartButton);
        UISprites.Apply(backButton, images.backButton);
        ApplyToggleSprite(images, presetGuideToggle?.value ?? false);
        ApplySliderSprites(bgmSlider, images.bgmSliderTrack, images.bgmSliderHandle);
        ApplySliderSprites(sfxSlider, images.sfxSliderTrack, images.sfxSliderHandle);
        UISprites.Apply(presetGuideLabel, images.presetGuideLabel);
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
        if (images == null || presetGuideToggle == null)
        {
            return;
        }

        Sprite sprite = isOn ? images.toggleOn : images.toggleOff;
        UISprites.Apply(presetGuideToggle.Q<VisualElement>(className: "unity-toggle__checkmark"), sprite);
    }

    // ── JSAM AudioManager 볼륨 연동 ──────────────────────────────────────
    // AudioManager가 씬에 없거나(InternalInstance == null) 비재생 상태면 볼륨 조작을 건너뛴다.
    // 매니저가 한 번 잡히면 InternalInstance가 static 캐시되어 이후 부수효과(FindObjectOfType)가 없다.
    // JSAM은 볼륨 set 시 자체적으로 PlayerPrefs에 저장/로드하므로 별도 영속화는 두지 않는다.
    private static bool AudioReady => Application.isPlaying && AudioManager.InternalInstance != null;

    private float ReadMusicVolume() => AudioReady ? AudioManager.MusicVolume : DefaultVolume;

    private float ReadSoundVolume() => AudioReady ? AudioManager.SoundVolume : DefaultVolume;

    private void ApplyMusicVolume(float value)
    {
        value = Mathf.Round(value / VolumeStep) * VolumeStep;

        bgmSlider?.SetValueWithoutNotify(value);

        if (!AudioReady)
            return;

        AudioManager.MusicVolume = value;
    }

    private void ApplySoundVolume(float value)
    {
        value = Mathf.Round(value / VolumeStep) * VolumeStep;

        sfxSlider?.SetValueWithoutNotify(value);

        if (!AudioReady)
            return;

        AudioManager.SoundVolume = value;
    }
}
