using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using NusantaraColossi.Core;
using NusantaraColossi.Game.Net;
using NusantaraColossi.Game.Rendering;
using NusantaraColossi.Game.UI.Controls;
using ThreeNet;

using Palette = NusantaraColossi.Game.UI.Theme;

namespace NusantaraColossi.Game.UI.Screens;

/// <summary>
/// The hunt. Runs the simulation at a fixed 60 Hz, feeds events to VFX, audio, camera and the HUD,
/// and shows pause / victory / defeat overlays. The same screen serves solo and co-op.
/// </summary>
public sealed class HuntScreen : Screen
{
    private const float Step = 1 / 60f;

    public Encounter Encounter { get; }
    public int LocalSlot { get; }
    private readonly CoopSession? coop;
    private readonly RegionDefinition region;
    private WorldRenderer? world;
    private float accumulator;
    private bool paused;
    private bool resultShown;
    private float hurtFlash;
    private float subtitleTimer;
    private float hintTimer;
    private float time;

    // HUD
    private readonly GripRing grip = new();
    private readonly CandiVitality vitality = new();
    private readonly HealthBar health = new();
    private readonly AbilitySlot scanSlot = new(), hackSlot = new(), decoySlot = new();
    private readonly StackPanel log = new() { Spacing = 3 };
    private readonly TextBlock hint = new();
    private readonly TextBlock subtitle = new();
    private readonly Border subtitleBox;
    private readonly Border hurtVignette;
    private readonly TextBlock gustWarning = new();
    private readonly TextBlock partyLabel = new();
    private readonly TextBlock fps = new();
    private readonly ContentControl overlay = new();
    private readonly StackPanel breathPanel = new() { Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 132), IsHitTestVisible = false, Opacity = 0 };
    private readonly Border breathFill = new() { Height = 4, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly HashSet<StrikeKind> volleyLogged = [];
    private readonly Grid hud;

    private HuntScreen(Shell shell, Encounter encounter, int localSlot, CoopSession? coop) : base(shell)
    {
        Encounter = encounter;
        LocalSlot = localSlot;
        this.coop = coop;
        region = encounter.Region;

        vitality.Encounter = encounter;
        var top = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 18, 0, 0), IsHitTestVisible = false };
        top.Children.Add(new TextBlock { Text = encounter.Blueprint.Name.Get().ToUpperInvariant(), FontFamily = Palette.Inscription, FontSize = 20, LetterSpacing = 4, Foreground = Palette.Kapur, HorizontalAlignment = HorizontalAlignment.Center });
        top.Children.Add(new TextBlock { Text = encounter.Blueprint.Epithet.Get() + (encounter.Options.Legend ? " · " + Loc.T("map.legend") : ""), FontFamily = Palette.Body, FontSize = 12.5, Foreground = encounter.Options.Legend ? Palette.Bara : Palette.KapurMuted, HorizontalAlignment = HorizontalAlignment.Center });
        top.Children.Add(vitality);

        var healthPanel = new StackPanel { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(36, 0, 0, 30), IsHitTestVisible = false };
        healthPanel.Children.Add(log);
        healthPanel.Children.Add(Palette.Mono((encounter.Players[localSlot].Name + " · " + Loc.T("hud.health")).ToUpperInvariant(), 10.5, Palette.KapurMuted).Also(t => t.Margin = new Thickness(0, 12, 0, 0)));
        healthPanel.Children.Add(health);

        var abilities = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 36, 30) };
        abilities.Children.Add(scanSlot);
        abilities.Children.Add(hackSlot);
        abilities.Children.Add(decoySlot);
        scanSlot.Label = Loc.T("hud.scan");
        hackSlot.Label = Loc.T("hud.hack");
        decoySlot.Label = Loc.T("hud.decoy");

        grip.HorizontalAlignment = HorizontalAlignment.Center;
        grip.VerticalAlignment = VerticalAlignment.Bottom;
        grip.Margin = new Thickness(0, 0, 0, 34);

        hint.FontFamily = Palette.Body;
        hint.FontSize = 15;
        hint.Foreground = Palette.Kapur;
        hint.HorizontalAlignment = HorizontalAlignment.Center;
        hint.VerticalAlignment = VerticalAlignment.Bottom;
        hint.Margin = new Thickness(0, 0, 0, 160);
        hint.IsHitTestVisible = false;

        subtitle.FontFamily = Palette.Body;
        subtitle.FontSize = 17;
        subtitle.Foreground = Palette.Kapur;
        subtitle.TextWrapping = TextWrapping.Wrap;
        subtitle.TextAlignment = TextAlignment.Center;
        subtitleBox = new Border
        {
            Child = subtitle,
            Background = Palette.ScrimLight,
            Padding = new Thickness(18, 8),
            MaxWidth = 760,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 200),
            IsVisible = false,
            IsHitTestVisible = false,
        };

        gustWarning.FontFamily = Palette.Telemetry;
        gustWarning.FontSize = 13;
        gustWarning.Foreground = Palette.Bara;
        gustWarning.HorizontalAlignment = HorizontalAlignment.Center;
        gustWarning.VerticalAlignment = VerticalAlignment.Center;
        gustWarning.Margin = new Thickness(0, -220, 0, 0);
        gustWarning.IsHitTestVisible = false;

        partyLabel.FontFamily = Palette.Telemetry;
        partyLabel.FontSize = 11;
        partyLabel.Foreground = Palette.Nadi;
        partyLabel.HorizontalAlignment = HorizontalAlignment.Left;
        partyLabel.VerticalAlignment = VerticalAlignment.Top;
        partyLabel.Margin = new Thickness(36, 26, 0, 0);
        partyLabel.IsHitTestVisible = false;

        fps.FontFamily = Palette.Telemetry;
        fps.FontSize = 10;
        fps.Foreground = Palette.KapurMuted;
        fps.HorizontalAlignment = HorizontalAlignment.Right;
        fps.VerticalAlignment = VerticalAlignment.Top;
        fps.Margin = new Thickness(0, 20, 30, 0);
        fps.IsHitTestVisible = false;

        hurtVignette = new Border
        {
            IsHitTestVisible = false,
            Opacity = 0,
            Background = new RadialGradientBrush
            {
                RadiusX = new RelativeScalar(.7, RelativeUnit.Relative),
                RadiusY = new RelativeScalar(.7, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.FromArgb(0, 224, 96, 58), .5), new GradientStop(Color.FromArgb(170, 150, 30, 20), 1) },
            },
        };

        // Breath: a cyan bar over the grip ring, only while lungs are not full.
        breathPanel.Children.Add(Palette.Mono(Loc.T("hud.breath").ToUpperInvariant(), 10, Palette.Nadi).Also(t => t.HorizontalAlignment = HorizontalAlignment.Center));
        breathFill.Background = Palette.Nadi;
        breathPanel.Children.Add(new Border { Width = 150, Height = 4, Background = Palette.ScrimLight, Child = breathFill });

        hud = new Grid { Children = { hurtVignette, top, healthPanel, abilities, grip, breathPanel, hint, subtitleBox, gustWarning, partyLabel, fps } };
        if (Ctx.IsMobile)
        {
            var input = Ctx.Input;
            hud.Children.Add(new TouchControls(input, input.PressJump, input.PressDodge, input.PressScan, input.PressHack, input.PressDecoy, input.PressLock, input.PressPause));
            abilities.Margin = new Thickness(0, 24, 90, 0);
            abilities.VerticalAlignment = VerticalAlignment.Top;
            grip.HorizontalAlignment = HorizontalAlignment.Left;
            grip.Margin = new Thickness(40, 0, 0, 160);
        }

        Content = new Grid { Children = { hud, overlay } };
        Ctx.Audio.Voice += OnVoice;
    }

    public override bool CapturesGameInput => !paused && !resultShown;

    public static HuntScreen Solo(Shell shell, RegionDefinition region, ColossusBlueprint guardian, EncounterOptions options)
    {
        var encounter = new Encounter(region, guardian, options);
        var profile = shell.Context.Profile;
        encounter.AddPlayer(profile.PlayerName, profile.Stats);
        return new HuntScreen(shell, encounter, 0, null);
    }

    public static HuntScreen Coop(Shell shell, CoopSession session, RegionDefinition region, ColossusBlueprint guardian, EncounterOptions options, string name)
    {
        var encounter = new Encounter(region, guardian, options);
        var slot = session.LocalSlot;
        while (encounter.Players.Count < slot) encounter.Players.Add(new Player(encounter.Players.Count, "…") { IsRemote = true });
        encounter.AddPlayer(name, shell.Context.Profile.Stats);
        session.Encounter = encounter;
        return new HuntScreen(shell, encounter, slot, session);
    }

    public override void Enter()
    {
        world = Shell.Stage.ShowHunt(Encounter, Ctx.Settings.Quality);
        world.Lightning += strength =>
        {
            Ctx.Audio.Thunder(strength);
            Shell.Stage.Camera.AddTrauma(.15f * strength);
        };
        Shell.Stage.Camera.ShakeScale = Ctx.Settings.CameraShake;
        Ctx.Audio.StartHunt(region);
        if (world.AssetWarning is { } warning) Log(warning, Palette.KapurMuted);
        if (!Ctx.Profile.IntroSeen)
        {
            Ctx.Audio.SayLine("voice.intro");
            Ctx.Profile.IntroSeen = true;
            Ctx.SaveProfile();
        }
    }

    public override void Leave()
    {
        Ctx.Audio.Voice -= OnVoice;
        if (coop is not null)
        {
            coop.Encounter = null;
            coop.Dispose();
        }
    }


    public override bool Back()
    {
        if (resultShown) return false;
        SetPaused(!paused);
        return true;
    }

    private Player Me => Encounter.Players[LocalSlot];

    public override void Tick(float dt)
    {
        time += dt;
        var input = Ctx.Input;
        if (CapturesGameInput && input.ConsumePause()) SetPaused(true);
        else if (paused && input.Pad.WasPressed(GamepadButton.Start)) SetPaused(false);
        var camera = Shell.Stage.Camera;
        if (input.ConsumeLockToggle()) camera.LockOn = !camera.LockOn;

        coop?.Poll();

        // Solo pauses the world; co-op keeps simulating (the host can't stop for one player).
        var simulate = !paused || coop is not null;
        if (simulate)
        {
            var look = CapturesGameInput ? input.Look(dt, Ctx.Settings.Sensitivity, Ctx.Settings.InvertY) : Vector2.Zero;
            camera.Update(Me, Encounter.Colossus, region, look, dt);

            accumulator = MathF.Min(accumulator + dt, .25f);
            Span<PlayerInput> inputs = stackalloc PlayerInput[1];
            while (accumulator >= Step)
            {
                accumulator -= Step;
                inputs[0] = autopilot > 0 ? AutopilotInput(camera.Forward)
                          : CapturesGameInput ? input.Sample(camera.Forward)
                          : new PlayerInput { CameraForward = camera.Forward };
                autopilot -= Step;
                Encounter.Step(inputs, Step, LocalSlot);
                foreach (var e in Encounter.Events) Handle(e);
                coop?.AfterStep(Step);
            }
            if (coop is not null)
            {
                foreach (var e in coop.ReceivedEvents) Handle(e);
                coop.ReceivedEvents.Clear();
            }
        }

        world?.Update(simulate ? dt : 0, camera, LocalSlot, Me.Stats.HunterInstinct);
        UpdateHud(dt);
        CheckOutcome();
    }

    private void Handle(GameEvent e)
    {
        world?.OnEvent(e);
        Ctx.Audio.OnEvent(e, LocalSlot);
        var camera = Shell.Stage.Camera;
        var mine = e.Slot == LocalSlot;
        var distance = Vector3.Distance(e.Position, Me.Position);
        switch (e.Kind)
        {
            case GameEventKind.StompImpact or GameEventKind.SweepHit or GameEventKind.ChargeHit:
                camera.AddTrauma(Math.Clamp(1.1f - distance / 60, .1f, .8f));
                Rumble(.6f, .4f, 260);
                break;
            case GameEventKind.RoarWave: camera.AddTrauma(.35f); Rumble(.3f, .5f, 500); break;
            case GameEventKind.Collapse: camera.AddTrauma(.7f); Rumble(.8f, .5f, 900); break;
            case GameEventKind.CoreDestroyed: camera.AddTrauma(.5f); Log(Loc.T("log.core"), Palette.Prada); Rumble(.7f, .7f, 600); break;
            case GameEventKind.CoreResonance: Log(Loc.T("log.resonance"), Palette.Nadi); break;
            case GameEventKind.CoreHit: if (mine) Rumble(.3f, .6f, 120); break;
            case GameEventKind.PlayerHurt when mine: hurtFlash = 1; camera.AddTrauma(.35f); Rumble(.5f, .3f, 200); break;
            case GameEventKind.Rage: Log(Loc.T("log.rage"), Palette.Bara); camera.AddTrauma(.3f); break;
            case GameEventKind.ShakeWarn: if (Me.Mode == PlayerMode.Attached) Log(Loc.T("log.shake"), Palette.Bara); break;
            case GameEventKind.GustWarn: Log(Loc.T("log.gust"), Palette.Bara); break;
            case GameEventKind.Awaken: Log(Loc.T("log.awaken"), Palette.Prada); break;
            case GameEventKind.SecondWind when mine: Log(Loc.T("log.secondWind"), Palette.Nadi); break;
            case GameEventKind.StaminaLow when mine: Log(Loc.T("log.stamina"), Palette.Bara); Ctx.Audio.SayLine("voice.grip"); break;
            case GameEventKind.StabSealed when mine: Log(Loc.T("log.sealed"), Palette.KapurMuted); ShowHint(Loc.F("hud.hint.scan", Ctx.Input.ScanLabel)); break;
            case GameEventKind.StabDeflected when mine: Log(Loc.T("log.deflected"), Palette.KapurMuted); ShowHint(Loc.F("hud.hint.plated", Ctx.Input.HackLabel)); break;
            case GameEventKind.Scan when mine:
                var open = 0;
                foreach (var c in Encounter.Colossus.Cores) if (!c.Destroyed) open++;
                Log(Loc.F("log.scan", open, (int)(Encounter.Blueprint.SealOpenSeconds * Me.Stats.SealScale)), Palette.Nadi);
                break;
            case GameEventKind.ScanDenied when mine: LogDenied(DroneAbilities.Scan, () => Loc.F("log.scanCooldown", e.Value)); break;
            case GameEventKind.Hack when mine: Log(Loc.T("log.hack"), Palette.Nadi); break;
            case GameEventKind.HackDenied when mine && e.Value < 0: Log(Loc.T("log.immune"), Palette.Nadi); Ctx.Audio.SayLine("voice.immune"); break;
            case GameEventKind.HackDenied when mine: LogDenied(DroneAbilities.Hack, () => Loc.T("log.hackDenied")); break;
            case GameEventKind.FloodWarn: Log(Loc.T("log.flood"), Palette.Bara); break;
            case GameEventKind.DiveWarn: Log(Loc.T("log.dive"), Palette.Bara); break;
            case GameEventKind.Surface: Log(Loc.T("log.surface"), Palette.KapurMuted); break;
            case GameEventKind.DiveStart or GameEventKind.FloodRise: camera.AddTrauma(.25f); Rumble(.4f, .2f, 600); break;
            case GameEventKind.Volley:
                // Name each new threat once; after that the rings speak for themselves.
                if (volleyLogged.Add((StrikeKind)(int)e.Value)) Log(Loc.T("log.volley." + (StrikeKind)(int)e.Value), Palette.Bara);
                break;
            case GameEventKind.StrikeImpact:
                if (distance < 30) { camera.AddTrauma(Math.Clamp(.5f - distance / 60, .05f, .4f)); Rumble(.35f, .3f, 160); }
                break;
            case GameEventKind.BreathLow when mine: Log(Loc.T("log.breath"), Palette.Bara); break;
            case GameEventKind.Decoy when mine: Log(Loc.T("log.decoy"), Palette.Nadi); break;
            case GameEventKind.DecoyDenied when mine: LogDenied(DroneAbilities.Decoy, () => Loc.T("log.decoyDenied")); break;
        }
    }

    private void LogDenied(DroneAbilities ability, Func<string> message) =>
        Log((Encounter.Options.Abilities & ability) == 0 ? Loc.T("log.locked") : message(), Palette.KapurMuted);

    private void Rumble(float strong, float weak, int ms)
    {
        if (!Ctx.Input.UsingGamepad || Ctx.Pads is null) return;
        try { Ctx.Pads.Rumble(0, strong, weak, TimeSpan.FromMilliseconds(ms)); }
        catch (ThreeNetException) { }
    }

    private void Log(string text, IBrush brush)
    {
        log.Children.Add(new TextBlock { Text = text, FontFamily = Palette.Telemetry, FontSize = 12, Foreground = brush, Tag = time });
        while (log.Children.Count > 5) log.Children.RemoveAt(0);
    }

    private void ShowHint(string text)
    {
        hint.Text = text;
        hintTimer = 4;
    }

    private void OnVoice(string key)
    {
        if (!Ctx.Settings.Subtitles) return;
        subtitle.Text = "GARUDA: " + Loc.T(key);
        subtitleBox.IsVisible = true;
        subtitleTimer = 5;
    }

    private void UpdateHud(float dt)
    {
        var me = Me;
        var c = Encounter.Colossus;
        var stats = me.Stats;
        var attached = me.Mode == PlayerMode.Attached;

        grip.Time = time;
        grip.Stamina = me.Stamina / stats.MaxStamina;
        grip.Charge = me.Charging ? me.StabCharge : 0;
        grip.Latched = Ctx.Input.GripLatched;
        grip.Opacity = Mathx.Damp((float)grip.Opacity, attached || grip.Stamina < .99f || me.Charging || grip.Latched ? 1 : .0f, 6, dt);

        // Pulse dot only when close to an open core: the beat is information, not decoration.
        var nearOpen = false;
        var nearSealed = false;
        var nearPlated = false;
        if (attached && !c.IsDefeated)
        {
            var bp = Encounter.Blueprint;
            for (var i = 0; i < bp.Cores.Length; i++)
            {
                if (c.Cores[i].Destroyed) continue;
                var (position, _) = ColossusRig.CoreWorld(bp, bp.Cores[i], c.PartWorld);
                if (Vector3.Distance(position, me.BodyCenter) > 4) continue;
                if (c.Cores[i].SealOpen <= 0) nearSealed = true;
                else if (bp.Cores[i].Plated && c.State != ColossusState.Stunned) nearPlated = true;
                else nearOpen = true;
            }
        }
        breathPanel.Opacity = Mathx.Damp((float)breathPanel.Opacity, me.Breath < .995f && me.Mode != PlayerMode.Dead ? 1 : 0, 6, dt);
        breathFill.Width = 150 * me.Breath;
        breathFill.Background = me.Breath < .35f ? Palette.Bara : Palette.Nadi;

        grip.ShowPulse = nearOpen;
        grip.Pulse = c.CorePulse(Encounter.Elapsed);
        grip.InvalidateVisual();

        vitality.Time = time;
        vitality.InvalidateVisual();

        health.Value = me.Health / stats.MaxHealth;
        hurtFlash = MathF.Max(0, hurtFlash - dt * 1.8f);
        health.Flash = hurtFlash;
        health.InvalidateVisual();
        hurtVignette.Opacity = MathF.Max(hurtFlash * .9f, health.Value < .3f && me.Mode != PlayerMode.Dead ? .25f + .15f * MathF.Sin(time * 5) : 0);

        ref var drone = ref Encounter.Drones[LocalSlot];
        var abilities = Encounter.Options.Abilities;
        Configure(scanSlot, Ctx.Input.ScanLabel, drone.ScanCooldown / stats.ScanCooldown, (abilities & DroneAbilities.Scan) == 0);
        Configure(hackSlot, Ctx.Input.HackLabel, drone.HackCooldown / stats.HackCooldown, (abilities & DroneAbilities.Hack) == 0);
        Configure(decoySlot, Ctx.Input.DecoyLabel, drone.DecoyCooldown / stats.DecoyCooldown, (abilities & DroneAbilities.Decoy) == 0);

        // Contextual hint, lowest priority first.
        hintTimer -= dt;
        if (hintTimer <= 0)
        {
            string? text = null;
            if (nearOpen) text = Loc.F("hud.hint.stab", Ctx.Input.StabLabel);
            else if (nearPlated) text = Loc.F("hud.hint.plated", Ctx.Input.HackLabel);
            else if (nearSealed) text = Loc.F("hud.hint.scan", Ctx.Input.ScanLabel);
            else if (me.IsRiding) text = Loc.F("hud.hint.ride", Ctx.Input.InteractLabel, Ctx.Input.JumpLabel);
            else if (me.Mode == PlayerMode.Air && !me.Gliding && me.AirTime > .4f && me.Position.Y - region.Height(me.Position.X, me.Position.Z) > 6)
                text = Loc.F("hud.hint.glide", Ctx.Input.JumpLabel);
            else if (!attached && me.IsOnGround && Vector3.Distance(Mathx.Flat(me.Position), Mathx.Flat(c.Pose.Position)) < c.Blueprint.Height * .6f + 6 && !c.IsDefeated)
                text = Loc.F("hud.hint.grip", Ctx.Input.GripLabel);
            else if (me.IsOnGround && NearbyVehicle(me) is { } kind)
                text = Loc.F("hud.hint.mount", Ctx.Input.InteractLabel, Loc.T("vehicle." + kind));
            hint.Text = text ?? "";
        }
        hint.Opacity = string.IsNullOrEmpty(hint.Text) ? 0 : .92;

        subtitleTimer -= dt;
        if (subtitleTimer <= 0) subtitleBox.IsVisible = false;

        gustWarning.Text = Encounter.GustWarning ? "<<<  " + (Loc.Language == "en" ? "GUST" : "HEMBUSAN") + "  >>>" : "";
        gustWarning.Opacity = .6 + .4 * Math.Sin(time * 10);

        // Telemetry fades after 7 s.
        foreach (var child in log.Children)
            if (child is TextBlock { Tag: float born } line) line.Opacity = Math.Clamp(8 - (time - born), 0, 1);

        if (coop is not null)
        {
            var names = string.Join("  ", Encounter.Players.Where(p => p.Name != "…").Select(p => $"{(p.Mode == PlayerMode.Dead ? "✕" : "◆")} {p.Name}"));
            partyLabel.Text = $"GARUDA//PARTY  {names}" + (coop.HudStatus is { } link ? "\n" + link : "");
            partyLabel.Foreground = coop.HudAlert ? Palette.Bara : Palette.Nadi; // ember while reconnecting / migrating
        }
        fps.Text = $"{Shell.FrameRate:0} FPS";
    }

    private VehicleKind? NearbyVehicle(Player me)
    {
        for (var i = 0; i < Encounter.VehicleCount; i++)
        {
            ref readonly var v = ref Encounter.Vehicles[i];
            if (v.Rider < 0 && Vector3.Distance(Mathx.Flat(v.Position), Mathx.Flat(me.Position)) < Vehicle.MountRange) return v.Kind;
        }
        return null;
    }

    private static void Configure(AbilitySlot slot, string key, float cooldown, bool locked)
    {
        slot.Key = key;
        slot.Cooldown = cooldown;
        slot.Locked = locked;
        slot.InvalidateVisual();
    }

    private void SetPaused(bool value)
    {
        if (resultShown) return;
        paused = value;
        Ctx.Input.Reset();
        if (!paused)
        {
            overlay.Content = null;
            Focus();
            return;
        }
        var panel = Panel(Loc.T("hud.paused"), coop is null ? Encounter.Blueprint.Name.Get() : "GARUDA//PARTY");
        var menu = new StackPanel { Spacing = 2, Margin = new Thickness(0, 20, 0, 0) };
        menu.Children.Add(Palette.MenuItem(Loc.T("hud.resume"), () => SetPaused(false)));
        if (coop is null) menu.Children.Add(Palette.MenuItem(Loc.T("hud.restart"), Restart));
        menu.Children.Add(Palette.MenuItem(Loc.T("hud.abandon"), () => Shell.FadeThrough(() => Shell.Navigate(new MapScreen(Shell, region.Id)))));
        panel.Children.Add(menu);
        panel.Children.Add(new TextBlock { Text = Ctx.Input.Summary(Ctx.Input.UsingGamepad), FontFamily = Palette.Body, FontSize = 12.5, Foreground = Palette.KapurMuted, TextWrapping = TextWrapping.Wrap, MaxWidth = 520, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 26, 0, 0), LineHeight = 20 });
        ShowOverlay(panel);
    }

    private void Restart()
    {
        var options = Encounter.Options with { Seed = Environment.TickCount };
        Shell.FadeThrough(() => Shell.Navigate(Solo(Shell, region, Encounter.Blueprint, options)));
    }

    private StackPanel Panel(string title, string eyebrow)
    {
        var s = new StackPanel { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(Ctx.IsMobile ? 32 : 96, 0, 0, 0) };
        s.Children.Add(Palette.Eyebrow(eyebrow, Palette.Nadi));
        s.Children.Add(Palette.Title(title, 46));
        s.Children.Add(Palette.Rule(Palette.Prada, 96).Also(r => r.Margin = new Thickness(0, 12, 0, 0)));
        return s;
    }

    private void ShowOverlay(Control content)
    {
        overlay.Content = new Grid
        {
            Background = Palette.Scrim,
            Children = { content },
        };
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            foreach (var b in content.GetVisualDescendantsOfType<Button>()) { b.Focus(); break; }
        }, Avalonia.Threading.DispatcherPriority.Background);
    }

    private void CheckOutcome()
    {
        if (resultShown || Encounter.Phase == EncounterPhase.Fight) return;
        // Let the collapse play before the card appears.
        if (Encounter.Phase == EncounterPhase.Victory && Encounter.Colossus.Pose.Collapse < .98f) return;
        resultShown = true;
        Ctx.Input.Reset();
        if (Encounter.Phase == EncounterPhase.Victory) ShowVictory();
        else ShowDefeat();
    }

    private void ShowVictory()
    {
        var e = Encounter;
        var rank = e.Rank;
        var first = !Ctx.Profile.IsDefeated(e.Blueprint.Id);
        var points = Ctx.Profile.RecordVictory(e.Blueprint.Id, e.Elapsed, rank, e.Options.Legend);
        var hunters = e.Players.Count(p => p.Name != "…");
        var place = Ctx.Profile.RecordTime(e.Blueprint.Id, e.Elapsed, rank, e.Options.Legend, hunters, DateTime.Now);
        Ctx.SaveProfile();
        var finale = e.Region.Secret;
        if (finale) Ctx.Audio.SayLine("voice.finale");

        var layout = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(Ctx.IsMobile ? 32 : 96, 0, 48, 0) };
        var rankText = new TextBlock { Text = rank.ToString(), FontFamily = Palette.Inscription, FontSize = Ctx.IsMobile ? 120 : 180, Foreground = rank == Rank.S ? Palette.PradaBright : Palette.Prada, Margin = new Thickness(0, -20, 48, 0), VerticalAlignment = VerticalAlignment.Top };
        layout.Children.Add(rankText);

        var s = new StackPanel { MaxWidth = 560 };
        s.Children.Add(Palette.Eyebrow(e.Blueprint.Name.Get(), Palette.Nadi));
        s.Children.Add(Palette.Title(Loc.T("victory.title"), 42));
        var stats = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto,Auto"), Margin = new Thickness(0, 18, 0, 0) };
        void Stat(int col, string label, string value)
        {
            var p = new StackPanel { Margin = new Thickness(0, 0, 36, 0) };
            p.Children.Add(Palette.Mono(label.ToUpperInvariant(), 10.5, Palette.KapurMuted));
            p.Children.Add(Palette.Text(value, 22, Palette.Kapur));
            Grid.SetColumn(p, col);
            stats.Children.Add(p);
        }
        Stat(0, Loc.T("victory.time"), MapScreen.FormatTime(e.Elapsed));
        Stat(1, Loc.T("victory.rank"), rank.ToString());
        Stat(2, Loc.T("victory.resonance"), e.Resonances.ToString());
        Stat(3, Loc.T("victory.damage"), $"{e.DamageTaken:0}");
        s.Children.Add(stats);
        if (points > 0) s.Children.Add(Palette.Text(Loc.F("victory.points", points), 15, Palette.Prada).Also(t => t.Margin = new Thickness(0, 16, 0, 0)));
        s.Children.Add(TimeBoard(e.Blueprint.Id, place));
        if (finale)
        {
            s.Children.Add(Palette.Mono(Loc.T("finale.eyebrow").ToUpperInvariant(), 10.5, Palette.Nadi).Also(t => t.Margin = new Thickness(0, 22, 0, 6)));
            s.Children.Add(new TextBlock { Text = Loc.T("finale.body"), FontFamily = Palette.Body, FontSize = 15, Foreground = Palette.Kapur, TextWrapping = TextWrapping.Wrap, LineHeight = 24, MaxWidth = 540, HorizontalAlignment = HorizontalAlignment.Left });
        }
        if (first)
        {
            s.Children.Add(Palette.Mono(Loc.T("victory.unlocked").ToUpperInvariant(), 10.5, Palette.Nadi).Also(t => t.Margin = new Thickness(0, 22, 0, 6)));
            s.Children.Add(new Border
            {
                BorderBrush = Palette.Prada,
                BorderThickness = new Thickness(2, 0, 0, 0),
                Padding = new Thickness(16, 4, 0, 4),
                Child = new TextBlock { Text = e.Blueprint.Inscription.Get(), FontFamily = Palette.Inscription, FontSize = 16, Foreground = Palette.Kapur, TextWrapping = TextWrapping.Wrap, LineHeight = 26 },
            });
        }
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 30, 0, 0) };
        buttons.Children.Add(Palette.Primary(Loc.T("victory.continue"), () => Shell.FadeThrough(() => Shell.Navigate(new MapScreen(Shell, NextRegion())))));
        s.Children.Add(buttons);
        Grid.SetColumn(s, 1);
        layout.Children.Add(s);
        ShowOverlay(layout);
    }

    /// <summary>The five fastest runs on this guardian; the new one, if it placed, is marked in gold.</summary>
    private Control TimeBoard(string colossusId, int place)
    {
        var panel = new StackPanel { Spacing = 2, Margin = new Thickness(0, 20, 0, 0) };
        var title = Loc.T("victory.board").ToUpperInvariant() + (place > 0 ? "  ·  " + Loc.F("victory.place", place).ToUpperInvariant() : "");
        panel.Children.Add(Palette.Mono(title, 10.5, place > 0 ? Palette.Prada : Palette.KapurMuted));
        var board = Ctx.Profile.Leaderboard(colossusId);
        for (var i = 0; i < board.Count; i++)
        {
            var entry = board[i];
            var mine = i + 1 == place;
            var extra = (entry.Legend ? " · " + Loc.T("map.legend") : "") + (entry.Hunters > 1 ? " · " + Loc.F("victory.coop", entry.Hunters) : "");
            var line = $"{i + 1}.  {MapScreen.FormatTime(entry.Seconds),-8} {entry.Rank}   {entry.Date}{extra}";
            panel.Children.Add(Palette.Mono((mine ? "▶ " : "  ") + line, 12, mine ? Palette.PradaBright : Palette.Kapur));
        }
        return panel;
    }

    private RegionId NextRegion()
    {
        var next = Regions.All.FirstOrDefault(r => Ctx.Profile.IsUnlocked(r.Id) && !Ctx.Profile.IsDefeated(r.ColossusId));
        return next?.Id ?? region.Id;
    }

    private void ShowDefeat()
    {
        var s = Panel(Loc.T("defeat.title"), Encounter.Blueprint.Name.Get());
        s.Children.Add(Palette.Text(Loc.T("defeat.body"), 15, Palette.KapurMuted).Also(t => { t.Margin = new Thickness(0, 16, 0, 0); t.MaxWidth = 460; t.HorizontalAlignment = HorizontalAlignment.Left; t.TextWrapping = TextWrapping.Wrap; }));
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 28, 0, 0) };
        if (coop is null) buttons.Children.Add(Palette.Primary(Loc.T("defeat.retry"), Restart));
        buttons.Children.Add(Palette.Secondary(Loc.T("menu.map"), () => Shell.FadeThrough(() => Shell.Navigate(new MapScreen(Shell, region.Id)))));
        s.Children.Add(buttons);
        ShowOverlay(s);
    }

    /// <summary>Screenshot tour: drive Arka toward the guardian without a player.</summary>
    internal void Autopilot(float seconds)
    {
        autopilot = seconds;
        Shell.Stage.Camera.LockOn = true;
    }

    private float autopilot;

    internal PlayerInput AutopilotInput(Vector3 cameraForward)
    {
        var to = Mathx.Flat(Encounter.Colossus.Pose.Position - Me.Position);
        var f = Mathx.Flat(cameraForward);
        var right = new Vector3(-f.Z, 0, f.X);
        var dir = Mathx.SafeNormalize(to, Vector3.UnitZ);
        return new PlayerInput { CameraForward = cameraForward, Move = new Vector2(-Vector3.Dot(dir, right), Vector3.Dot(dir, Mathx.SafeNormalize(f, Vector3.UnitZ))), Grip = true, Sprint = true };
    }
}
