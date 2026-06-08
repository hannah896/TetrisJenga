using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class ResultScreenSceneBootstrap
{
    const string TargetScenePath = "Assets/Scenes/Level1.unity";
    static int retryCount;

    static ResultScreenSceneBootstrap()
    {
        EditorApplication.delayCall += StartRetry;
    }

    static void StartRetry()
    {
        retryCount = 0;
        EditorApplication.update -= RetryEnsureScreens;
        EditorApplication.update += RetryEnsureScreens;
    }

    static void RetryEnsureScreens()
    {
        if (Application.isPlaying)
            return;

        if (EnsureLoadedSceneScreens() || retryCount++ > 120)
        {
            if (retryCount > 120)
                EnsureSceneAssetScreens();
            EditorApplication.update -= RetryEnsureScreens;
        }
    }

    static bool EnsureLoadedSceneScreens()
    {
        if (Application.isPlaying)
            return false;

        bool handledAnyScene = false;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(scene.path))
                continue;
            if (scene.path.StartsWith("Temp/") || scene.path.Contains("__Backupscenes"))
                continue;
            if (scene.path != TargetScenePath)
                continue;

            handledAnyScene = true;
            EnsureSceneScreens(scene);
        }
        return handledAnyScene;
    }

    static void EnsureSceneAssetScreens()
    {
        var openedScene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Additive);
        if (!openedScene.IsValid() || !openedScene.isLoaded)
            return;

        EnsureSceneScreens(openedScene);
        EditorSceneManager.SaveScene(openedScene);
        EditorSceneManager.CloseScene(openedScene, true);
    }

    static void EnsureSceneScreens(Scene scene)
    {
        var canvas = FindSceneCanvas(scene);
        if (canvas == null)
            return;

        bool changed = false;
        changed |= EnsureScreen(canvas.transform, "GameOverScreen", "GAME OVER", "RestartButton", "Restart");
        changed |= EnsureScreen(canvas.transform, "ClearScreen", "CLEAR", "NextButton", "Next");
        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }

    static Canvas FindSceneCanvas(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
            {
                if (canvas == null || canvas.name == "HudTextFallbackCanvas")
                    continue;
                if (canvas.name == "Canvas")
                    return canvas;
            }
        }
        return null;
    }

    static bool EnsureScreen(Transform canvasTransform, string screenName, string title, string primaryButtonName, string primaryButtonText)
    {
        bool changed = false;
        var screen = FindDirectChild(canvasTransform, screenName);
        if (screen == null)
        {
            var go = new GameObject(screenName, typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(GameOverScreen));
            go.layer = canvasTransform.gameObject.layer;
            go.transform.SetParent(canvasTransform, false);
            screen = go.transform;
            changed = true;
        }

        var rect = screen.GetComponent<RectTransform>();
        changed |= SetRect(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Vector2.zero);

        var image = screen.GetComponent<UnityEngine.UI.Image>();
        if (image == null)
        {
            image = screen.gameObject.AddComponent<UnityEngine.UI.Image>();
            changed = true;
        }
        var bgColor = new Color(0f, 0f, 0f, 0.72f);
        if (image.color != bgColor)
        {
            image.color = bgColor;
            changed = true;
        }
        if (!image.raycastTarget)
        {
            image.raycastTarget = true;
            changed = true;
        }
        if (screen.GetComponent<GameOverScreen>() == null)
        {
            screen.gameObject.AddComponent<GameOverScreen>();
            changed = true;
        }

        changed |= EnsureLabel(screen, "Title", title, new Vector2(0f, 150f), 72f, Color.white);
        changed |= EnsureLabel(screen, "CurrentScore", "SCORE: 0", new Vector2(0f, 45f), 42f, new Color(1f, 0.85f, 0.1f));
        changed |= EnsureLabel(screen, "TargetScore", "TARGET: 30", new Vector2(0f, -20f), 36f, new Color(0.85f, 0.92f, 1f));
        changed |= EnsureLabel(screen, "RestartHint", "Select an Option", new Vector2(0f, -115f), 26f, new Color(0.75f, 0.75f, 0.75f));
        changed |= EnsureButton(screen, primaryButtonName, primaryButtonText, new Vector2(-150f, -210f));
        changed |= EnsureButton(screen, "MainMenuButton", "Main Menu", new Vector2(150f, -210f));

        if (screen.gameObject.activeSelf)
        {
            screen.gameObject.SetActive(false);
            changed = true;
        }
        return changed;
    }

    static bool EnsureLabel(Transform parent, string objectName, string text, Vector2 position, float fontSize, Color color)
    {
        bool changed = false;
        var labelTransform = FindDirectChild(parent, objectName);
        if (labelTransform == null)
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            labelTransform = go.transform;
            changed = true;
        }

        var rect = labelTransform.GetComponent<RectTransform>();
        changed |= SetRect(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, new Vector2(760f, 100f));

        var label = labelTransform.GetComponent<TextMeshProUGUI>();
        if (label == null)
        {
            label = labelTransform.gameObject.AddComponent<TextMeshProUGUI>();
            changed = true;
        }
        if (label.text != text) { label.text = text; changed = true; }
        if (!Mathf.Approximately(label.fontSize, fontSize)) { label.fontSize = fontSize; changed = true; }
        if (label.fontStyle != FontStyles.Bold) { label.fontStyle = FontStyles.Bold; changed = true; }
        if (label.alignment != TextAlignmentOptions.Center) { label.alignment = TextAlignmentOptions.Center; changed = true; }
        if (label.color != color) { label.color = color; changed = true; }
        if (label.raycastTarget) { label.raycastTarget = false; changed = true; }
        if (label.textWrappingMode != TextWrappingModes.NoWrap) { label.textWrappingMode = TextWrappingModes.NoWrap; changed = true; }
        if (label.overflowMode != TextOverflowModes.Overflow) { label.overflowMode = TextOverflowModes.Overflow; changed = true; }
        return changed;
    }

    static bool EnsureButton(Transform parent, string objectName, string labelText, Vector2 position)
    {
        bool changed = false;
        var buttonTransform = FindDirectChild(parent, objectName);
        if (buttonTransform == null)
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            buttonTransform = go.transform;
            changed = true;
        }

        var rect = buttonTransform.GetComponent<RectTransform>();
        changed |= SetRect(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, new Vector2(240f, 72f));

        var image = buttonTransform.GetComponent<UnityEngine.UI.Image>();
        if (image == null)
        {
            image = buttonTransform.gameObject.AddComponent<UnityEngine.UI.Image>();
            changed = true;
        }
        var bgColor = new Color(1f, 1f, 1f, 0.18f);
        if (image.color != bgColor) { image.color = bgColor; changed = true; }
        if (!image.raycastTarget) { image.raycastTarget = true; changed = true; }

        var button = buttonTransform.GetComponent<UnityEngine.UI.Button>();
        if (button == null)
        {
            button = buttonTransform.gameObject.AddComponent<UnityEngine.UI.Button>();
            changed = true;
        }
        if (button.targetGraphic != image) { button.targetGraphic = image; changed = true; }

        var label = FindDirectChild(buttonTransform, "Label");
        if (label == null)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(buttonTransform, false);
            label = go.transform;
            changed = true;
        }

        var labelRect = label.GetComponent<RectTransform>();
        changed |= SetRect(labelRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        var tmp = label.GetComponent<TextMeshProUGUI>();
        if (tmp == null)
        {
            tmp = label.gameObject.AddComponent<TextMeshProUGUI>();
            changed = true;
        }
        if (tmp.text != labelText) { tmp.text = labelText; changed = true; }
        if (!Mathf.Approximately(tmp.fontSize, 30f)) { tmp.fontSize = 30f; changed = true; }
        if (tmp.fontStyle != FontStyles.Bold) { tmp.fontStyle = FontStyles.Bold; changed = true; }
        if (tmp.alignment != TextAlignmentOptions.Center) { tmp.alignment = TextAlignmentOptions.Center; changed = true; }
        if (tmp.color != Color.white) { tmp.color = Color.white; changed = true; }
        if (tmp.raycastTarget) { tmp.raycastTarget = false; changed = true; }
        if (tmp.textWrappingMode != TextWrappingModes.NoWrap) { tmp.textWrappingMode = TextWrappingModes.NoWrap; changed = true; }
        if (tmp.overflowMode != TextOverflowModes.Overflow) { tmp.overflowMode = TextOverflowModes.Overflow; changed = true; }
        return changed;
    }

    static Transform FindDirectChild(Transform parent, string childName)
    {
        foreach (Transform child in parent)
            if (child != null && child.name == childName)
                return child;
        return null;
    }

    static bool SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        bool changed = false;
        if (rect.anchorMin != anchorMin) { rect.anchorMin = anchorMin; changed = true; }
        if (rect.anchorMax != anchorMax) { rect.anchorMax = anchorMax; changed = true; }
        if (rect.pivot != pivot) { rect.pivot = pivot; changed = true; }
        if (rect.anchoredPosition != anchoredPosition) { rect.anchoredPosition = anchoredPosition; changed = true; }
        if (rect.sizeDelta != sizeDelta) { rect.sizeDelta = sizeDelta; changed = true; }
        return changed;
    }
}
