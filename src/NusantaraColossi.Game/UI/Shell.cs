using System.Diagnostics;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using NusantaraColossi.Game.Rendering;
using NusantaraColossi.Game.UI.Screens;
using ThreeNet;
using ThreeNet.Avalonia;
using Key = Avalonia.Input.Key;
using MouseButton = Avalonia.Input.MouseButton;

using Palette = NusantaraColossi.Game.UI.Theme;

namespace NusantaraColossi.Game.UI;

/// <summary>Base for every full-screen page layered over the 3D stage.</summary>
public abstract class Screen : UserControl
{
    protected Screen(Shell shell)
    {
        Shell = shell;
        Focusable = true;
    }

    protected Shell Shell { get; }
    protected GameContext Ctx => Shell.Context;

    /// <summary>Raw game input (WASD, mouse look) goes to the simulation, not to focus navigation.</summary>
    public virtual bool CapturesGameInput => false;

    public virtual void Enter() { }
    public virtual void Leave() { }

    /// <summary>Called once per rendered frame on the UI thread.</summary>
    public virtual void Tick(float dt) => Shell.Stage.TickShowcase(dt);

    /// <summary>Escape / gamepad B. Return true when handled.</summary>
    public virtual bool Back()
    {
        Shell.Navigate(new TitleScreen(Shell));
        return true;
    }
}

/// <summary>
/// Root of the app on every platform: the ThreeNet viewport, a cinematic vignette, the active
/// screen, and a fade layer. Owns the frame loop — an 8 ms dispatcher timer that drives the screen
/// and renders manually (IsRendering = false), so simulation, audio and rendering share one clock.
/// </summary>
public sealed class Shell : UserControl, IDisposable
{
    private readonly ThreeNetView viewport;
    private readonly ContentControl host = new();
    private readonly Border fade;
    private readonly TextBlock failure;
    private readonly DispatcherTimer timer;
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private double lastTick, lastRender;
    private bool rendererFailed;
    /// <summary>NC_PROFILE=1 (or <see cref="ProfileOverride"/> on Android) logs frame timings every 2 s.</summary>
    public static bool ProfileOverride { get; set; }
    /// <summary>Where profile lines go; Android routes them to logcat (tag NC).</summary>
    public static Action<string> Log { get; set; } = line => Console.Error.WriteLine(line);
    private static readonly bool ProfileEnv = Environment.GetEnvironmentVariable("NC_PROFILE") == "1";
    private static bool Profile => ProfileOverride || ProfileEnv;
    private double profileStart, profileTick, profileRender;
    private int profileFrames;

    public GameContext Context { get; }
    public Stage Stage { get; } = new();
    public Screen? Current { get; private set; }
    public bool RendererFailed => rendererFailed;
    public double FrameRate { get; private set; }

    public Shell(GameContext context)
    {
        Context = context;
        Background = Palette.Andesite;
        ClipToBounds = true;
        Styles.Add(Palette.Styles());

        var quality = int.TryParse(Environment.GetEnvironmentVariable("NC_Q"), out var q) ? q : context.Settings.Quality;
        viewport = new ThreeNetView
        {
            IsRendering = false,
            RenderScale = quality switch { 0 => .7f, 1 => context.IsMobile ? .8f : 1f, _ => 1f },
            RendererOptions = RendererOptions.Default with
            {
                BgraOutput = true,
                VSync = false,
                MsaaSamples = quality >= 2 ? 4 : 1,
                Shadows = quality >= 1,
                ShadowMapSize = quality >= 2 ? 2048 : 1024,
                ShadowDistance = quality >= 2 ? 140 : 90,
                ShadowCascades = quality >= 2 ? 3 : 2,
                Bloom = quality >= 1,
                BloomIntensity = .55f,
                BloomThreshold = 1.1f,
                ToneMapping = ToneMapping.Aces,
                Exposure = .8f,
                Ssao = quality >= 2,
                FrustumCulling = true,
            },
            Focusable = false,
        };
        viewport.RenderFailed += (_, args) => OnRenderFailed(args);
        Stage.WorldChanged += world =>
        {
            if (world is null) return;
            viewport.Scene = world.Scene;
            viewport.Camera = world.Camera;
        };

        // Cinematic vignette so gold UI text always reads against bright skies.
        var vignette = new Border
        {
            IsHitTestVisible = false,
            Background = new RadialGradientBrush
            {
                Center = new RelativePoint(.5, .5, RelativeUnit.Relative),
                GradientOrigin = new RelativePoint(.5, .5, RelativeUnit.Relative),
                RadiusX = new RelativeScalar(.75, RelativeUnit.Relative),
                RadiusY = new RelativeScalar(.75, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(0, 0, 0, 0), .55),
                    new GradientStop(Color.FromArgb(150, 6, 8, 7), 1),
                },
            },
        };

        failure = new TextBlock
        {
            IsVisible = false,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Palette.Bara,
            FontFamily = Palette.Telemetry,
            FontSize = 13,
            Margin = new Thickness(24),
            VerticalAlignment = VerticalAlignment.Bottom,
            HorizontalAlignment = HorizontalAlignment.Right,
            MaxWidth = 520,
            IsHitTestVisible = false,
        };

        fade = new Border
        {
            Background = Brushes.Black,
            Opacity = 1,
            IsHitTestVisible = false,
            Transitions = [new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(420), Easing = new Avalonia.Animation.Easings.CubicEaseInOut() }],
        };

        Content = new Grid { Children = { viewport, vignette, host, failure, fade } };

        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, OnKeyUp, RoutingStrategies.Tunnel);
        if (Profile) AddHandler(PointerPressedEvent, (_, e) => Log($"[input] {e.Pointer.Type} press at {e.GetPosition(this)} on {e.Source?.GetType().Name}"), RoutingStrategies.Tunnel, handledEventsToo: true);
        if (Profile) AddHandler(PointerReleasedEvent, (_, e) => Log($"[input] {e.Pointer.Type} release at {e.GetPosition(this)} handled={e.Handled}"), RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(PointerPressedEvent, (_, e) => { if (Current?.CapturesGameInput == true) Context.Input.PointerPressed(e.GetCurrentPoint(this).Properties, e.GetPosition(this)); }, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, (_, e) => Context.Input.PointerReleased(e.InitialPressMouseButton switch
        {
            MouseButton.Left => PointerUpdateKind.LeftButtonReleased,
            MouseButton.Right => PointerUpdateKind.RightButtonReleased,
            _ => PointerUpdateKind.Other,
        }), RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerMovedEvent, (_, e) => Context.Input.PointerMoved(e.GetPosition(this)), RoutingStrategies.Tunnel, handledEventsToo: true);
        LostFocus += (_, _) => { if (Current?.CapturesGameInput != true) Context.Input.Reset(); };

        // On phones and tablets a frame often takes longer than the poll interval. A Render-priority
        // timer would then always be due and starve touch input and layout, so mobile polls at
        // Background priority: input and layout run first, then the next frame.
        timer = new DispatcherTimer(TimeSpan.FromMilliseconds(8), App.IsMobile ? DispatcherPriority.Background : DispatcherPriority.Render, (_, _) => Frame());
        // Transitions need a live render clock, so the first screen is created once attached.
        AttachedToVisualTree += (_, _) =>
        {
            timer.Start();
            if (Current is null) Navigate(new TitleScreen(this));
        };
        DetachedFromVisualTree += (_, _) => timer.Stop();

        Context.Audio.Preload();
    }

    public void Navigate(Screen screen)
    {
        Context.Input.Reset();
        Current?.Leave();
        Current = screen;
        host.Content = screen;
        screen.Opacity = 0;
        screen.Transitions = [new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(320) }];
        screen.Enter();
        Dispatcher.UIThread.Post(() =>
        {
            screen.Opacity = 1;
            fade.Opacity = 0;
            if (!screen.CapturesGameInput) FocusFirst(screen);
            else screen.Focus();
        }, DispatcherPriority.Background);
    }

    /// <summary>Fades to black, runs <paramref name="work"/> (e.g. building a world), then fades back.</summary>
    public void FadeThrough(Action work)
    {
        fade.Opacity = 1;
        DispatcherTimer.RunOnce(() =>
        {
            work();
            fade.Opacity = 0;
        }, TimeSpan.FromMilliseconds(440));
    }

    private static void FocusFirst(Control root)
    {
        foreach (var control in root.GetVisualDescendantsOfType<Button>())
        {
            if (control.IsEffectivelyEnabled && control.IsEffectivelyVisible)
            {
                control.Focus(NavigationMethod.Directional);
                return;
            }
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (Current?.CapturesGameInput == true)
        {
            Context.Input.KeyDown(e.Key);
            if (e.Key is Key.Tab or Key.Space or Key.Up or Key.Down or Key.Left or Key.Right) e.Handled = true;
            return;
        }
        if (e.Key == Key.Escape && Current?.Back() == true) e.Handled = true;
    }

    private void OnKeyUp(object? sender, KeyEventArgs e) => Context.Input.KeyUp(e.Key);

    private void OnRenderFailed(object? args)
    {
        if (rendererFailed) return;
        rendererFailed = true;
        var message = args?.GetType().GetProperty("Message")?.GetValue(args)?.ToString()
                   ?? args?.GetType().GetProperty("Exception")?.GetValue(args)?.ToString()
                   ?? args?.ToString() ?? "unknown";
        failure.Text = Loc.F("rendererFailed", message);
        failure.IsVisible = true;
    }

    private void Frame()
    {
        var now = clock.Elapsed.TotalSeconds;
        var cap = Math.Clamp(Context.Settings.FpsCap, 24, 240);
        // Render only when the frame budget is reached; the timer just polls.
        if (now - lastRender < 1.0 / cap - .0015) return;
        var dt = (float)Math.Min(now - lastTick, .1);
        lastTick = now;
        FrameRate = FrameRate * .9 + .1 / Math.Max(1e-3, now - lastRender);
        lastRender = now;

        if (Context.Pads is { } pads)
        {
            try
            {
                pads.Update();
                Context.Input.Pad = pads.Get(0);
                if (Current?.CapturesGameInput != true) MenuGamepad(Context.Input.Pad);
            }
            catch (ThreeNetException) { }
        }

        var t0 = clock.Elapsed.TotalMilliseconds;
        try { Current?.Tick(dt); }
        catch (Exception ex) when (ex is ThreeNetException)
        {
            OnRenderFailed(ex);
        }
        var t1 = clock.Elapsed.TotalMilliseconds;
        Context.Audio.Update(dt, Current is HuntScreen h ? h.Encounter : null, Current is HuntScreen hs ? hs.LocalSlot : 0, Stage.World?.Scene, Stage.World?.Camera);

        if (!rendererFailed && Stage.World is not null)
        {
            try { viewport.RenderFrame(); }
            catch (Exception ex) when (ex is ThreeNetException or InvalidOperationException) { OnRenderFailed(ex); }
        }
        if (Context.Settings.DynamicResolution && Current is HuntScreen) AdaptResolution(now, clock.Elapsed.TotalMilliseconds - t1);
        if (Profile)
        {
            var t2 = clock.Elapsed.TotalMilliseconds;
            profileTick += t1 - t0;
            profileRender += t2 - t1;
            profileFrames++;
            if (now - profileStart > 2)
            {
                var r = viewport.Renderer;
                var st = r?.Stats;
                Log($"[frame] {profileFrames / (now - profileStart):0.0} fps  tick {profileTick / profileFrames:0.00} ms  render {profileRender / profileFrames:0.00} ms  ({Current?.GetType().Name}) {r?.AdapterName} {r?.Width}x{r?.Height} draws {st?.DrawCalls} tris {st?.Triangles} lights {st?.Lights} shadowDraws {st?.ShadowDrawCalls} cpu {st?.CpuTimeMs:0.0} gpu {st?.GpuTimeMs:0.0}");
                profileStart = now;
                profileTick = profileRender = 0;
                profileFrames = 0;
            }
        }
    }

    private double renderAverage, lastAdapt;
    private double maxScale = -1;

    /// <summary>
    /// Dynamic resolution: steps RenderScale down when frames run over budget and back up when there
    /// is headroom. Changes are rare (≥1.5 s apart) because each one rebuilds the render target.
    /// </summary>
    private void AdaptResolution(double now, double renderMs)
    {
        if (maxScale < 0) maxScale = viewport.RenderScale;
        renderAverage = renderAverage <= 0 ? renderMs : renderAverage * .95 + renderMs * .05;
        if (now - lastAdapt < 1.5) return;
        var budget = 1000.0 / Math.Clamp(Context.Settings.FpsCap, 24, 240);
        var scale = viewport.RenderScale;
        if (renderAverage > budget * 1.1 && scale > .5) scale = Math.Max(.5, scale - .1);
        else if (renderAverage < budget * .6 && scale < maxScale) scale = Math.Min(maxScale, scale + .05);
        else return;
        viewport.RenderScale = scale;
        lastAdapt = now;
        renderAverage = 0;
    }

    private void MenuGamepad(GamepadState pad)
    {
        if (!pad.IsConnected) return;
        var top = TopLevel.GetTopLevel(this);
        var focused = top?.FocusManager?.GetFocusedElement() as InputElement;
        NavigationDirection? dir = null;
        if (pad.WasPressed(GamepadButton.DPadDown) || pad.WasPressed(GamepadButton.DPadRight)) dir = NavigationDirection.Next;
        if (pad.WasPressed(GamepadButton.DPadUp) || pad.WasPressed(GamepadButton.DPadLeft)) dir = NavigationDirection.Previous;
        if (dir is { } d && Current is not null)
        {
            // Focus order = visual order of enabled, visible focusable controls on the screen.
            var items = Current.GetVisualDescendantsOfType<InputElement>()
                .Where(c => c.Focusable && c.IsEffectivelyEnabled && c.IsEffectivelyVisible && c is not Screen)
                .ToList();
            if (items.Count > 0)
            {
                var index = focused is null ? -1 : items.IndexOf(focused);
                index = d == NavigationDirection.Next ? (index + 1) % items.Count : (index - 1 + items.Count) % items.Count;
                items[index].Focus(NavigationMethod.Directional);
            }
        }
        if (pad.WasPressed(GamepadButton.South) && focused is Button button) button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (pad.WasPressed(GamepadButton.East)) Current?.Back();
    }

    /// <summary>Saves a PNG of the whole UI, 3D included.</summary>
    public void Capture(string path)
    {
        var size = new PixelSize(Math.Max(1, (int)Bounds.Width), Math.Max(1, (int)Bounds.Height));
        using var bitmap = new RenderTargetBitmap(size, new Vector(96, 96));
        bitmap.Render(this);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        bitmap.Save(path);
    }

    public void Dispose()
    {
        timer.Stop();
        Current?.Leave();
        Stage.Dispose();
        Context.Dispose();
    }
}

internal static class VisualExtensions
{
    public static IEnumerable<T> GetVisualDescendantsOfType<T>(this Visual root) where T : Visual
    {
        foreach (var child in Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(root))
            if (child is T t) yield return t;
    }
}
