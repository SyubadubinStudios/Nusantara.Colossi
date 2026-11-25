using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using NusantaraColossi.Core;
using NusantaraColossi.Game.Net;

using Palette = NusantaraColossi.Game.UI.Theme;

namespace NusantaraColossi.Game.UI.Screens;

/// <summary>
/// Co-op lobby: host picks a guardian and starts immediately; others pick a host GARUDA heard on the
/// local network (one click) or join by address.
/// </summary>
public sealed class CoopScreen : Screen
{
    private readonly TextBox name, address, port;
    private readonly ComboBox guardian;
    private readonly CheckBox legend;
    private readonly TextBlock status;
    private readonly StackPanel lanList = new() { Spacing = 6 };
    private readonly TextBlock lanHeader;
    private LanBrowser? browser;
    private CoopSession? pending;
    private float timeout;
    private float scanClock;

    public CoopScreen(Shell shell) : base(shell)
    {
        var s = Ctx.Settings;
        name = Field(Ctx.Profile.PlayerName);
        address = Field(s.CoopAddress);
        port = Field(s.CoopPort.ToString());
        port.Width = 110;
        guardian = new ComboBox { Width = 320, ItemsSource = Colossi.All.Select(c => c.Name.Get()).ToList(), SelectedIndex = Math.Max(0, Array.FindIndex(Colossi.All, c => !Ctx.Profile.IsDefeated(c.Id))) };
        legend = new CheckBox { Content = Palette.Text(Loc.T("map.legend"), 14, Palette.Kapur) };
        status = Palette.Mono("", 12, Palette.Nadi);
        status.TextWrapping = TextWrapping.Wrap;

        var root = new StackPanel { Margin = new Thickness(Ctx.IsMobile ? 24 : 56, 40, 24, 32), MaxWidth = 900, HorizontalAlignment = HorizontalAlignment.Left };
        root.Children.Add(Palette.Eyebrow(Loc.T("coop.eyebrow"), Palette.Nadi));
        root.Children.Add(Palette.Title(Loc.T("coop.title"), 38));
        root.Children.Add(Palette.Rule(Palette.Prada, 80).Also(r => { r.Margin = new Thickness(0, 12, 0, 18); r.HorizontalAlignment = HorizontalAlignment.Left; }));
        root.Children.Add(Palette.Text(Loc.T("coop.help"), 14, Palette.KapurMuted).Also(t => { t.TextWrapping = TextWrapping.Wrap; t.LineHeight = 21; t.MaxWidth = 640; t.HorizontalAlignment = HorizontalAlignment.Left; }));
        root.Children.Add(Labeled(Loc.T("coop.name"), name).Also(c => c.Margin = new Thickness(0, 20, 0, 0)));

        // GARUDA's LAN sweep: hosts heard on the local network, one click to join.
        lanHeader = Palette.Mono("", 11, Palette.Nadi);
        var lan = new StackPanel { Spacing = 10, Margin = new Thickness(0, 24, 0, 0) };
        lan.Children.Add(lanHeader);
        lan.Children.Add(new Border
        {
            BorderBrush = Palette.Hairline,
            BorderThickness = new Thickness(1, 0, 0, 0),
            Padding = new Thickness(14, 2, 0, 2),
            Child = lanList,
            MaxWidth = 720,
            HorizontalAlignment = HorizontalAlignment.Left,
        });
        root.Children.Add(lan);

        var columns = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), Margin = new Thickness(0, 24, 0, 0) };
        var host = Column(Loc.T("coop.host"));
        host.Children.Add(Labeled(Loc.T("coop.region"), guardian));
        host.Children.Add(legend);
        host.Children.Add(Palette.Primary(Loc.T("coop.host"), Host).Also(b => b.HorizontalAlignment = HorizontalAlignment.Left));
        var join = Column(Loc.T("coop.joinManual"));
        var addr = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        addr.Children.Add(Labeled(Loc.T("coop.address"), address));
        addr.Children.Add(Labeled(Loc.T("coop.port"), port));
        join.Children.Add(addr);
        join.Children.Add(Palette.Primary(Loc.T("coop.join"), Join).Also(b => b.HorizontalAlignment = HorizontalAlignment.Left));
        Grid.SetColumn(join, 1);
        columns.Children.Add(host);
        columns.Children.Add(join);
        root.Children.Add(columns);
        root.Children.Add(status.Also(t => t.Margin = new Thickness(0, 20, 0, 0)));
        root.Children.Add(Palette.Secondary("← " + Loc.T("menu.back"), () => Back()).Also(b => { b.Margin = new Thickness(0, 24, 0, 0); b.HorizontalAlignment = HorizontalAlignment.Left; }));
        Content = new Border { Background = Palette.Scrim, Child = new ScrollViewer { Content = root } };

        browser = new LanBrowser();
        RebuildLan();
    }

    private static TextBox Field(string text) => new()
    {
        Text = text,
        Width = 260,
        FontFamily = Palette.Telemetry,
        FontSize = 14,
        Background = Palette.AndesiteRaised,
        Foreground = Palette.Kapur,
        BorderBrush = Palette.Hairline,
    };

    private static StackPanel Column(string title)
    {
        var s = new StackPanel { Spacing = 12, Margin = new Thickness(0, 0, 24, 0) };
        s.Children.Add(Palette.Mono(title.ToUpperInvariant(), 11, Palette.Prada));
        return s;
    }

    private static StackPanel Labeled(string label, Control control)
    {
        var s = new StackPanel { Spacing = 4 };
        s.Children.Add(Palette.Mono(label.ToUpperInvariant(), 10.5, Palette.KapurMuted));
        s.Children.Add(control);
        return s;
    }

    // ---------------------------------------------------------------- LAN list

    private void RebuildLan()
    {
        lanList.Children.Clear();
        var sessions = browser?.Sessions ?? [];
        if (browser is not { IsListening: true })
        {
            lanList.Children.Add(Palette.Text(Loc.T("coop.lanOff"), 13, Palette.KapurMuted).Also(t => { t.TextWrapping = TextWrapping.Wrap; t.HorizontalAlignment = HorizontalAlignment.Left; }));
            return;
        }
        if (sessions.Count == 0)
        {
            lanList.Children.Add(Palette.Text(Loc.T("coop.lanEmpty"), 13, Palette.KapurMuted).Also(t => { t.TextWrapping = TextWrapping.Wrap; t.HorizontalAlignment = HorizontalAlignment.Left; }));
            return;
        }
        foreach (var found in sessions) lanList.Children.Add(LanRow(found));
    }

    private Button LanRow(LanBrowser.Found found)
    {
        var b = found.Beacon;
        var bp = Colossi.All.FirstOrDefault(c => c.Id == b.ColossusId);
        var region = bp is null ? null : Regions.All.FirstOrDefault(r => r.ColossusId == bp.Id);
        var full = b.Players >= b.MaxPlayers;

        var rule = new Border { Width = 0, Height = 2, Background = Palette.Prada, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
        var title = new TextBlock { Text = (bp?.Name.Get() ?? b.ColossusId).ToUpperInvariant(), FontFamily = Palette.Inscription, FontSize = 17, LetterSpacing = 2, Foreground = full ? Palette.KapurMuted : Palette.Kapur };
        var place = Palette.Text((region?.Name.Get() ?? "") + (b.Legend ? " · " + Loc.T("map.legend") : ""), 12.5, b.Legend ? Palette.Bara : Palette.KapurMuted);
        var left = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        left.Children.Add(title);
        left.Children.Add(place);

        var telemetry = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(24, 0, 0, 0) };
        telemetry.Children.Add(Palette.Mono($"{b.HostName.ToUpperInvariant()}  ·  {b.Players}/{b.MaxPlayers}", 11, full ? Palette.Bara : Palette.Nadi).Also(t => t.HorizontalAlignment = HorizontalAlignment.Right));
        telemetry.Children.Add(Palette.Mono($"{found.Address}:{b.GamePort}", 10.5, Palette.KapurMuted).Also(t => t.HorizontalAlignment = HorizontalAlignment.Right));

        var action = Palette.Mono(full ? Loc.T("coop.lanFull").ToUpperInvariant() : Loc.T("coop.join").ToUpperInvariant() + " →", 11, full ? Palette.Bara : Palette.Prada);
        action.VerticalAlignment = VerticalAlignment.Center;
        action.Margin = new Thickness(20, 0, 0, 0);

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto") };
        row.Children.Add(rule);
        Grid.SetColumn(left, 1);
        row.Children.Add(left);
        Grid.SetColumn(telemetry, 2);
        row.Children.Add(telemetry);
        Grid.SetColumn(action, 3);
        row.Children.Add(action);

        var button = new Button
        {
            Content = row,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0, 6),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            IsEnabled = !full,
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
        };
        button.Classes.Add("menu");
        void Highlight(bool on)
        {
            rule.Width = on ? 22 : 0;
            title.Foreground = on ? Palette.PradaBright : full ? Palette.KapurMuted : Palette.Kapur;
        }
        button.PointerEntered += (_, _) => { Highlight(true); Palette.Sound?.Invoke("ui-hover"); };
        button.PointerExited += (_, _) => Highlight(button.IsFocused);
        button.GotFocus += (_, _) => Highlight(true);
        button.LostFocus += (_, _) => Highlight(false);
        var target = found.Address.ToString();
        var gamePort = b.GamePort;
        button.Click += (_, _) =>
        {
            Palette.Sound?.Invoke("ui-confirm");
            address.Text = target;
            port.Text = gamePort.ToString();
            Join();
        };
        return button;
    }

    private void UpdateLanHeader()
    {
        var dots = new string('·', 1 + (int)(scanClock * 2.5f) % 3);
        var count = browser?.Sessions.Count ?? 0;
        lanHeader.Text = browser is { IsListening: true }
            ? $"GARUDA//LAN  {Loc.T("coop.lan").ToUpperInvariant()}  {dots}  {count}"
            : $"GARUDA//LAN  {Loc.T("coop.lan").ToUpperInvariant()}  ✕";
    }

    // ---------------------------------------------------------------- host / join

    private string HunterName() => string.IsNullOrWhiteSpace(name.Text) ? "Arka" : name.Text.Trim()[..Math.Min(24, name.Text.Trim().Length)];

    private int Port() => int.TryParse(port.Text, out var p) && p is > 1024 and < 65536 && p != Coop.DiscoveryPort ? p : Coop.DefaultGamePort;

    private void Remember()
    {
        Ctx.Profile.PlayerName = HunterName();
        Ctx.Settings.CoopAddress = address.Text?.Trim() ?? "127.0.0.1";
        Ctx.Settings.CoopPort = Port();
        Ctx.SaveSettings();
        Ctx.SaveProfile();
    }

    private void Host()
    {
        Remember();
        var bp = Colossi.All[Math.Max(0, guardian.SelectedIndex)];
        var options = new EncounterOptions { Legend = legend.IsChecked == true, Abilities = DroneAbilities.All, Seed = Environment.TickCount, IsAuthority = true };
        try
        {
            var session = CoopSession.Host(Port(), HunterName());
            session.WelcomeFor = slot => new Coop.Welcome(slot, bp.Id, options.Legend, options.Abilities, options.Seed);
            StopBrowsing();
            Shell.FadeThrough(() => Shell.Navigate(HuntScreen.Coop(Shell, session, Regions.ForColossus(bp.Id), bp, options, HunterName())));
        }
        catch (Exception ex) when (ex is ThreeNet.ThreeNetException or System.Net.Sockets.SocketException or InvalidOperationException)
        {
            status.Text = Loc.F("coop.failed", ex.Message);
        }
    }

    private void Join()
    {
        Remember();
        pending?.Dispose();
        pending = null;
        var host = Ctx.Settings.CoopAddress;
        status.Text = Loc.F("coop.connecting", host, Port());
        try
        {
            var session = CoopSession.Join(host, Port(), HunterName());
            pending = session;
            timeout = 8;
            session.Status += text => { if (pending == session) status.Text = text; };
            session.Closed += reason => { if (pending == session) { status.Text = Loc.F("coop.failed", reason); pending = null; session.Dispose(); } };
            session.Accepted += welcome =>
            {
                if (pending != session) return;
                pending = null;
                if (!Colossi.All.Any(c => c.Id == welcome.ColossusId))
                {
                    status.Text = Loc.F("coop.failed", welcome.ColossusId);
                    session.Dispose();
                    return;
                }
                StopBrowsing();
                var bp = Colossi.Get(welcome.ColossusId);
                var options = new EncounterOptions { Legend = welcome.Legend, Abilities = welcome.Abilities, Seed = welcome.Seed, IsAuthority = false };
                Shell.FadeThrough(() => Shell.Navigate(HuntScreen.Coop(Shell, session, Regions.ForColossus(bp.Id), bp, options, HunterName())));
            };
        }
        catch (Exception ex) when (ex is ThreeNet.ThreeNetException or System.Net.Sockets.SocketException or ArgumentException)
        {
            status.Text = Loc.F("coop.failed", ex.Message);
        }
    }

    private void StopBrowsing()
    {
        browser?.Dispose();
        browser = null;
    }

    public override void Tick(float dt)
    {
        base.Tick(dt);
        scanClock += dt;
        if (browser is not null && browser.Poll(dt)) RebuildLan();
        UpdateLanHeader();

        if (pending is null) return;
        pending.Poll();
        timeout -= dt;
        if (timeout <= 0 && pending is not null)
        {
            status.Text = Loc.F("coop.failed", "timeout");
            pending.Dispose();
            pending = null;
        }
    }

    public override void Leave()
    {
        // A session handed to the hunt screen is no longer pending.
        pending?.Dispose();
        pending = null;
        StopBrowsing();
    }
}
