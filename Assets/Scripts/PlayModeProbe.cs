using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Play-mode probe for the RushHour start screen and the gameplay run.
/// It only *invokes* UI events and reads runtime state - it changes no assets.
/// Compiled by execute_script into a throwaway root-level assembly, so every project type
/// is reached by reflection, and the Input System is driven by reflection too so this file
/// needs no compile-time reference to the Input System assembly.
/// </summary>
public static class PlayModeProbe
{
    internal static readonly List<string> Diag = new List<string>();

    // ------------------------------------------------------------------ helpers

    static Type FindType(string simpleName)
    {
        return FindType(simpleName, null);
    }

    static Type FindType(string simpleName, string namespaceHint)
    {
        Type loose = null;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException rtle) { types = rtle.Types; }
            catch { continue; }

            if (types == null) continue;
            for (int i = 0; i < types.Length; i++)
            {
                var t = types[i];
                if (t == null || t.Name != simpleName) continue;
                if (namespaceHint != null && t.Namespace == namespaceHint) return t;
                if (loose == null) loose = t;
            }
        }
        return loose;
    }

    static string PathOf(Transform t)
    {
        string p = t.name;
        while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
        return p;
    }

    /// <summary>Finds the first live component of the given type inside a loaded scene.</summary>
    static Component FirstOf(Type t)
    {
        if (t == null) { Diag.Add("FirstOf: type was NULL"); return null; }
        try
        {
            var all = Resources.FindObjectsOfTypeAll(t);
            Component firstInactive = null;
            for (int i = 0; i < all.Length; i++)
            {
                var c = all[i] as Component;
                if (c == null) continue;
                if (!c.gameObject.scene.IsValid() || !c.gameObject.scene.isLoaded) continue;
                if (c.gameObject.activeInHierarchy) return c;
                if (firstInactive == null) firstInactive = c;
            }
            Diag.Add("FirstOf(" + t.Name + ") -> none active; inactive candidates=" + (firstInactive != null ? 1 : 0)
                     + " of " + all.Length + " found");
            return firstInactive;
        }
        catch (Exception e)
        {
            Diag.Add("FirstOf(" + t.Name + ") threw: " + e.Message);
            return null;
        }
    }

    static GameObject FindByPath(string fullPath)
    {
        var all = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < all.Length; i++)
        {
            var go = all[i];
            if (!go.scene.IsValid() || !go.scene.isLoaded) continue;
            if (PathOf(go.transform) == fullPath) return go;
        }
        return null;
    }

    static string DiagText()
    {
        return "DIAG: " + (Diag.Count == 0 ? "<none>" : string.Join(" | ", Diag.ToArray()));
    }

    // ------------------------------------------------------------- scene state

    static string LoadedScenes()
    {
        var names = new List<string>();
        for (int i = 0; i < SceneManager.sceneCount; i++) names.Add(SceneManager.GetSceneAt(i).name);
        return string.Join(", ", names.ToArray());
    }

    // ------------------------------------------------------------------ sample

    static string Sample()
    {
        var sb = new StringBuilder();
        sb.Append("t=" + Time.time.ToString("F3"));
        sb.Append("  frame=" + Time.frameCount);
        sb.Append("  timeScale=" + Time.timeScale.ToString("F2"));

        var car = FirstOf(FindType("CarController"));
        if (car == null)
        {
            sb.Append("  | CarController: <none in scene>");
        }
        else
        {
            var t = car.transform;
            sb.Append("  | pod='").Append(PathOf(t)).Append("'");
            sb.Append("  pos=(").Append(t.position.x.ToString("F2")).Append(", ")
              .Append(t.position.y.ToString("F2")).Append(", ")
              .Append(t.position.z.ToString("F2")).Append(")");

            var laneProp = car.GetType().GetProperty("CurrentLane", BindingFlags.Public | BindingFlags.Instance);
            sb.Append("  lane=").Append(laneProp != null ? laneProp.GetValue(car).ToString() : "?");

            var aliveM = car.GetType().GetMethod("IsAlive", BindingFlags.Public | BindingFlags.Instance);
            sb.Append("  alive=").Append(aliveM != null ? aliveM.Invoke(car, null).ToString() : "?");

            var speedF = car.GetType().GetField("forwardSpeed", BindingFlags.Public | BindingFlags.Instance);
            sb.Append("  forwardSpeed=").Append(speedF != null ? ((float)speedF.GetValue(car)).ToString("F2") : "?");
        }

        var bat = FirstOf(FindType("BatterySystem"));
        if (bat == null)
        {
            sb.Append("  | BatterySystem: <none>");
        }
        else
        {
            var f = BindingFlags.Public | BindingFlags.Instance;
            var chargeF = bat.GetType().GetField("currentCharge", f);
            var maxF = bat.GetType().GetField("maxCharge", f);
            var drainF = bat.GetType().GetField("drainPerSecond", f);
            sb.Append("  | charge=").Append(chargeF != null ? ((float)chargeF.GetValue(bat)).ToString("F3") : "?");
            sb.Append(" / ").Append(maxF != null ? ((float)maxF.GetValue(bat)).ToString("F1") : "?");
            sb.Append("  drainPerSecond=").Append(drainF != null ? ((float)drainF.GetValue(bat)).ToString("F2") : "?");
        }

        return sb.ToString();
    }

    // ------------------------------------------------------------ entry points

    /// <summary>Warms the assembly in edit mode and records the baseline gameplay sample.</summary>
    public static string SampleFirst()
    {
        Diag.Clear();
        if (!Application.isPlaying)
            return "EDIT MODE - probe assembly compiled successfully; play-mode sampling deferred.";

        string s = Sample();
        UnityEditor.SessionState.SetString("rh_probe_first", s);
        return "SAMPLE 1  scene=" + SceneManager.GetActiveScene().name + "  loaded=[" + LoadedScenes() + "]\n  " + s
             + "\n" + DiagText();
    }

    public static string SampleAgain()
    {
        Diag.Clear();
        if (!Application.isPlaying) return "NOT IN PLAY MODE";
        string first = UnityEditor.SessionState.GetString("rh_probe_first", "<none recorded>");
        return "SAMPLE 1:  " + first
             + "\nSAMPLE 2  scene=" + SceneManager.GetActiveScene().name + "\n  " + Sample()
             + "\n" + DiagText();
    }

    /// <summary>Invokes the real Button.onClick on HOW TO PLAY, then on BACK.</summary>
    public static string ExerciseOverlay()
    {
        Diag.Clear();
        if (!Application.isPlaying) return "NOT IN PLAY MODE";

        var sb = new StringBuilder();
        sb.AppendLine("scene=" + SceneManager.GetActiveScene().name + "  loaded=[" + LoadedScenes() + "]");

        var scType = FindType("StartScreenController");
        var ctrl = FirstOf(scType);
        if (ctrl == null)
        {
            sb.AppendLine("StartScreenController: <none>   (type resolved=" + (scType != null) + ")");
            sb.AppendLine(DiagText());
            return sb.ToString();
        }

        var panelField = scType.GetField("howToPlayPanel", BindingFlags.Public | BindingFlags.Instance);
        var panel = panelField != null ? panelField.GetValue(ctrl) as GameObject : null;
        sb.AppendLine("overlay ref            : " + (panel == null ? "<NULL>" : PathOf(panel.transform)));
        sb.AppendLine("overlay before         : activeSelf=" + (panel != null && panel.activeSelf)
            + "  activeInHierarchy=" + (panel != null && panel.activeInHierarchy));

        var how = FindByPath("StartScreen/HowToPlayButton");
        if (how == null) sb.AppendLine("HowToPlayButton        : <not found>");
        else
        {
            how.GetComponent<Button>().onClick.Invoke();
            sb.AppendLine("after HOW TO PLAY click: activeSelf=" + panel.activeSelf
                + "  activeInHierarchy=" + panel.activeInHierarchy);
        }

        var back = FindByPath("StartScreen/HowToPlayPanel/BackButton");
        if (back == null) sb.AppendLine("BackButton             : <not found>");
        else
        {
            back.GetComponent<Button>().onClick.Invoke();
            sb.AppendLine("after BACK click       : activeSelf=" + panel.activeSelf
                + "  activeInHierarchy=" + panel.activeInHierarchy);
        }

        sb.AppendLine(DiagText());
        return sb.ToString();
    }

    /// <summary>Invokes the real Button.onClick on PLAY.</summary>
    public static string ClickPlay()
    {
        Diag.Clear();
        if (!Application.isPlaying) return "NOT IN PLAY MODE";

        var before = SceneManager.GetActiveScene().name;
        var btn = FindByPath("StartScreen/PlayButton");
        if (btn == null) return "PLAY button <not found> in " + before + "\n" + DiagText();

        btn.GetComponent<Button>().onClick.Invoke();

        return "before click : activeScene=" + before + "  loaded=[" + LoadedScenes() + "]"
             + "\nimmediately after PLAY click: activeScene=" + SceneManager.GetActiveScene().name
             + "  loaded=[" + LoadedScenes() + "]\n" + DiagText();
    }

    // -------------------------------------------------------------- input probe

    static string InjectKeyboard(params string[] keys)
    {
        try
        {
            var kbType = FindType("Keyboard", "UnityEngine.InputSystem");
            var sysType = FindType("InputSystem", "UnityEngine.InputSystem");
            var stateType = FindType("KeyboardState", "UnityEngine.InputSystem.LowLevel");
            var keyEnum = FindType("Key", "UnityEngine.InputSystem");

            if (kbType == null || sysType == null || stateType == null || keyEnum == null)
                return "InputSystem types not found via reflection (Keyboard=" + (kbType != null)
                     + " InputSystem=" + (sysType != null) + " KeyboardState=" + (stateType != null)
                     + " Key=" + (keyEnum != null) + ")";

            var currentProp = kbType.GetProperty("current", BindingFlags.Public | BindingFlags.Static);
            var kb = currentProp != null ? currentProp.GetValue(null) : null;
            if (kb == null) return "Keyboard.current is null - no keyboard device";

            var keyArray = Array.CreateInstance(keyEnum, keys.Length);
            for (int i = 0; i < keys.Length; i++) keyArray.SetValue(Enum.Parse(keyEnum, keys[i]), i);

            var ctor = stateType.GetConstructor(new[] { keyEnum.MakeArrayType() });
            if (ctor == null) return "KeyboardState(params Key[]) ctor not found";
            var state = ctor.Invoke(new object[] { keyArray });

            var candidates = sysType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "QueueStateEvent" && m.IsGenericMethodDefinition && m.GetParameters().Length >= 2)
                .ToList();
            var generic = candidates.FirstOrDefault(m => m.GetParameters()[1].ParameterType.IsGenericParameter);
            if (generic == null)
                return "InputSystem.QueueStateEvent<T> not found; generic candidates=" + candidates.Count;

            var ps = generic.GetParameters();
            var invokeArgs = new object[ps.Length];
            invokeArgs[0] = kb;
            invokeArgs[1] = state;
            for (int i = 2; i < ps.Length; i++)
                invokeArgs[i] = ps[i].HasDefaultValue ? ps[i].DefaultValue
                             : (ps[i].ParameterType.IsValueType ? Activator.CreateInstance(ps[i].ParameterType) : null);

            generic.MakeGenericMethod(stateType).Invoke(null, invokeArgs);
            return "queued KeyboardState[" + (keys.Length == 0 ? "all released" : string.Join("+", keys)) + "]"
                 + "  via " + ps.Length + "-param overload";

        }
        catch (Exception e)
        {
            return "injection failed: " + (e.InnerException != null ? e.InnerException.Message : e.Message);
        }
    }

    /// <summary>Queues a genuine A-key press so the next frame's CarController.Update sees it.</summary>
    public static string PressKeyA()
    {
        Diag.Clear();
        if (!Application.isPlaying) return "NOT IN PLAY MODE";
        return InjectKeyboard("A") + "   (CarController reads Keyboard.current.aKey.wasPressedThisFrame)";
    }

    /// <summary>
    /// Decisive input test. Revives the pod to a known lane, injects a real device key event,
    /// flushes it so the press is the newest transition, then runs the private
    /// CarController.HandleLaneInput() - the exact method the key binding feeds.
    /// </summary>
    static string InjectAndDrive(string keyName)
    {
        Diag.Clear();
        if (!Application.isPlaying) return "NOT IN PLAY MODE";

        var sb = new StringBuilder();
        var car = FirstOf(FindType("CarController"));
        if (car == null) return "CarController <none>\n" + DiagText();

        var t = car.GetType();
        var laneProp = t.GetProperty("CurrentLane", BindingFlags.Public | BindingFlags.Instance);
        var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        // Known-good starting state: live pod, lane 0, x = 0, hazards cleared.
        var reset = t.GetMethod("ResetCarState", flags);
        if (reset != null) reset.Invoke(car, null);
        sb.AppendLine("after ResetCarState: lane=" + laneProp.GetValue(car)
            + "  x=" + car.transform.position.x.ToString("F2")
            + "  alive=" + t.GetMethod("IsAlive").Invoke(car, null));

        sb.AppendLine(InjectKeyboard(keyName));

        var sysType = FindType("InputSystem", "UnityEngine.InputSystem");
        try
        {
            var upd = sysType.GetMethod("Update", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
            upd.Invoke(null, null);
            sb.AppendLine("InputSystem.Update() flushed");
        }
        catch (Exception e) { sb.AppendLine("InputSystem.Update() failed: " + e.Message); }

        var kbType = FindType("Keyboard", "UnityEngine.InputSystem");
        var kb = kbType.GetProperty("current", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        var keyProp = keyName == "A" ? "aKey" : "dKey";
        var ctrl = kbType.GetProperty(keyProp, BindingFlags.Public | BindingFlags.Instance).GetValue(kb);
        sb.AppendLine("Keyboard.current." + keyProp + ": isPressed="
            + ctrl.GetType().GetProperty("isPressed").GetValue(ctrl)
            + "  wasPressedThisFrame=" + ctrl.GetType().GetProperty("wasPressedThisFrame").GetValue(ctrl));

        int laneBefore = (int)laneProp.GetValue(car);
        var handle = t.GetMethod("HandleLaneInput", flags);
        if (handle == null) { sb.AppendLine("HandleLaneInput <not found>"); return sb.ToString(); }
        handle.Invoke(car, null);
        int laneAfter = (int)laneProp.GetValue(car);

        sb.AppendLine("lane: " + laneBefore + "  ->  " + laneAfter
            + "   (SHIFTED=" + (laneBefore != laneAfter) + ")");
        sb.AppendLine(DiagText());
        return sb.ToString();
    }

    /// <summary>A key / left arrow -> lane -1.</summary>
    public static string PressKeyAImmediate() { return InjectAndDrive("A"); }

    /// <summary>D key / right arrow -> lane +1.</summary>
    public static string PressKeyDImmediate() { return InjectAndDrive("D"); }

    /// <summary>
    /// Revives the pod to lane 0, then queues a key event WITHOUT flushing, so the player
    /// loop's own input update delivers it and wasPressedThisFrame is genuinely true
    /// during the next CarController.Update().
    /// </summary>
    static string ReviveThenQueue(string keyName)
    {
        Diag.Clear();
        if (!Application.isPlaying) return "NOT IN PLAY MODE";

        var sb = new StringBuilder();
        var car = FirstOf(FindType("CarController"));
        if (car == null) return "CarController <none>\n" + DiagText();

        var t = car.GetType();
        var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        var laneProp = t.GetProperty("CurrentLane", flags);
        var reset = t.GetMethod("ResetCarState", flags);
        if (reset != null) reset.Invoke(car, null);

        sb.AppendLine("revived pod: lane=" + laneProp.GetValue(car)
            + "  x=" + car.transform.position.x.ToString("F2")
            + "  alive=" + t.GetMethod("IsAlive").Invoke(car, null));
        sb.AppendLine(InjectKeyboard(keyName) + "   (queued, NOT flushed - player loop delivers it next frame)");
        return sb.ToString();
    }

    public static string ReviveThenPressA() { return ReviveThenQueue("A"); }
    public static string ReviveThenPressD() { return ReviveThenQueue("D"); }



    /// <summary>Releases all keys.</summary>
    public static string ReleaseKeys()
    {
        if (!Application.isPlaying) return "NOT IN PLAY MODE";
        return InjectKeyboard();
    }

    /// <summary>Control: drives the same method HandleLaneInput calls, bypassing the device.</summary>
    public static string SwerveLeftViaScript()
    {
        Diag.Clear();
        if (!Application.isPlaying) return "NOT IN PLAY MODE";
        var car = FirstOf(FindType("CarController"));
        if (car == null) return "CarController <none>\n" + DiagText();
        var m = car.GetType().GetMethod("ShiftLane", BindingFlags.Public | BindingFlags.Instance);
        if (m == null) return "ShiftLane <not found>";
        m.Invoke(car, new object[] { -1 });
        return "invoked CarController.ShiftLane(-1)";
    }

    /// <summary>Reports which project and package types the throwaway assembly can see.</summary>
    public static string Diagnose()
    {
        Diag.Clear();
        var sb = new StringBuilder();
        sb.AppendLine("isPlaying=" + Application.isPlaying + "  activeScene=" + SceneManager.GetActiveScene().name);
        foreach (var n in new[] { "StartScreenController", "StartScreenBackdrop", "CarController",
                                  "BatterySystem", "GameManager", "HudController", "ObstacleSpawner",
                                  "EventSystem", "InputSystemUIInputModule" })
        {
            var t = FindType(n);
            sb.AppendLine("  " + n + " -> " + (t == null ? "<NOT FOUND>" : t.Assembly.GetName().Name));
        }
        sb.AppendLine(DiagText());
        return sb.ToString();
    }

    /// <summary>Reads the lane-input bindings straight out of CarController.</summary>
    public static string AuditLaneBindings()
    {
        Diag.Clear();
        var carType = FindType("CarController");
        if (carType == null) return "CarController type <not found>";
        var m = carType.GetMethod("HandleLaneInput", BindingFlags.NonPublic | BindingFlags.Instance);
        return "CarController.HandleLaneInput present = " + (m != null)
             + "\n  ShiftLane(int) public  = " + (carType.GetMethod("ShiftLane") != null)
             + "\n  CurrentLane property   = " + (carType.GetProperty("CurrentLane") != null)
             + "\n  minLaneIndex field     = " + (carType.GetField("minLaneIndex") != null);
    }

    public static Type Resolve(string simple, string ns) { return FindType(simple, ns); }

    /// <summary>Right / D direction: walks the pod from lane -1 up to lane +1.</summary>
    public static string SwerveRightViaScript()
    {
        Diag.Clear();
        if (!Application.isPlaying) return "NOT IN PLAY MODE";
        var car = FirstOf(FindType("CarController"));
        if (car == null) return "CarController <none>\n" + DiagText();
        var m = car.GetType().GetMethod("ShiftLane", BindingFlags.Public | BindingFlags.Instance);
        if (m == null) return "ShiftLane <not found>";
        m.Invoke(car, new object[] { 1 });
        m.Invoke(car, new object[] { 1 });

        return "invoked CarController.ShiftLane(+1) twice (lane -1 -> +1)";
    }


    /// <summary>
    /// Attaches a runtime component that records, every frame and from inside the real player
    /// loop, exactly what CarController.HandleLaneInput() would see on the keyboard.
    /// </summary>
    public static string StartKeyLogger()
    {
        Diag.Clear();
        if (!Application.isPlaying) return "NOT IN PLAY MODE";

        var go = new GameObject("KeyLoggerProbe");
        go.AddComponent<KeyLoggerBehaviour>();

        return "KeyLoggerProbe attached; " + InjectKeyboard()
             + "\nlog path = " + KeyLoggerBehaviour.LogPath();
    }
}

/// <summary>
/// Runtime observer. Reads the live Keyboard device and the CarController once per frame
/// and appends a line to a text file in the project root, so the evidence survives the
/// throwaway probe assembly being recompiled.
/// </summary>
public class KeyLoggerBehaviour : MonoBehaviour
{
    object _kb, _aKey, _dKey;
    PropertyInfo _isPressed, _wasPressed;
    PropertyInfo _laneProp;
    MethodInfo _aliveM;
    Component _car;
    int _frames;
    static string _path;

    public static string LogPath()
    {
        return System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "probe_log.txt"));
    }

    void Awake()
    {
        var kbType = PlayModeProbe.Resolve("Keyboard", "UnityEngine.InputSystem");
        if (kbType != null)
        {
            var cur = kbType.GetProperty("current", BindingFlags.Public | BindingFlags.Static);
            _kb = cur != null ? cur.GetValue(null) : null;
            if (_kb != null)
            {
                _aKey = kbType.GetProperty("aKey", BindingFlags.Public | BindingFlags.Instance).GetValue(_kb);
                _dKey = kbType.GetProperty("dKey", BindingFlags.Public | BindingFlags.Instance).GetValue(_kb);
                var ctrl = _aKey.GetType();
                _isPressed = ctrl.GetProperty("isPressed");
                _wasPressed = ctrl.GetProperty("wasPressedThisFrame");
            }
        }

        _path = LogPath();
        try
        {
            System.IO.File.WriteAllText(_path,
                "KeyLoggerBehaviour attached at frame " + Time.frameCount
                + "  keyboard=" + (_kb != null) + "\n");
        }
        catch { }
    }

    void Update()
    {
        _frames++;

        if (_car == null)
        {
            var carType = PlayModeProbe.Resolve("CarController", null);
            if (carType != null)
            {
                foreach (var o in Resources.FindObjectsOfTypeAll(carType))
                {
                    var c = o as Component;
                    if (c == null || !c.gameObject.scene.IsValid()) continue;
                    _car = c;
                    _laneProp = carType.GetProperty("CurrentLane", BindingFlags.Public | BindingFlags.Instance);
                    _aliveM = carType.GetMethod("IsAlive", BindingFlags.Public | BindingFlags.Instance);
                    break;
                }
            }
        }

        string a = _aKey != null ? _isPressed.GetValue(_aKey) + "/" + _wasPressed.GetValue(_aKey) : "n/a";
        string d = _dKey != null ? _isPressed.GetValue(_dKey) + "/" + _wasPressed.GetValue(_dKey) : "n/a";
        string lane = "?", alive = "?", x = "?";
        if (_car != null)
        {
            lane = _laneProp.GetValue(_car).ToString();
            alive = _aliveM.Invoke(_car, null).ToString();
            x = _car.transform.position.x.ToString("F2");
        }

        try
        {
            System.IO.File.AppendAllText(_path, string.Format(
                "f={0} t={1:F2}  A(pressed/wasPressedThisFrame)={2}  D={3}  lane={4}  alive={5}  x={6}\n",
                Time.frameCount, Time.time, a, d, lane, alive, x));
        }
        catch { }

        if (_frames > 900) Destroy(this);
    }
}

