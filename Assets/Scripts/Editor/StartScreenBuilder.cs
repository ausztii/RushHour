#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// One-shot editor builder that assembles the RushHour start screen: the same Route 7 stretch and
/// the same GreenLine courier pod as the playable scene, seen from a static camera, with the title
/// card over it.
///
/// It reuses the playable builder's atmosphere, sun, post grade and hero-pod helpers
/// directly rather than restating them, so the menu and the run can never drift apart in colour.
/// The interface itself is composed from RushHourUiTheme, which the HUD draws from as well.
/// Menu: Tools/RushHour/Build Start Screen
/// </summary>
public static class StartScreenBuilder
{
    private const string ScenePath = "Assets/Scenes/StartScreen.unity";
    private const string GameplayScenePath = "Assets/Scenes/RushHour.unity";
    private const string GameplaySceneName = "RushHour";

    private const string StreakColorHex = "FFB857";

    public static string Execute()
    {
        // --- 0. Safety: don't silently discard unsaved scene edits ---
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return "CANCELLED: unsaved scene changes were not saved.";
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // --- 1. The same atmosphere and sun as the run ---
        RushHourSceneBuilder.ConfigureAtmosphere();
        RushHourSceneBuilder.BuildSun();

        // --- 2. The hero stretch, built from the playable palette ---
        GameObject[] laneDashes = RushHourSceneBuilder.BuildHeroStreet();
        Transform heroPod = RushHourSceneBuilder.BuildHeroPod(new Vector3(1.15f, 0f, 126f), 0f);

        // --- 3. Static camera + the shared grade ---
        BuildCamera();
        RushHourSceneBuilder.BuildPostProcessing();

        // --- 4. Life in the backdrop, then the title card over it ---
        BuildBackdrop(laneDashes, heroPod);
        BuildMenu();

        // --- 5. Save, and make this the scene the player boots into ---
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        RegisterScenes();

        return string.Format(
            "Start screen built at {0}. Route 7 seen from a static camera at (0, 3.35, 108): the same " +
            "GreenLine pod parked on the resurfaced stretch, with the shared atmosphere, sun and post grade. " +
            "Title card over it in the shared ink-and-cream interface style: a banded ink scrim down the " +
            "left of the view, the eyebrow and RUSHHOUR wordmark in bone and amber with speed-line streaks, " +
            "tagline, mission hook, a torn-cream selection stroke on PLAY, a boxed HOW TO PLAY, and the " +
            "key-prompt bar across the foot of the screen carrying the SDG 11 credit. HOW TO PLAY opens a " +
            "full-width briefing of label and body rows with its own boxed BACK. " +
            "{1} lane dashes scroll past at 14 m/s and the pod idles on its suspension. " +
            "Build order set to {0} first, {2} second.",
            ScenePath, laneDashes.Length, GameplayScenePath);
    }

    [MenuItem("Tools/RushHour/Build Start Screen", false, 11)]
    public static void BuildFromMenu()
    {
        string result = Execute();
        Debug.Log("<color=green>RushHour start screen builder:</color> " + result);
    }

    // ================================================================== camera

    private static Camera BuildCamera()
    {
        GameObject camGo = new GameObject("Camera");
        camGo.transform.position = new Vector3(0f, 3.35f, 108f);
        camGo.transform.rotation = Quaternion.Euler(8.5f, 0f, 0f);
        try { camGo.tag = "MainCamera"; } catch { }

        Camera cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 52f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 1200f;
        cam.clearFlags = CameraClearFlags.Skybox;

        // A fresh URP camera defaults this to false, so the menu would render ungraded and read as
        // a different time of day from the run it is advertising.
        cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;

        camGo.AddComponent<AudioListener>();
        return cam;
    }

    // ================================================================ backdrop

    private static void BuildBackdrop(GameObject[] laneDashes, Transform heroPod)
    {
        GameObject go = new GameObject("Backdrop");
        StartScreenBackdrop backdrop = go.AddComponent<StartScreenBackdrop>();

        SerializedObject so = new SerializedObject(backdrop);

        SerializedProperty dashes = so.FindProperty("laneDashes");
        dashes.arraySize = laneDashes.Length;
        for (int i = 0; i < laneDashes.Length; i++)
        {
            dashes.GetArrayElementAtIndex(i).objectReferenceValue = laneDashes[i].transform;
        }

        so.FindProperty("heroPod").objectReferenceValue = heroPod;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ==================================================================== menu

    private static StartScreenController BuildMenu()
    {
        Font font = RushHourSceneBuilder.GetFont();

        GameObject canvasGo = new GameObject("StartScreen",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // This project runs the new Input System exclusively, so the UI needs its module or the
        // buttons will hover but never click.
        GameObject esGo = new GameObject("EventSystem", typeof(EventSystem));
        System.Type uiModule = System.Type.GetType(
            "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (uiModule != null) esGo.AddComponent(uiModule);
        else esGo.AddComponent<StandaloneInputModule>();

        StartScreenController controller = canvasGo.AddComponent<StartScreenController>();

        // Streaks first, then the scrim over them, then the words - so the field reads as solid
        // behind the type and thins out into the road on the right.
        BuildSpeedLines(canvasGo.transform);
        BuildScrim(canvasGo.transform);

        Vector2 left = new Vector2(0f, 0.5f);

        RushHourUiTheme.Type(canvasGo.transform, "Eyebrow",
            RushHourUiTheme.Spread("GREENLINE LOGISTICS \u00b7 ROUTE 7 \u00b7 MEDICAL RUN"),
            font, 20, TextAnchor.MiddleLeft, RushHourUiTheme.Amber, FontStyle.Bold,
            left, left, left, new Vector2(154f, 240f), new Vector2(1200f, 30f));

        RushHourUiTheme.Type(canvasGo.transform, "Title", "RUSHHOUR",
            font, 132, TextAnchor.MiddleLeft, RushHourUiTheme.Bone, FontStyle.Bold,
            left, left, left, new Vector2(150f, 104f), new Vector2(1500f, 190f));

        RushHourUiTheme.Type(canvasGo.transform, "Tagline", "Every second counts. Every pothole costs.",
            font, 30, TextAnchor.MiddleLeft, RushHourUiTheme.Cream, FontStyle.Bold,
            left, left, left, new Vector2(156f, 2f), new Vector2(1300f, 40f));

        RushHourUiTheme.Type(canvasGo.transform, "Hook",
            "Deliver the vaccines to the Greenfield Clinic before the city gets in your way.",
            font, 24, TextAnchor.MiddleLeft, RushHourUiTheme.Muted, FontStyle.Normal,
            left, left, left, new Vector2(156f, -48f), new Vector2(1500f, 34f));

        RushHourUiTheme.Hairline(canvasGo.transform, "HookRule",
            left, left, left, new Vector2(154f, -86f), new Vector2(760f, 2f));

        // PLAY carries the torn cream stroke with ink type on it; HOW TO PLAY is the quiet row
        // beneath, on a band that is barely there until the pointer finds it.
        Button play = RushHourUiTheme.MenuItem(canvasGo.transform, "PlayButton",
            RushHourUiTheme.Spread("PLAY"), font, 32,
            left, left, left, new Vector2(154f, -142f), new Vector2(340f, 76f),
            true, 32f, 811201, controller.PlayGame);

        RushHourUiTheme.MenuItem(canvasGo.transform, "HowToPlayButton",
            RushHourUiTheme.Spread("HOW TO PLAY"), font, 26,
            left, left, left, new Vector2(154f, -236f), new Vector2(340f, 76f),
            false, 32f, 811202, controller.ShowHowToPlay);

        // The foot of the screen carries the prompt and the credit, the way every reference layout
        // closes itself off.
        RushHourUiTheme.HintBar(canvasGo.transform, font, "LMB", "SELECT",
            "INSPIRED BY SDG 11  \u00b7  SUSTAINABLE CITIES AND COMMUNITIES");

        // Built last so it draws over the title card, and off by default so PLAY is the only
        // thing on screen when the menu opens.
        GameObject overlay = BuildHowToPlayOverlay(canvasGo.transform, controller, font);
        controller.howToPlayPanel = overlay;

        Selection.activeGameObject = play.gameObject;
        return controller;
    }

    /// <summary>
    /// The ink field the type stands on. A single flat panel would hide the stretch the menu is
    /// advertising, so it is built as five stacked bands that fall away to nothing across the
    /// right half of the view: solid behind the words, open over the road.
    /// </summary>
    private static void BuildScrim(Transform parent)
    {
        GameObject root = new GameObject("Scrim", typeof(RectTransform));
        RectTransform rt = root.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;

        const int bands = 5;
        const float coverage = 0.66f;

        for (int i = 0; i < bands; i++)
        {
            float t = i / (float)(bands - 1);
            float left = coverage * (i / (float)bands);
            float right = coverage * ((i + 1) / (float)bands);

            RushHourUiTheme.Box(rt, "ScrimBand_" + i,
                RushHourUiTheme.WithAlpha(RushHourUiTheme.Ink, Mathf.Lerp(0.58f, 0.06f, t)),
                new Vector2(left, 0f), new Vector2(right, 1f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
        }
    }

    /// <summary>
    /// Faint horizontal streaks that taper away from the wordmark in four segments, reading as the
    /// speed the run is built on. Seeded so a rebuild is byte-identical.
    /// </summary>
    private static void BuildSpeedLines(Transform parent)
    {
        Random.InitState(20240519);
        Color streak = HexColor(StreakColorHex);

        GameObject root = new GameObject("SpeedLines", typeof(RectTransform));
        RectTransform rootRt = root.GetComponent<RectTransform>();
        rootRt.SetParent(parent, false);
        rootRt.anchorMin = new Vector2(0f, 0.5f);
        rootRt.anchorMax = new Vector2(0f, 0.5f);
        rootRt.pivot = new Vector2(0f, 0.5f);
        rootRt.anchoredPosition = Vector2.zero;
        rootRt.sizeDelta = Vector2.zero;

        const int streakCount = 13;
        const int segments = 4;

        for (int i = 0; i < streakCount; i++)
        {
            bool trailingLeft = i % 2 == 0;
            float head = trailingLeft ? 760f : 830f;
            float direction = trailingLeft ? -1f : 1f;
            float thickness = Random.Range(2f, 7f);
            float length = Random.Range(300f, 980f);
            float y = Random.Range(0f, 200f);

            float cursor = head;

            for (int s = 0; s < segments; s++)
            {
                float width = length * 0.25f * (1f - (s * 0.22f));
                float alphas = Mathf.Lerp(0.26f, 0.04f, s / (float)(segments - 1));

                RushHourSceneBuilder.CreateUIImage(rootRt, "Streak_" + i + "_" + s,
                    new Color(streak.r, streak.g, streak.b, alphas),
                    new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(cursor + (direction * width * 0.5f), y), new Vector2(width, thickness),
                    false);

                cursor += direction * width;
            }
        }
    }

    /// <summary>
    /// The briefing, set as a tab of cream with ink type on it, a rule running off its shoulder,
    /// and one hairline-separated row per rule of the run.
    /// </summary>
    private static GameObject BuildHowToPlayOverlay(Transform parent, StartScreenController controller, Font font)
    {
        // Opaque enough to read against, and a raycast target so clicks never reach the
        // title-card buttons underneath while it is open.
        GameObject panel = RushHourUiTheme.Box(parent, "HowToPlayPanel",
            RushHourUiTheme.WithAlpha(RushHourUiTheme.Ink, 0.97f),
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, true);

        Vector2 topLeft = new Vector2(0f, 1f);

        GameObject tab = RushHourUiTheme.Box(panel.transform, "HeadingBlock", RushHourUiTheme.Bone,
            topLeft, topLeft, topLeft, new Vector2(300f, -116f), new Vector2(430f, 74f));

        RushHourUiTheme.Type(tab.transform, "HeadingLabel", RushHourUiTheme.Spread("HOW TO PLAY"),
            font, 28, TextAnchor.MiddleCenter, RushHourUiTheme.Ink, FontStyle.Bold,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(-16f, -8f), false);

        RushHourUiTheme.Hairline(panel.transform, "HeadingRule", topLeft, topLeft, topLeft,
            new Vector2(752f, -153f), new Vector2(868f, 2f));

        RushHourUiTheme.Type(panel.transform, "Subheading",
            "Route 7 drives itself. You only choose the lane - and the charge you spend choosing it.",
            font, 24, TextAnchor.MiddleLeft, RushHourUiTheme.Muted, FontStyle.Normal,
            topLeft, topLeft, topLeft, new Vector2(300f, -216f), new Vector2(1500f, 34f));

        string[] labels =
        {
            "SWERVE", "CHARGE", "COLLIDE", "POTHOLE", "SHIELD", "RECHARGE", "DELIVER",
        };

        string[] bodies =
        {
            "A / D or the left and right arrows change lane. Every swerve costs 1% of the pack.",
            "The pack drains as you drive, and faster when you swerve. Empty ends the run.",
            "Barricades, debris and congestion end the run on contact.",
            "Costs a chunk of charge instead of ending the run - and no shield will save you.",
            "Absorbs one crash. Blue pickups grant it.",
            "Green pickups restore 30% of the pack.",
            "Reach the Greenfield Community Clinic, 500 m along Route 7.",
        };

        for (int i = 0; i < labels.Length; i++)
        {
            float y = -300f - (i * 48f);

            RushHourUiTheme.Hairline(panel.transform, "RowRule_" + i, topLeft, topLeft, topLeft,
                new Vector2(300f, y + 24f), new Vector2(1320f, 2f), RushHourUiTheme.RuleFaintInk);

            RushHourUiTheme.Type(panel.transform, "RowLabel_" + i, RushHourUiTheme.Spread(labels[i]),
                font, 22, TextAnchor.MiddleLeft, RushHourUiTheme.Amber, FontStyle.Bold,
                topLeft, topLeft, topLeft, new Vector2(300f, y), new Vector2(260f, 34f), false);

            RushHourUiTheme.Type(panel.transform, "RowBody_" + i, bodies[i],
                font, 24, TextAnchor.MiddleLeft, RushHourUiTheme.Cream, FontStyle.Normal,
                topLeft, topLeft, topLeft, new Vector2(600f, y), new Vector2(1020f, 34f), false);
        }

        RushHourUiTheme.FramedButton(panel.transform, "BackButton", "BACK", font, 26,
            Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(300f, 66f), new Vector2(240f, 68f),
            RushHourUiTheme.Bone, RushHourUiTheme.Bone, controller.HideHowToPlay);

        panel.SetActive(false);
        return panel;
    }

    // ================================================================= helpers

    private static Color HexColor(string hex)
    {
        Color color;
        if (ColorUtility.TryParseHtmlString("#" + hex, out color)) return color;
        return new Color(1f, 0.72f, 0.34f);
    }

    /// <summary>
    /// Puts the menu at the front of the build list and the run directly after it, leaving every
    /// other entry where the project had it.
    /// </summary>
    private static void RegisterScenes()
    {
        List<EditorBuildSettingsScene> existing =
            new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

        List<EditorBuildSettingsScene> ordered = new List<EditorBuildSettingsScene>();
        ordered.Add(new EditorBuildSettingsScene(ScenePath, true));

        bool hasGameplay = false;
        for (int i = 0; i < existing.Count; i++)
        {
            EditorBuildSettingsScene entry = existing[i];
            if (entry.path == ScenePath) continue;

            if (entry.path == GameplayScenePath)
            {
                ordered.Add(new EditorBuildSettingsScene(GameplayScenePath, true));
                hasGameplay = true;
                continue;
            }

            ordered.Add(entry);
        }

        if (!hasGameplay) ordered.Add(new EditorBuildSettingsScene(GameplayScenePath, true));

        EditorBuildSettings.scenes = ordered.ToArray();
    }
}
#endif
