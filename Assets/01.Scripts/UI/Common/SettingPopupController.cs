using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 모든 씬이 공유하는 설정 팝업 로직. MonoBehaviour가 아니라 각 씬 컨트롤러가
/// 인스턴스로 보유하고 Initialize(root, ...)로 바인딩한다.
/// 설정 팝업 마크업(name="setting-popup" 등)은 각 씬 uxml에 포함되어 있어야 한다.
/// </summary>
public class SettingPopupController
{
    private const string BgmKey = "Settings.BgmVolume";
    private const string SfxKey = "Settings.SfxVolume";
    private const string PresetGuideKey = "Settings.PresetGuide";
    private const float DefaultVolume = 1f;

    private VisualElement popup;
    private Slider bgmSlider;
    private Slider sfxSlider;
    private Toggle presetGuideToggle;
    private Button restartButton;
    private Button backButton;

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
        restartButton = root.Q<Button>("setting-restart-button");
        backButton = root.Q<Button>("setting-back-button");

        ApplySprites(images);
        LoadState();

        bgmSlider?.RegisterValueChangedCallback(evt => SaveVolume(BgmKey, evt.newValue));
        sfxSlider?.RegisterValueChangedCallback(evt => SaveVolume(SfxKey, evt.newValue));
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
        LoadSlider(bgmSlider, BgmKey);
        LoadSlider(sfxSlider, SfxKey);
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

    private void LoadSlider(Slider slider, string prefsKey)
    {
        if (slider == null)
        {
            return;
        }

        slider.SetValueWithoutNotify(Mathf.Clamp01(PlayerPrefs.GetFloat(prefsKey, DefaultVolume)));
    }

    private void SaveVolume(string prefsKey, float value)
    {
        PlayerPrefs.SetFloat(prefsKey, Mathf.Clamp01(value));
        PlayerPrefs.Save();
    }
}
