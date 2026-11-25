using System.Numerics;
using NusantaraColossi.Core;
using ThreeNet;

namespace NusantaraColossi.Game.Audio;

public enum MusicMood { Silent, Title, Explore, Battle, Victory }

/// <summary>
/// Adaptive score and spatial effects on top of ThreeNet's AudioEngine.
/// Music is three always-running layers (explore, battle, rage) whose gains crossfade with the
/// fight's intensity, so transitions never restart a loop. The heartbeat cue is phase-locked to
/// the colossus core pulse; it is the audible "now" for a resonance stab.
/// Missing device or files degrade to silence and never throw into the game loop.
/// </summary>
public sealed class GameAudio : IDisposable
{
    private readonly AudioEngine? engine;
    private readonly Dictionary<string, AudioClip?> clips = new(StringComparer.Ordinal);
    private readonly GameSettings settings;

    private SoundInstance? explore, battle, rage, title, ambience, rain, rumble;
    private float exploreGain, battleGain, rageGain, titleGain, rumbleGain;
    private MusicMood mood = MusicMood.Silent;
    private float lastPulse;
    private float stepCooldown;

    /// <summary>Raised when a voice line should be subtitled (localisation key).</summary>
    public event Action<string>? Voice;

    public bool IsAvailable => engine is not null;

    /// <summary>
    /// Set by a host whose platform ThreeNet cannot open a device on (Android: the native output aborts
    /// because no NDK context is registered). GameAudio then mixes into an offline engine at
    /// <see cref="OfflineSampleRate"/> and hands it to this sink, which streams <see cref="AudioEngine.Render"/>
    /// output to the platform's own audio API. The sink returns an IDisposable that stops the stream.
    /// </summary>
    public static Func<AudioEngine, IDisposable>? OfflineSink { get; set; }
    public const int OfflineSampleRate = 48000;
    private readonly IDisposable? offlineStream;

    public GameAudio(GameSettings settings)
    {
        this.settings = settings;
        try
        {
            if (OfflineSink is { } sink)
            {
                engine = AudioEngine.CreateOffline(OfflineSampleRate);
                offlineStream = sink(engine);
            }
            else engine = AudioEngine.Open();
            engine.MasterGain = settings.Volume;
        }
        catch (Exception)
        {
            engine = null; // No audio device (CI, headless, some emulators).
        }
    }

    private AudioClip? Clip(string name)
    {
        if (engine is null) return null;
        if (clips.TryGetValue(name, out var clip)) return clip;
        var path = AssetPaths.Audio(name);
        try { clip = File.Exists(path) ? engine.LoadClip(path) : null; }
        catch (Exception) { clip = null; }
        clips[name] = clip;
        return clip;
    }

    /// <summary>Loads everything up front so the first stomp never hitches on disk I/O.</summary>
    public void Preload()
    {
        foreach (var name in (string[])["music-explore", "music-battle", "music-rage", "music-victory", "stomp", "roar", "core-hit", "core-resonance", "core-destroy", "stab", "heartbeat", "step", "climb", "grab", "land", "jump", "hurt", "scan"])
            Clip(name);
    }

    public void ApplySettings()
    {
        if (engine is not null) engine.MasterGain = settings.Volume;
    }

    public SoundInstance? Play(string name, float gain = 1, float pitch = 1)
    {
        var clip = Clip(name);
        if (clip is null || engine is null) return null;
        var o = SoundOptions.Flat(gain);
        o.Pitch = pitch;
        return engine.Play(clip, o);
    }

    public SoundInstance? PlayAt(string name, Vector3 position, float gain = 1, float pitch = 1, float minDistance = 4, float maxDistance = 400)
    {
        var clip = Clip(name);
        if (clip is null || engine is null) return null;
        var o = SoundOptions.At(position, gain);
        o.Pitch = pitch;
        o.MinDistance = minDistance;
        o.MaxDistance = maxDistance;
        return engine.Play(clip, o);
    }

    private SoundInstance? Loop(string name, float gain)
    {
        var clip = Clip(name);
        if (clip is null || engine is null) return null;
        var o = SoundOptions.Flat(gain, loop: true);
        return engine.Play(clip, o);
    }

    private static void SetGain(SoundInstance? s, float gain)
    {
        if (s is null) return;
        var o = s.Options;
        if (MathF.Abs(o.Gain - gain) < .002f) return;
        o.Gain = gain;
        s.Options = o;
    }

    /// <summary>Title / map screens.</summary>
    public void StartMenu()
    {
        StopWorld();
        mood = MusicMood.Title;
        title ??= Loop("music-title", 0);
    }

    /// <summary>Starts the layered score and the biome ambience for a hunt.</summary>
    public void StartHunt(RegionDefinition region)
    {
        StopWorld();
        mood = MusicMood.Explore;
        explore = Loop("music-explore", 0);
        battle = Loop("music-battle", 0);
        rage = Loop("music-rage", 0);
        rumble = Loop("rumble", 0);
        ambience = Loop(region.Biome switch
        {
            Biome.City => "amb-city",
            Biome.Rainforest or Biome.Hills or Biome.Terraces => "amb-jungle",
            Biome.Lagoon or Biome.Sea => "amb-sea",
            Biome.Crater => "amb-fire",
            _ => "amb-wind",
        }, .35f);
        if (region.Weather is Weather.Rain or Weather.Storm) rain = Loop("amb-rain", .4f);
        exploreGain = battleGain = rageGain = rumbleGain = 0;
    }

    public void Victory()
    {
        mood = MusicMood.Victory;
        Play("music-victory", .9f * settings.MusicVolume);
    }

    private void StopWorld()
    {
        foreach (var s in (SoundInstance?[])[explore, battle, rage, ambience, rain, rumble]) s?.Stop();
        explore = battle = rage = ambience = rain = rumble = null;
    }

    public void StopAll()
    {
        StopWorld();
        title?.Stop();
        title = null;
        mood = MusicMood.Silent;
    }

    /// <summary>Per-frame mix. <paramref name="listener"/> is the camera node.</summary>
    public void Update(float dt, Encounter? encounter, int localSlot, Scene? scene, Node? listener)
    {
        if (engine is null) return;
        if (scene is not null && listener is not null) engine.Update(scene, listener, dt);
        stepCooldown -= dt;

        var music = settings.MusicVolume;
        titleGain = Mathx.Damp(titleGain, mood == MusicMood.Title ? .85f : 0, 1.2f, dt);
        SetGain(title, titleGain * music);
        if (encounter is null)
        {
            SetGain(explore, 0); SetGain(battle, 0); SetGain(rage, 0);
            return;
        }

        var c = encounter.Colossus;
        var player = localSlot < encounter.Players.Count ? encounter.Players[localSlot] : null;
        // Intensity: distance to the colossus, whether it is hunting, whether we are on it.
        float intensity = 0;
        if (c.State != ColossusState.Dormant && !c.IsDefeated && player is not null)
        {
            var d = Vector3.Distance(Mathx.Flat(player.Position), Mathx.Flat(c.Pose.Position));
            intensity = Mathx.Saturate(1.4f - d / (c.Blueprint.Height * 2.5f + 30));
            if (player.Mode == PlayerMode.Attached) intensity = 1;
            if (c.State is ColossusState.Pursue or ColossusState.Attack or ColossusState.ShakeWarn or ColossusState.ShakeOff) intensity = MathF.Max(intensity, .8f);
        }
        var victory = mood == MusicMood.Victory || encounter.Phase != EncounterPhase.Fight;
        exploreGain = Mathx.Damp(exploreGain, victory ? 0 : (1 - intensity) * .75f, 1.5f, dt);
        battleGain = Mathx.Damp(battleGain, victory ? 0 : intensity * .9f, 2.5f, dt);
        rageGain = Mathx.Damp(rageGain, victory ? 0 : intensity * MathF.Min(1, c.Rage * .5f) * .8f, 2, dt);
        SetGain(explore, exploreGain * music);
        SetGain(battle, battleGain * music);
        SetGain(rage, rageGain * music);

        // Colossus footsteps as a low rumble while it moves.
        var moving = !c.IsDefeated && c.State != ColossusState.Dormant ? c.Pose.GaitWeight : 0;
        rumbleGain = Mathx.Damp(rumbleGain, moving * .45f + c.ShakeIntensity * .4f, 3, dt);
        SetGain(rumble, rumbleGain);

        // Heartbeat on the pulse peak, louder the closer you are to an open core.
        if (!c.IsDefeated && player is not null && encounter.Phase == EncounterPhase.Fight)
        {
            var pulse = c.CorePulse(encounter.Elapsed);
            if (pulse > .5f && lastPulse <= .5f)
            {
                var near = player.Mode == PlayerMode.Attached ? 1 : intensity * .5f;
                if (near > .05f) Play("heartbeat", .25f + .65f * near, 1 + .08f * c.Rage);
            }
            lastPulse = pulse;
        }
    }

    /// <summary>Maps simulation events to sound. Positions are world space.</summary>
    public void OnEvent(in GameEvent e, int localSlot)
    {
        if (engine is null) return;
        var mine = e.Slot == localSlot;
        switch (e.Kind)
        {
            case GameEventKind.Footstep:
                if (stepCooldown <= 0) { PlayAt("step", e.Position, mine ? .5f : .3f, .9f + Random.Shared.NextSingle() * .2f, 2, 40); stepCooldown = .12f; }
                break;
            case GameEventKind.ClimbStep: PlayAt("climb", e.Position, .45f, .9f + Random.Shared.NextSingle() * .25f, 2, 40); break;
            case GameEventKind.Jump: PlayAt("jump", e.Position, .5f); break;
            case GameEventKind.Land: PlayAt("land", e.Position, .4f + MathF.Min(.5f, e.Value * .04f)); break;
            case GameEventKind.Dodge: PlayAt("dodge", e.Position, .55f); break;
            case GameEventKind.Splash: PlayAt("splash", e.Position, .7f); break;
            case GameEventKind.Grab: PlayAt("grab", e.Position, .65f); break;
            case GameEventKind.GripLost or GameEventKind.ThrownOff: PlayAt("grab", e.Position, .5f, .7f); break;
            case GameEventKind.StaminaLow: if (mine) Play("heartbeat", .35f, 1.3f); break;
            case GameEventKind.SecondWind: if (mine) Play("ui-confirm", .5f); break;
            case GameEventKind.PlayerHurt: PlayAt("hurt", e.Position, mine ? .9f : .5f); break;
            case GameEventKind.PlayerDied: Play("death", mine ? .9f : .4f); break;
            case GameEventKind.StabFlesh: PlayAt("stab", e.Position, .8f); break;
            case GameEventKind.StabSealed: PlayAt("sealed", e.Position, .7f); break;
            case GameEventKind.StabDeflected: PlayAt("deflect", e.Position, .8f); break;
            case GameEventKind.CoreHit: PlayAt("core-hit", e.Position, .9f, 1, 8, 600); break;
            case GameEventKind.CoreResonance: PlayAt("core-resonance", e.Position, 1, 1, 8, 600); break;
            case GameEventKind.CoreDestroyed: Play("core-destroy", 1); SayLine("voice.core"); break;
            case GameEventKind.Awaken: Play("awaken", 1); SayLine("voice.intro"); break;
            case GameEventKind.ShakeWarn: PlayAt("charge", e.Position, .5f, 1.3f, 10, 600); break;
            case GameEventKind.ShakeStart: PlayAt("roar", e.Position, .6f, 1.25f, 10, 600); break;
            case GameEventKind.StompWindup: PlayAt("charge", e.Position, .45f, .8f, 10, 600); break;
            case GameEventKind.StompImpact: PlayAt("stomp", e.Position, 1, .95f + Random.Shared.NextSingle() * .1f, 15, 900); break;
            case GameEventKind.SweepStart: PlayAt("sweep", e.Position, .9f, 1, 10, 600); break;
            case GameEventKind.SweepHit or GameEventKind.ChargeHit: PlayAt("stomp", e.Position, .8f, 1.2f, 10, 600); break;
            case GameEventKind.RoarStart: PlayAt("roar", e.Position, 1, .9f, 20, 1200); break;
            case GameEventKind.ChargeStart: PlayAt("charge", e.Position, .9f, 1, 15, 900); break;
            case GameEventKind.Stunned: PlayAt("hack", e.Position, .6f, .7f, 10, 600); break;
            case GameEventKind.Staggered: PlayAt("stomp", e.Position, .7f, .8f, 15, 900); break;
            case GameEventKind.Rage: Play("roar", 1, .8f); SayLine("voice.enrage"); break;
            case GameEventKind.Collapse: Play("collapse", 1); break;
            case GameEventKind.Scan: if (mine) { Play("scan", .7f); SayLine("voice.scan"); } break;
            case GameEventKind.Hack: Play("hack", mine ? .8f : .4f); break;
            case GameEventKind.Decoy: Play("decoy", mine ? .7f : .4f); break;
            case GameEventKind.ScanDenied or GameEventKind.HackDenied or GameEventKind.DecoyDenied: if (mine) Play("denied", .5f); break;
            case GameEventKind.GustWarn: Play("gust", .35f, .8f); break;
            case GameEventKind.Gust: Play("gust", .8f); break;
            case GameEventKind.Victory: Victory(); SayLine("voice.victory"); break;
            case GameEventKind.FloodWarn: Play("rumble", .7f, .7f); SayLine("voice.flood"); break;
            case GameEventKind.FloodRise: Play("amb-sea", .6f, .9f); break;
            case GameEventKind.DiveWarn: PlayAt("roar", e.Position, .8f, .7f, 20, 900); SayLine("voice.dive"); break;
            case GameEventKind.DiveStart or GameEventKind.Surface: PlayAt("splash", e.Position, 1, .5f, 20, 900); break;
            case GameEventKind.Volley:
                if ((StrikeKind)(int)e.Value == StrikeKind.Cannonball) PlayAt("thunder", e.Position, 1, 1.6f, 20, 1200);
                else if ((StrikeKind)(int)e.Value == StrikeKind.Shard) PlayAt("deflect", e.Position, 1, .6f, 20, 900);
                else Play("hack", .7f, .6f);
                break;
            case GameEventKind.StrikeImpact:
                var strike = (StrikeKind)(int)e.Value;
                PlayAt(strike == StrikeKind.DataBolt ? "thunder" : strike == StrikeKind.Cannonball ? "stomp" : "deflect", e.Position,
                    strike == StrikeKind.Shard ? .7f : .9f, strike == StrikeKind.DataBolt ? 1.8f : 1.3f + Random.Shared.NextSingle() * .2f, 6, 300);
                break;
            case GameEventKind.BreathLow: if (mine) { Play("heartbeat", .45f, 1.5f); SayLine("voice.breath"); } break;
            case GameEventKind.Mount: PlayAt("charge", e.Position, .5f, 1.6f); break;
            case GameEventKind.Dismount: PlayAt("land", e.Position, .5f); break;
            case GameEventKind.GliderOpen: PlayAt("gust", e.Position, .6f, 1.4f); break;
            case GameEventKind.GliderClose: PlayAt("grab", e.Position, .4f, 1.3f); break;
        }
    }

    public void Thunder(float strength) => Play("thunder", .4f + .6f * strength, .85f + Random.Shared.NextSingle() * .3f);

    private float voiceCooldown;
    private DateTime lastVoice;

    /// <summary>Plays a recorded voice line if present, and always raises the subtitle event.</summary>
    public void SayLine(string key)
    {
        var now = DateTime.UtcNow;
        if ((now - lastVoice).TotalSeconds < voiceCooldown) return;
        lastVoice = now;
        voiceCooldown = 4;
        Voice?.Invoke(key);
        if (engine is null) return;
        // Language-specific recording first (intro-en.wav), then a language-neutral one (intro.wav).
        var name = key.Replace("voice.", "");
        var path = AssetPaths.Voice(name + "-" + Loc.Language);
        if (!File.Exists(path)) path = AssetPaths.Voice(name);
        if (!File.Exists(path)) return;
        try
        {
            if (!clips.TryGetValue(path, out var clip)) clips[path] = clip = engine.LoadClip(path);
            if (clip is not null) engine.Play(clip, SoundOptions.Flat(.95f));
        }
        catch (Exception) { /* Voice is optional. */ }
    }

    public void Dispose()
    {
        StopAll();
        foreach (var clip in clips.Values) clip?.Remove();
        clips.Clear();
        offlineStream?.Dispose(); // stop the platform stream before the engine goes away
        engine?.Dispose();
    }
}
