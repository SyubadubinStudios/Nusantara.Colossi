using System.Numerics;
using Avalonia.Input;
using NusantaraColossi.Core;
using ThreeNet;
using Key = Avalonia.Input.Key;

namespace NusantaraColossi.Game.UI;

/// <summary>Every remappable game action. The order is the order shown in Settings.</summary>
public enum GameAction
{
    MoveForward, MoveBack, MoveLeft, MoveRight,
    Jump, Dodge, Grip, Sprint, Attack, Scan, Hack, Decoy, Interact, Lock, Pause,
}

/// <summary>
/// Merges keyboard, mouse, gamepad and on-screen touch controls into one <see cref="PlayerInput"/>
/// per simulation tick. "Pressed" flags latch until consumed so a tap shorter than a frame is
/// never lost at 60 Hz fixed steps.
/// <para>
/// Bindings are a table: one keyboard key and one gamepad button per <see cref="GameAction"/>,
/// persisted in <see cref="GameSettings.KeyBindings"/> / <see cref="GameSettings.PadBindings"/>.
/// Fixed extras that are never remapped: the other-side Shift/Ctrl/Alt mirrors the bound one, left
/// click also attacks, right-drag / arrow keys / right stick look, left stick moves, and Esc / Start
/// always pause (menus need them).
/// </para>
/// </summary>
public sealed class InputMapper
{
    public const int ActionCount = (int)GameAction.Pause + 1;

    /// <summary>Default keyboard layout.</summary>
    public static readonly Key[] DefaultKeys =
    [
        Key.W, Key.S, Key.A, Key.D,
        Key.Space, Key.LeftCtrl, Key.LeftShift, Key.LeftShift, Key.F, Key.Q, Key.E, Key.R, Key.G, Key.Tab, Key.Escape,
    ];

    /// <summary>Default gamepad layout. Movement is always the left stick (None here).</summary>
    public static readonly GamepadButton[] DefaultPad =
    [
        0, 0, 0, 0,
        GamepadButton.South, GamepadButton.East, GamepadButton.RightTrigger, GamepadButton.LeftStick, GamepadButton.West,
        GamepadButton.LeftBumper, GamepadButton.RightBumper, GamepadButton.LeftTrigger, GamepadButton.DPadUp, GamepadButton.North, GamepadButton.Start,
    ];

    private readonly Key[] keyMap = (Key[])DefaultKeys.Clone();
    private readonly GamepadButton[] padMap = (GamepadButton[])DefaultPad.Clone();
    private readonly string[] keyLabels = new string[ActionCount];
    private readonly string[] padLabels = new string[ActionCount];
    private GameSettings? appliedSettings;
    private int appliedVersion = -1;
    private string appliedLanguage = "";

    private readonly HashSet<Key> keys = [];
    private bool leftMouse, rightMouse;
    private Avalonia.Point? lastPointer;
    private Vector2 mouseLook;
    private bool jumpLatch, dodgeLatch, scanLatch, hackLatch, decoyLatch, lockLatch, pauseLatch, interactLatch, gripTapLatch;
    private bool gripRawPrev, gripLatched;
    private GamepadButton padPrev;

    // Touch controls feed these.
    public Vector2 TouchMove { get; set; }
    public Vector2 TouchLook { get; set; }
    public bool TouchGrip { get; set; }
    public bool TouchAttack { get; set; }

    public GamepadState Pad { get; set; }
    public bool UsingGamepad { get; private set; }

    /// <summary>True while hold-to-toggle grip has latched the grip on.</summary>
    public bool GripLatched => Settings.GripToggle && gripLatched;

    private static GameSettings Settings => GameSettings.Active;

    // ---------------------------------------------------------------- bindings

    /// <summary>Refreshes the binding tables when settings or language changed. Allocation-free when nothing changed.</summary>
    private void EnsureBindings()
    {
        var s = Settings;
        if (ReferenceEquals(s, appliedSettings) && s.BindingsVersion == appliedVersion && string.Equals(Loc.Language, appliedLanguage, StringComparison.Ordinal)) return;
        appliedSettings = s;
        appliedVersion = s.BindingsVersion;
        appliedLanguage = Loc.Language;
        for (var i = 0; i < ActionCount; i++)
        {
            var action = (GameAction)i;
            keyMap[i] = KeyBinding(s, action);
            padMap[i] = PadBinding(s, action);
            keyLabels[i] = KeyName(keyMap[i]);
            padLabels[i] = padMap[i] == 0 ? (Loc.Language == "en" ? "Left stick" : "Stik kiri") : PadName(padMap[i]);
        }
        stabKeyLabel = (Loc.Language == "en" ? "Left click / " : "Klik kiri / ") + keyLabels[(int)GameAction.Attack];
    }

    private string stabKeyLabel = "";

    public static Key KeyBinding(GameSettings s, GameAction action) =>
        s.KeyBindings.TryGetValue(action.ToString(), out var name) && Enum.TryParse<Key>(name, out var key) && key != Key.None ? key : DefaultKeys[(int)action];

    public static GamepadButton PadBinding(GameSettings s, GameAction action) =>
        IsMovement(action) ? 0
        : s.PadBindings.TryGetValue(action.ToString(), out var name) && Enum.TryParse<GamepadButton>(name, out var b) && b != 0 ? b : DefaultPad[(int)action];

    public static bool IsMovement(GameAction action) => action <= GameAction.MoveRight;

    /// <summary>Grip and Sprint may share a key: "hold Shift" grips on a wall and sprints on the ground.</summary>
    private static bool MayShare(GameAction a, GameAction b) =>
        (a, b) is (GameAction.Grip, GameAction.Sprint) or (GameAction.Sprint, GameAction.Grip);

    /// <summary>Binds <paramref name="key"/>; any other action already using it takes this action's old key (swap).</summary>
    public static void Rebind(GameSettings s, GameAction action, Key key)
    {
        var old = KeyBinding(s, action);
        if (old == key) return;
        for (var i = 0; i < ActionCount; i++)
        {
            var other = (GameAction)i;
            if (other == action || MayShare(action, other) || KeyBinding(s, other) != key) continue;
            s.KeyBindings[other.ToString()] = old.ToString();
        }
        s.KeyBindings[action.ToString()] = key.ToString();
        s.BindingsVersion++;
    }

    /// <summary>Binds <paramref name="button"/>; any other action already using it takes this action's old button (swap).</summary>
    public static void Rebind(GameSettings s, GameAction action, GamepadButton button)
    {
        if (IsMovement(action)) return;
        var old = PadBinding(s, action);
        if (old == button) return;
        for (var i = 0; i < ActionCount; i++)
        {
            var other = (GameAction)i;
            if (other == action || IsMovement(other) || PadBinding(s, other) != button) continue;
            s.PadBindings[other.ToString()] = old.ToString();
        }
        s.PadBindings[action.ToString()] = button.ToString();
        s.BindingsVersion++;
    }

    public static void ResetBindings(GameSettings s)
    {
        s.KeyBindings.Clear();
        s.PadBindings.Clear();
        s.BindingsVersion++;
    }

    /// <summary>Keys the capture prompt refuses: Esc cancels capture, and these are reserved for the OS / IME.</summary>
    public static bool IsBindable(Key key) => key is not (Key.None or Key.Escape or Key.LWin or Key.RWin or Key.ImeProcessed or Key.System or Key.DeadCharProcessed);

    public static bool IsBindable(GamepadButton button) => button is not (0 or GamepadButton.Guide);

    /// <summary>Gamepad buttons in capture-scan order (single bits only).</summary>
    public static readonly GamepadButton[] CapturableButtons =
    [
        GamepadButton.South, GamepadButton.East, GamepadButton.West, GamepadButton.North,
        GamepadButton.LeftBumper, GamepadButton.RightBumper, GamepadButton.LeftTrigger, GamepadButton.RightTrigger,
        GamepadButton.LeftStick, GamepadButton.RightStick, GamepadButton.Select, GamepadButton.Start,
        GamepadButton.DPadUp, GamepadButton.DPadDown, GamepadButton.DPadLeft, GamepadButton.DPadRight,
    ];

    public static string KeyName(Key key) => key switch
    {
        Key.LeftShift or Key.RightShift => "Shift",
        Key.LeftCtrl or Key.RightCtrl => "Ctrl",
        Key.LeftAlt or Key.RightAlt => "Alt",
        Key.Space => Loc.Language == "en" ? "Space" : "Spasi",
        Key.Escape => "Esc",
        Key.Return => "Enter",
        Key.Back => "Backspace",
        Key.Up => "↑",
        Key.Down => "↓",
        Key.Left => "←",
        Key.Right => "→",
        Key.CapsLock => "Caps",
        Key.PageUp => "PgUp",
        Key.PageDown => "PgDn",
        >= Key.D0 and <= Key.D9 => ((char)('0' + (key - Key.D0))).ToString(),
        >= Key.NumPad0 and <= Key.NumPad9 => "Num " + (char)('0' + (key - Key.NumPad0)),
        Key.OemComma => ",",
        Key.OemPeriod => ".",
        Key.OemMinus => "-",
        Key.OemPlus => "=",
        Key.OemQuestion => "/",
        Key.OemSemicolon => ";",
        Key.OemQuotes => "'",
        Key.OemOpenBrackets => "[",
        Key.OemCloseBrackets => "]",
        Key.OemPipe or Key.OemBackslash => "\\",
        Key.OemTilde => "`",
        _ => key.ToString(),
    };

    public static string PadName(GamepadButton b) => b switch
    {
        GamepadButton.South => "A",
        GamepadButton.East => "B",
        GamepadButton.West => "X",
        GamepadButton.North => "Y",
        GamepadButton.LeftBumper => "LB",
        GamepadButton.RightBumper => "RB",
        GamepadButton.LeftTrigger => "LT",
        GamepadButton.RightTrigger => "RT",
        GamepadButton.LeftStick => "L3",
        GamepadButton.RightStick => "R3",
        GamepadButton.Select => "View",
        GamepadButton.Start => "Start",
        GamepadButton.DPadUp => "D-pad ↑",
        GamepadButton.DPadDown => "D-pad ↓",
        GamepadButton.DPadLeft => "D-pad ←",
        GamepadButton.DPadRight => "D-pad →",
        _ => b.ToString(),
    };

    /// <summary>Label of the current binding for <paramref name="action"/> on the given device.</summary>
    public string Label(GameAction action, bool gamepad)
    {
        EnsureBindings();
        return gamepad ? padLabels[(int)action] : keyLabels[(int)action];
    }

    /// <summary>One-line control summary built from the live bindings (pause menu, settings).</summary>
    public string Summary(bool gamepad)
    {
        EnsureBindings();
        var en = Loc.Language == "en";
        string L(GameAction a) => gamepad ? padLabels[(int)a] : keyLabels[(int)a];
        var grip = Settings.GripToggle ? (en ? "toggle" : "alih") : (en ? "hold" : "tahan");
        if (gamepad)
            return en
                ? $"Left stick move · Right stick camera · {L(GameAction.Grip)} grip ({grip}) · {L(GameAction.Sprint)} sprint · {L(GameAction.Jump)} jump · {L(GameAction.Dodge)} dodge · Hold {L(GameAction.Attack)}: stab · {L(GameAction.Lock)} lock view · {L(GameAction.Scan)} scan · {L(GameAction.Hack)} hack · {L(GameAction.Decoy)} decoy · {L(GameAction.Interact)} interact · {L(GameAction.Pause)} pause"
                : $"Stik kiri bergerak · Stik kanan kamera · {L(GameAction.Grip)} genggam ({grip}) · {L(GameAction.Sprint)} lari · {L(GameAction.Jump)} lompat · {L(GameAction.Dodge)} menghindar · {L(GameAction.Attack)} tahan: tusuk · {L(GameAction.Lock)} kunci pandangan · {L(GameAction.Scan)} pindai · {L(GameAction.Hack)} retas · {L(GameAction.Decoy)} umpan · {L(GameAction.Interact)} interaksi · {L(GameAction.Pause)} jeda";
        var move = L(GameAction.MoveForward) + L(GameAction.MoveLeft) + L(GameAction.MoveBack) + L(GameAction.MoveRight);
        return en
            ? $"{move} move · {L(GameAction.Grip)}: grip ({grip}) · {L(GameAction.Sprint)}: sprint · {L(GameAction.Jump)} jump · {L(GameAction.Dodge)} dodge · Hold left click / {L(GameAction.Attack)}: stab · Right-drag / arrows: camera · {L(GameAction.Lock)}: lock view · {L(GameAction.Scan)} scan · {L(GameAction.Hack)} hack · {L(GameAction.Decoy)} decoy · {L(GameAction.Interact)} interact · {L(GameAction.Pause)} pause"
            : $"{move} bergerak · {L(GameAction.Grip)}: genggam ({grip}) · {L(GameAction.Sprint)}: lari · {L(GameAction.Jump)} lompat · {L(GameAction.Dodge)} menghindar · Klik kiri / {L(GameAction.Attack)} tahan: tusuk · Klik kanan seret / panah: kamera · {L(GameAction.Lock)}: kunci pandangan · {L(GameAction.Scan)} pindai · {L(GameAction.Hack)} retas · {L(GameAction.Decoy)} umpan · {L(GameAction.Interact)} interaksi · {L(GameAction.Pause)} jeda";
    }

    // ---------------------------------------------------------------- raw events

    public void KeyDown(Key key)
    {
        if (!keys.Add(key)) return; // auto-repeat
        UsingGamepad = false;
        EnsureBindings();
        if (key == Key.Escape) pauseLatch = true; // always, whatever Pause is bound to
        for (var i = 0; i < ActionCount; i++)
        {
            if (!Matches(keyMap[i], key)) continue;
            switch ((GameAction)i)
            {
                case GameAction.Jump: jumpLatch = true; break;
                case GameAction.Dodge: dodgeLatch = true; break;
                case GameAction.Scan: scanLatch = true; break;
                case GameAction.Hack: hackLatch = true; break;
                case GameAction.Decoy: decoyLatch = true; break;
                case GameAction.Interact: interactLatch = true; break;
                case GameAction.Lock: lockLatch = true; break;
                case GameAction.Pause: pauseLatch = true; break;
                case GameAction.Grip: gripTapLatch = true; break;
            }
        }
    }

    public void KeyUp(Key key) => keys.Remove(key);

    // On-screen buttons latch exactly like their keys.
    public void PressJump() => jumpLatch = true;
    public void PressDodge() => dodgeLatch = true;
    public void PressScan() => scanLatch = true;
    public void PressHack() => hackLatch = true;
    public void PressDecoy() => decoyLatch = true;
    public void PressLock() => lockLatch = true;
    public void PressInteract() => interactLatch = true;
    public void PressPause() => pauseLatch = true;
    /// <summary>Touch grip button went down (needed for hold-to-toggle taps shorter than a tick).</summary>
    public void PressGrip() => gripTapLatch = true;

    public void PointerPressed(PointerPointProperties p, Avalonia.Point position)
    {
        UsingGamepad = false;
        if (p.IsLeftButtonPressed) leftMouse = true;
        if (p.IsRightButtonPressed) { rightMouse = true; lastPointer = position; }
    }

    public void PointerReleased(PointerUpdateKind kind)
    {
        if (kind == PointerUpdateKind.LeftButtonReleased) leftMouse = false;
        if (kind == PointerUpdateKind.RightButtonReleased) { rightMouse = false; lastPointer = null; }
    }

    public void PointerMoved(Avalonia.Point position)
    {
        if (!rightMouse) return;
        if (lastPointer is { } last) mouseLook += new Vector2((float)(position.X - last.X), (float)(position.Y - last.Y));
        lastPointer = position;
    }

    /// <summary>Clears held state, e.g. when the window loses focus or a menu opens.</summary>
    public void Reset()
    {
        keys.Clear();
        leftMouse = rightMouse = false;
        lastPointer = null;
        mouseLook = Vector2.Zero;
        jumpLatch = dodgeLatch = scanLatch = hackLatch = decoyLatch = lockLatch = pauseLatch = interactLatch = gripTapLatch = false;
        gripRawPrev = gripLatched = false;
        padPrev = 0;
        TouchMove = TouchLook = Vector2.Zero;
        TouchGrip = TouchAttack = false;
    }

    // ---------------------------------------------------------------- queries

    /// <summary>The bound key, or its other-side twin for modifiers (LeftShift bound → RightShift works too).</summary>
    private static bool Matches(Key bound, Key key) => bound == key || Twin(bound) == key;

    private static Key Twin(Key k) => k switch
    {
        Key.LeftShift => Key.RightShift,
        Key.RightShift => Key.LeftShift,
        Key.LeftCtrl => Key.RightCtrl,
        Key.RightCtrl => Key.LeftCtrl,
        Key.LeftAlt => Key.RightAlt,
        Key.RightAlt => Key.LeftAlt,
        _ => Key.None,
    };

    private bool Down(Key k) => keys.Contains(k) || (Twin(k) is var t && t != Key.None && keys.Contains(t));

    private bool KeyHeld(GameAction a) => Down(keyMap[(int)a]);

    /// <summary>Pad buttons with the triggers decided by our own analog thresholds (grip feels best at .35).</summary>
    private GamepadButton PadButtons()
    {
        var b = Pad.Buttons & ~(GamepadButton.LeftTrigger | GamepadButton.RightTrigger);
        if (Pad.RightTrigger > .35f) b |= GamepadButton.RightTrigger;
        if (Pad.LeftTrigger > .35f) b |= GamepadButton.LeftTrigger;
        return b;
    }

    private bool PadHeld(GamepadButton now, GameAction a) => padMap[(int)a] is var b && b != 0 && (now & b) != 0;

    private bool PadEdge(GamepadButton now, GameAction a) => padMap[(int)a] is var b && b != 0 && (now & b) != 0 && (padPrev & b) == 0;

    public bool ConsumePause()
    {
        EnsureBindings();
        var p = pauseLatch || Pad.WasPressed(GamepadButton.Start) || (padMap[(int)GameAction.Pause] is var b && b != 0 && Pad.WasPressed(b));
        pauseLatch = false;
        return p;
    }

    public bool ConsumeLockToggle()
    {
        EnsureBindings();
        var l = lockLatch || (padMap[(int)GameAction.Lock] is var b && b != 0 && Pad.WasPressed(b));
        lockLatch = false;
        return l;
    }

    /// <summary>Camera look delta in radians for this frame.</summary>
    public Vector2 Look(float dt, float sensitivity, bool invertY)
    {
        var look = mouseLook * .0042f;
        mouseLook = Vector2.Zero;
        var keysLook = new Vector2((Down(Key.Right) ? 1 : 0) - (Down(Key.Left) ? 1 : 0), (Down(Key.Down) ? 1 : 0) - (Down(Key.Up) ? 1 : 0));
        look += keysLook * 2.2f * dt;
        var stick = Pad.RightStick;
        if (stick.LengthSquared() > .0001f) { UsingGamepad = true; look += new Vector2(stick.X, -stick.Y) * 2.8f * dt; }
        look += TouchLook * .006f;
        TouchLook = Vector2.Zero;
        look *= sensitivity;
        // Screen Y down = look down = negative pitch.
        look.Y = invertY ? look.Y : -look.Y;
        // Yaw positive turns left in our convention (forward = sin yaw, cos yaw), so screen right is negative.
        look.X = -look.X;
        return look;
    }

    /// <summary>Runs every simulation tick: no allocations.</summary>
    public PlayerInput Sample(Vector3 cameraForward)
    {
        EnsureBindings();
        var move = new Vector2(
            (KeyHeld(GameAction.MoveRight) ? 1 : 0) - (KeyHeld(GameAction.MoveLeft) ? 1 : 0),
            (KeyHeld(GameAction.MoveForward) ? 1 : 0) - (KeyHeld(GameAction.MoveBack) ? 1 : 0));
        if (Pad.LeftStick.LengthSquared() > .0001f) { UsingGamepad = true; move += Pad.LeftStick; }
        move += TouchMove;
        if (move.LengthSquared() > 1) move = Vector2.Normalize(move);

        var pad = PadButtons();
        if (pad != 0 && pad != padPrev) UsingGamepad = true;

        // Grip: hold, or hold-to-toggle (latch on press, release on the next press) for every device.
        var gripRaw = KeyHeld(GameAction.Grip) || PadHeld(pad, GameAction.Grip) || TouchGrip;
        bool grip;
        if (Settings.GripToggle)
        {
            if (gripTapLatch || (gripRaw && !gripRawPrev)) gripLatched = !gripLatched;
            grip = gripLatched;
        }
        else
        {
            gripLatched = false;
            grip = gripRaw;
        }
        gripRawPrev = gripRaw;

        var input = new PlayerInput
        {
            Move = move,
            CameraForward = cameraForward,
            JumpPressed = jumpLatch || PadEdge(pad, GameAction.Jump),
            DodgePressed = dodgeLatch || PadEdge(pad, GameAction.Dodge),
            Grip = grip,
            // Sprint key, or the grip trigger held while running (a pad habit from earlier builds).
            Sprint = KeyHeld(GameAction.Sprint) || PadHeld(pad, GameAction.Sprint) || (PadHeld(pad, GameAction.Grip) && move.LengthSquared() > .5f),
            Attack = leftMouse || KeyHeld(GameAction.Attack) || PadHeld(pad, GameAction.Attack) || TouchAttack,
            ScanPressed = scanLatch || PadEdge(pad, GameAction.Scan),
            HackPressed = hackLatch || PadEdge(pad, GameAction.Hack),
            DecoyPressed = decoyLatch || PadEdge(pad, GameAction.Decoy),
            InteractPressed = interactLatch || PadEdge(pad, GameAction.Interact),
        };
        padPrev = pad;
        jumpLatch = dodgeLatch = scanLatch = hackLatch = decoyLatch = interactLatch = gripTapLatch = false;
        return input;
    }

    // ---------------------------------------------------------------- HUD labels (cached strings, no per-frame allocation)

    private string Current(GameAction a)
    {
        EnsureBindings();
        return UsingGamepad ? padLabels[(int)a] : keyLabels[(int)a];
    }

    /// <summary>Key labels for HUD hints, matching the active device and the current bindings.</summary>
    public string GripLabel => Current(GameAction.Grip);
    public string StabLabel { get { EnsureBindings(); return UsingGamepad ? padLabels[(int)GameAction.Attack] : stabKeyLabel; } }
    public string ScanLabel => Current(GameAction.Scan);
    public string HackLabel => Current(GameAction.Hack);
    public string DecoyLabel => Current(GameAction.Decoy);
    public string InteractLabel => Current(GameAction.Interact);
    public string JumpLabel => Current(GameAction.Jump);
    public string DodgeLabel => Current(GameAction.Dodge);
    public string LockLabel => Current(GameAction.Lock);
    public string SprintLabel => Current(GameAction.Sprint);
    public string PauseLabel => Current(GameAction.Pause);
}
