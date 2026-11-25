using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using ThreeNet;
using Key = Avalonia.Input.Key;

using Palette = NusantaraColossi.Game.UI.Theme;

namespace NusantaraColossi.Game.UI.Screens;

/// <summary>
/// Settings apply immediately and persist on leave. Graphics quality applies on restart.
/// The Controls table rebinds keyboard keys and gamepad buttons: pick a binding, press the new
/// key / button (Esc cancels, 8 s auto-cancel), and a conflicting action swaps to the old binding.
/// </summary>
public sealed class SettingsScreen : Screen
{
    private const double CaptureTimeout = 8;

    private readonly Screen? returnTo;
    private readonly Dictionary<(GameAction Action, bool Pad), (Button Button, TextBlock Text)> bindingButtons = [];
    private TextBlock status = new();
    private (GameAction Action, bool Pad)? capture;
    private double captureAge;
    private bool captureArmed;

    public SettingsScreen(Shell shell, Screen? returnTo = null) : base(shell)
    {
        this.returnTo = returnTo;
        AddHandler(KeyDownEvent, OnCaptureKey, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerPressedEvent, OnCapturePointer, RoutingStrategies.Tunnel, handledEventsToo: true);
        Build();
    }

    /// <summary>While a binding is being captured, Shell routes raw keys here and skips menu gamepad navigation.</summary>
    public override bool CapturesGameInput => capture is not null;

    private void Build()
    {
        var s = Ctx.Settings;
        bindingButtons.Clear();

        // ------------------------------------------------ general + accessibility
        var form = new Grid { ColumnDefinitions = new ColumnDefinitions("250,*"), MaxWidth = 760, HorizontalAlignment = HorizontalAlignment.Left };
        var row = 0;
        void Row(string label, Control control)
        {
            form.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var l = Palette.Text(label, 15, Palette.Kapur);
            l.VerticalAlignment = VerticalAlignment.Center;
            l.Margin = new Thickness(0, 8, 16, 8);
            Grid.SetRow(l, row);
            control.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetRow(control, row);
            Grid.SetColumn(control, 1);
            form.Children.Add(l);
            form.Children.Add(control);
            row++;
        }
        void Heading(string text)
        {
            form.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var h = Palette.Mono(text.ToUpperInvariant(), 11, Palette.Nadi);
            h.Margin = new Thickness(0, 22, 0, 4);
            Grid.SetRow(h, row);
            Grid.SetColumnSpan(h, 2);
            form.Children.Add(h);
            row++;
        }

        Row(Loc.T("settings.language"), Choice(["Bahasa Indonesia", "English"], s.Language == "en" ? 1 : 0, i =>
        {
            s.Language = Loc.Language = i == 1 ? "en" : "id";
            Ctx.SaveSettings();
            Build();
        }));
        Row(Loc.T("settings.quality"), Choice([Loc.T("settings.quality.0"), Loc.T("settings.quality.1"), Loc.T("settings.quality.2")], s.Quality, i => { s.Quality = i; s.QualityChosen = true; }));
        Row(Loc.T("settings.volume"), Slider(s.Volume, 0, 1, v => { s.Volume = v; Ctx.Audio.ApplySettings(); }));
        Row(Loc.T("settings.music"), Slider(s.MusicVolume, 0, 1, v => s.MusicVolume = v));
        Row(Loc.T("settings.sensitivity"), Slider(s.Sensitivity, .3f, 2.5f, v => s.Sensitivity = v));
        Row(Loc.T("settings.shake"), Slider(s.CameraShake, 0, 1.5f, v => s.CameraShake = v));
        Row(Loc.T("settings.invert"), Toggle(s.InvertY, v => s.InvertY = v));
        Row(Loc.T("settings.subtitles"), Toggle(s.Subtitles, v => s.Subtitles = v));
        Row(Loc.Language == "en" ? "Dynamic resolution" : "Resolusi dinamis", Toggle(s.DynamicResolution, v => s.DynamicResolution = v));
        Row(Loc.T("settings.fps"), Choice(["30", "60", "90", "120", "144"], Array.IndexOf([30, 60, 90, 120, 144], s.FpsCap) is var i and >= 0 ? i : 1, i => s.FpsCap = new[] { 30, 60, 90, 120, 144 }[i]));

        Heading(Loc.T("settings.accessibility"));
        Row(Loc.T("settings.gripToggle"), Toggle(s.GripToggle, v => s.GripToggle = v));
        Row(Loc.T("settings.highContrast"), Toggle(s.HighContrastHud, v => { s.HighContrastHud = v; Palette.ApplyAccessibility(s); }));
        float[] scales = [.85f, 1f, 1.15f, 1.3f];
        var scaleIndex = 0;
        for (var k = 1; k < scales.Length; k++) if (Math.Abs(scales[k] - s.UiScale) < Math.Abs(scales[scaleIndex] - s.UiScale)) scaleIndex = k;
        Row(Loc.T("settings.uiScale"), Choice(["85%", "100%", "115%", "130%"], scaleIndex, k => { s.UiScale = scales[k]; Palette.ApplyAccessibility(s); }));

        var left = new StackPanel { Margin = new Thickness(0, 0, 56, 0) };
        left.Children.Add(form);
        left.Children.Add(Palette.Mono(Loc.Language == "en" ? "Graphics quality applies after restarting the game." : "Kualitas grafis berlaku setelah game dimulai ulang.", 11, Palette.KapurMuted).Also(t => { t.Margin = new Thickness(0, 8, 0, 0); t.HorizontalAlignment = HorizontalAlignment.Left; }));

        // ------------------------------------------------ controls
        var right = new StackPanel { MaxWidth = 600, HorizontalAlignment = HorizontalAlignment.Left };
        right.Children.Add(Palette.Mono(Loc.T("settings.controls").ToUpperInvariant(), 11, Palette.Nadi).Also(t => t.Margin = new Thickness(0, 8, 0, 8)));
        var table = new Grid { ColumnDefinitions = new ColumnDefinitions("220,170,170"), HorizontalAlignment = HorizontalAlignment.Left };
        table.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        AddCell(table, Palette.Eyebrow(Loc.T("settings.controls.action")), 0, 0);
        AddCell(table, Palette.Eyebrow(Loc.T("settings.controls.keyboard")), 0, 1);
        AddCell(table, Palette.Eyebrow(Loc.T("settings.controls.gamepad")), 0, 2);
        for (var a = 0; a < InputMapper.ActionCount; a++)
        {
            var action = (GameAction)a;
            var r = a + 1;
            table.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            AddCell(table, Palette.Text(Loc.T("action." + action), 14, Palette.Kapur).Also(t => { t.VerticalAlignment = VerticalAlignment.Center; t.Margin = new Thickness(0, 0, 12, 0); }), r, 0);
            AddCell(table, BindingButton(action, false), r, 1);
            AddCell(table, InputMapper.IsMovement(action)
                ? Palette.Mono(Loc.Language == "en" ? "Left stick" : "Stik kiri", 12.5, Palette.KapurMuted).Also(t => { t.VerticalAlignment = VerticalAlignment.Center; t.Margin = new Thickness(12, 0, 0, 0); })
                : BindingButton(action, true), r, 2);
        }
        right.Children.Add(table);
        status = Palette.Mono("", 12, Palette.PradaBright).Also(t => { t.Margin = new Thickness(0, 10, 0, 0); t.MinHeight = 18; t.HorizontalAlignment = HorizontalAlignment.Left; });
        right.Children.Add(status);
        right.Children.Add(Palette.Text(Loc.T("settings.controls.hint"), 13, Palette.KapurMuted).Also(t => { t.MaxWidth = 560; t.HorizontalAlignment = HorizontalAlignment.Left; t.Margin = new Thickness(0, 6, 0, 0); t.LineHeight = 20; }));
        right.Children.Add(Palette.Secondary(Loc.T("settings.controls.reset"), () =>
        {
            CancelCapture();
            InputMapper.ResetBindings(Ctx.Settings);
            Ctx.SaveSettings();
            RefreshBindings();
            status.Text = Loc.T("settings.controls.reset") + " ✓";
        }).Also(b => { b.Margin = new Thickness(0, 14, 0, 0); b.HorizontalAlignment = HorizontalAlignment.Left; }));

        var columns = new WrapPanel { Orientation = Orientation.Horizontal };
        columns.Children.Add(left);
        columns.Children.Add(right);

        var root = new StackPanel { Margin = new Thickness(Ctx.IsMobile ? 24 : 56, 40, 24, 32) };
        root.Children.Add(Palette.Eyebrow("GARUDA//CONFIG", Palette.Nadi));
        root.Children.Add(Palette.Title(Loc.T("settings.title"), 38));
        root.Children.Add(Palette.Rule(Palette.Prada, 80).Also(r => { r.Margin = new Thickness(0, 12, 0, 18); r.HorizontalAlignment = HorizontalAlignment.Left; }));
        root.Children.Add(columns);
        root.Children.Add(Palette.Secondary("← " + Loc.T("menu.back"), () => Back()).Also(b => { b.Margin = new Thickness(0, 28, 0, 0); b.HorizontalAlignment = HorizontalAlignment.Left; }));
        Content = new Border { Background = Palette.Scrim, Child = new ScrollViewer { Content = root } };
        RefreshBindings();
    }

    private static void AddCell(Grid grid, Control control, int row, int column)
    {
        Grid.SetRow(control, row);
        Grid.SetColumn(control, column);
        if (row > 0) control.Margin = new Thickness(control.Margin.Left, 3, control.Margin.Right, 3);
        grid.Children.Add(control);
    }

    // ---------------------------------------------------------------- binding capture

    private Button BindingButton(GameAction action, bool pad)
    {
        var text = Palette.Mono("", 13, Palette.Kapur);
        text.TextWrapping = TextWrapping.NoWrap;
        text.TextTrimming = TextTrimming.CharacterEllipsis;
        var button = Palette.Secondary("", () => BeginCapture(action, pad));
        button.Content = text;
        button.Padding = new Thickness(12, 6);
        button.Width = 158;
        button.HorizontalAlignment = HorizontalAlignment.Left;
        button.HorizontalContentAlignment = HorizontalAlignment.Left;
        bindingButtons[(action, pad)] = (button, text);
        return button;
    }

    private void RefreshBindings()
    {
        var s = Ctx.Settings;
        foreach (var ((action, pad), (button, text)) in bindingButtons)
        {
            var capturing = capture is { } c && c.Action == action && c.Pad == pad;
            text.Text = capturing ? Loc.T(pad ? "settings.controls.pressPad" : "settings.controls.pressKey")
                      : pad ? InputMapper.PadName(InputMapper.PadBinding(s, action))
                      : InputMapper.KeyName(InputMapper.KeyBinding(s, action));
            text.Foreground = capturing ? Palette.PradaBright : Palette.Kapur;
            button.BorderBrush = capturing ? Palette.PradaBright : Palette.Hairline;
        }
    }

    private void BeginCapture(GameAction action, bool pad)
    {
        if (capture is { } c && c.Action == action && c.Pad == pad) return;
        capture = (action, pad);
        captureAge = 0;
        captureArmed = false;
        Ctx.Input.Reset();
        status.Text = Loc.F("settings.controls.cancelHint", (int)CaptureTimeout);
        RefreshBindings();
    }

    private void CancelCapture()
    {
        if (capture is null) return;
        capture = null;
        Ctx.Input.Reset();
        status.Text = "";
        RefreshBindings();
    }

    private void FinishCapture(string newLabel, GameAction? swappedWith)
    {
        var action = capture!.Value.Action;
        capture = null;
        Ctx.Input.Reset();
        Ctx.SaveSettings();
        status.Text = swappedWith is { } other
            ? $"{Loc.T("action." + action)} → {newLabel} · {Loc.T("action." + other)} ⇄"
            : $"{Loc.T("action." + action)} → {newLabel}";
        RefreshBindings();
    }

    private void OnCaptureKey(object? sender, KeyEventArgs e)
    {
        if (capture is not { } c) return;
        e.Handled = true;
        if (e.Key == Key.Escape) { CancelCapture(); return; }
        if (c.Pad || !InputMapper.IsBindable(e.Key)) return;
        var s = Ctx.Settings;
        GameAction? swapped = null;
        for (var i = 0; i < InputMapper.ActionCount; i++)
        {
            var other = (GameAction)i;
            if (other != c.Action && InputMapper.KeyBinding(s, other) == e.Key && (c.Action, other) is not (GameAction.Grip, GameAction.Sprint) and not (GameAction.Sprint, GameAction.Grip)) swapped = other;
        }
        if (InputMapper.KeyBinding(s, c.Action) == e.Key) swapped = null;
        InputMapper.Rebind(s, c.Action, e.Key);
        FinishCapture(InputMapper.KeyName(e.Key), swapped);
    }

    private void OnCapturePointer(object? sender, PointerPressedEventArgs e)
    {
        if (capture is null) return;
        // Clicking anywhere while waiting for a key cancels, like Esc.
        CancelCapture();
        e.Handled = true;
    }

    public override void Tick(float dt)
    {
        base.Tick(dt);
        if (capture is not { } c) return;
        captureAge += dt;
        if (captureAge > CaptureTimeout) { CancelCapture(); return; }
        status.Text = Loc.F("settings.controls.cancelHint", (int)Math.Ceiling(CaptureTimeout - captureAge));
        // Skip the frame the capture began: the gamepad A that "clicked" the binding is still a fresh press.
        if (!captureArmed) { captureArmed = true; return; }
        if (!c.Pad) return;
        var pad = Ctx.Input.Pad;
        if (!pad.IsConnected) return;
        foreach (var b in InputMapper.CapturableButtons)
        {
            if (!pad.WasPressed(b)) continue;
            var s = Ctx.Settings;
            GameAction? swapped = null;
            for (var i = 0; i < InputMapper.ActionCount; i++)
            {
                var other = (GameAction)i;
                if (other != c.Action && !InputMapper.IsMovement(other) && InputMapper.PadBinding(s, other) == b) swapped = other;
            }
            if (InputMapper.PadBinding(s, c.Action) == b) swapped = null;
            InputMapper.Rebind(s, c.Action, b);
            FinishCapture(InputMapper.PadName(b), swapped);
            return;
        }
    }

    // ---------------------------------------------------------------- widgets

    private static Control Choice(string[] options, int selected, Action<int> changed)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        var buttons = new List<Button>();
        for (var i = 0; i < options.Length; i++)
        {
            var index = i;
            var b = Palette.Secondary(options[i], () => { });
            b.Padding = new Thickness(14, 7);
            b.Click += (_, _) =>
            {
                for (var k = 0; k < buttons.Count; k++) Mark(buttons[k], k == index);
                changed(index);
            };
            Mark(b, i == selected);
            buttons.Add(b);
            panel.Children.Add(b);
        }
        return panel;
    }

    /// <summary>Selected choice: gold border plus a thicker underline, so selection is not colour-only.</summary>
    private static void Mark(Button b, bool on)
    {
        b.BorderBrush = on ? Palette.Prada : Palette.Hairline;
        b.BorderThickness = on ? new Thickness(1, 1, 1, 3) : new Thickness(1);
    }

    private static Control Slider(float value, float min, float max, Action<float> changed)
    {
        var label = Palette.Mono($"{value:0.00}", 12, Palette.Prada);
        label.Width = 44;
        label.VerticalAlignment = VerticalAlignment.Center;
        var slider = new Slider { Minimum = min, Maximum = max, Value = value, Width = 300, Foreground = Palette.Prada };
        slider.ValueChanged += (_, e) =>
        {
            changed((float)e.NewValue);
            label.Text = $"{e.NewValue:0.00}";
        };
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14 };
        panel.Children.Add(slider);
        panel.Children.Add(label);
        return panel;
    }

    private static Control Toggle(bool value, Action<bool> changed)
    {
        var t = new ToggleSwitch { IsChecked = value, OnContent = "", OffContent = "" };
        t.IsCheckedChanged += (_, _) => changed(t.IsChecked == true);
        return t;
    }

    public override void Leave()
    {
        capture = null;
        Ctx.SaveSettings();
    }

    public override bool Back()
    {
        if (capture is not null)
        {
            CancelCapture();
            return true;
        }
        Ctx.SaveSettings();
        Shell.Navigate(returnTo ?? new TitleScreen(Shell));
        return true;
    }
}
