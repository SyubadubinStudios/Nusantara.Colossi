// Nusantara Colossi — AudioForge
// Synthesises every music loop, ambience bed and sound effect the game uses, deterministically.
// Gamelan voices are modelled from their inharmonic partials (saron, bonang, gong ageng, kendang),
// tuned to an approximate pelog scale. Output: 32 kHz mono 16-bit WAV in assets/audio.
using System.Text;

const int Rate = 32000;
var output = args.Length > 0 ? args[0] : FindAssets();
Directory.CreateDirectory(output);
var rng = new Random(2045);
var written = 0;
var lpState = 0f;

void Save(string name, float[] samples, float peak = .89f)
{
    Normalize(samples, peak);
    WriteWav(Path.Combine(output, name + ".wav"), samples);
    written++;
    Console.WriteLine($"  {name,-18} {samples.Length / (float)Rate,6:0.0} s");
}

Console.WriteLine($"AudioForge → {output}");

// ------------------------------------------------------------------ tuning
// Pelog-like scale (Hz) around D3; slightly "stretched" like a real gamelan set.
float[] pelog = [146.8f, 157.3f, 175.6f, 207.1f, 220.4f, 233.8f, 262.7f];
float Pelog(int degree)
{
    var octave = (int)MathF.Floor(degree / 7f);
    var d = ((degree % 7) + 7) % 7;
    return pelog[d] * MathF.Pow(2.01f, octave);
}

// ------------------------------------------------------------------ instruments
void Saron(float[] buf, float t, float freq, float gain, float decay = 1.6f)
{
    // Bronze bar: partials at ~1, 2.76, 5.4, 8.9 with faster upper decay.
    (float ratio, float amp, float dk)[] partials = [(1, 1, 1), (2.76f, .45f, 2.2f), (5.4f, .2f, 3.5f), (8.93f, .08f, 5)];
    Add(buf, t, decay * 2.2f, (x) =>
    {
        float s = 0;
        foreach (var (r, a, dk) in partials) s += a * MathF.Sin(MathF.Tau * freq * r * x) * MathF.Exp(-x * dk / decay * 2.2f);
        return s * Attack(x, .002f) * gain;
    });
}

void Bonang(float[] buf, float t, float freq, float gain)
{
    // Kettle gong: strong fundamental + 1.5 and 2.0 modes, slight beating.
    Add(buf, t, 2.4f, (x) =>
    {
        var env = MathF.Exp(-x * 1.9f) * Attack(x, .003f);
        var s = MathF.Sin(MathF.Tau * freq * x) + .5f * MathF.Sin(MathF.Tau * freq * 1.502f * x) * MathF.Exp(-x * 2) + .3f * MathF.Sin(MathF.Tau * freq * 2.003f * x + 1);
        s *= 1 + .15f * MathF.Sin(MathF.Tau * 3.1f * x);
        return s * env * gain;
    });
}

void Gong(float[] buf, float t, float freq, float gain, float length = 7)
{
    // Gong ageng: low fundamental with slow amplitude "ombak" (beating) and a soft mallet attack.
    Add(buf, t, length, (x) =>
    {
        var env = MathF.Exp(-x * 4.5f / length) * Attack(x, .03f);
        var ombak = 1 + .35f * MathF.Sin(MathF.Tau * 3.4f * x);
        var s = MathF.Sin(MathF.Tau * freq * x) * ombak + .5f * MathF.Sin(MathF.Tau * freq * 2.01f * x) * MathF.Exp(-x * .8f)
              + .25f * MathF.Sin(MathF.Tau * freq * 2.98f * x) * MathF.Exp(-x * 1.5f) + .15f * MathF.Sin(MathF.Tau * freq * .5f * x);
        return s * env * gain;
    });
}

void Kendang(float[] buf, float t, bool low, float gain)
{
    // Hand drum: pitch-swept membrane + slap noise.
    var f0 = low ? 95f : 210f;
    var noise = new Random((int)(t * 1000));
    Add(buf, t, .5f, (x) =>
    {
        var f = f0 * (1 + 1.6f * MathF.Exp(-x * 40));
        var body = MathF.Sin(MathF.Tau * f * x) * MathF.Exp(-x * (low ? 9 : 16));
        var slap = (noise.NextSingle() * 2 - 1) * MathF.Exp(-x * 70) * .5f;
        return (body + slap) * gain;
    });
}

void Taiko(float[] buf, float t, float gain)
{
    var noise = new Random((int)(t * 977));
    Add(buf, t, 1.4f, (x) =>
    {
        var f = 52 * (1 + .9f * MathF.Exp(-x * 25));
        var body = MathF.Sin(MathF.Tau * f * x) * MathF.Exp(-x * 3.4f);
        var hit = (noise.NextSingle() * 2 - 1) * MathF.Exp(-x * 45) * .6f;
        return (body * 1.2f + hit) * gain;
    });
}

void Drone(float[] buf, float t, float length, float freq, float gain, float brightness = .3f)
{
    // Rebab-like bowed drone: detuned saws through a moving low-pass.
    var lp = 0f;
    Add(buf, t, length, (x) =>
    {
        var vib = 1 + .004f * MathF.Sin(MathF.Tau * 5.2f * x);
        var s = Saw(freq * vib * x) + Saw(freq * 1.004f * x) * .8f + Saw(freq * .5f * x) * .5f;
        var cutoff = brightness * (.6f + .4f * MathF.Sin(MathF.Tau * .07f * x));
        lp += (s - lp) * cutoff * .08f;
        var env = MathF.Min(1, x / 2.5f) * MathF.Min(1, (length - x) / 2.5f);
        return lp * env * gain;
    });
}

void Choir(float[] buf, float t, float length, float freq, float gain)
{
    // Breathy "aah" pad: formant-ish sum of harmonics near 700 and 1150 Hz.
    Add(buf, t, length, (x) =>
    {
        float s = 0;
        for (var h = 1; h <= 14; h++)
        {
            var hf = freq * h;
            var formant = MathF.Exp(-MathF.Pow((hf - 700) / 260, 2)) + .6f * MathF.Exp(-MathF.Pow((hf - 1150) / 300, 2)) + .3f / h;
            s += MathF.Sin(MathF.Tau * hf * x * (1 + .003f * MathF.Sin(x * 4.7f + h))) * formant;
        }
        var env = MathF.Min(1, x / 3f) * MathF.Min(1, (length - x) / 3f);
        return s * env * gain * .25f;
    });
}

// ------------------------------------------------------------------ music
Console.WriteLine("Music");
{
    // Title: slow, ceremonial. Gong every cycle, saron balungan, choir swell.
    var len = 48f;
    var b = new float[(int)(Rate * len)];
    var beat = 60f / 54;
    int[] balungan = [2, 1, 2, 4, 5, 4, 2, 1, 6, 5, 4, 2, 1, 2, 4, 5];
    for (var i = 0; i < 40; i++)
    {
        var t = i * beat;
        if (t > len - 2) break;
        Saron(b, t, Pelog(balungan[i % balungan.Length]), .32f, 2.2f);
        if (i % 2 == 1) Bonang(b, t + beat * .5f, Pelog(balungan[(i + 3) % balungan.Length] + 7), .12f);
        if (i % 8 == 0) Gong(b, t, 49, .9f, 9);
    }
    Drone(b, 0, len, 73.4f, .18f);
    Choir(b, 8, 30, 146.8f, .28f);
    Choir(b, 14, 26, 220.4f, .2f);
    Save("music-title", Reverb(b, .55f));
}
{
    // Exploration: sparse, airy, the wind between ruins.
    var len = 56f;
    var b = new float[(int)(Rate * len)];
    var beat = 60f / 46;
    int[] notes = [4, -1, 5, -1, 4, 2, -1, -1, 1, -1, 2, 4, -1, 5, 6, -1];
    for (var i = 0; i * beat < len - 3; i++)
    {
        var n = notes[i % notes.Length];
        if (n >= 0) Saron(b, i * beat, Pelog(n + 7), .2f, 2.6f);
        if (i % 16 == 0) Gong(b, i * beat, 55, .55f, 8);
    }
    Drone(b, 0, len, 73.4f, .12f, .18f);
    Drone(b, 0, len, 110.2f, .06f, .12f);
    Save("music-explore", Reverb(b, .65f));
}
{
    // Battle: 132 bpm kendang + taiko, bonang ostinato, low brass drone, gong on the cycle.
    var len = 43.6f;
    var b = new float[(int)(Rate * len)];
    var beat = 60f / 132;
    int[] ost = [0, 2, 1, 2, 4, 2, 1, 2];
    for (var i = 0; i * beat < len - .5f; i++)
    {
        var t = i * beat;
        Bonang(b, t, Pelog(ost[i % ost.Length] + 7), .16f);
        if (i % 2 == 0) Kendang(b, t, true, .5f);
        if (i % 4 == 3) Kendang(b, t + beat * .5f, false, .35f);
        if (i % 4 == 0) Taiko(b, t, .7f);
        if (i % 16 == 0) Gong(b, t, 46, .8f, 6);
        if (i % 2 == 1) Saron(b, t, Pelog(ost[(i + 2) % ost.Length]), .22f, 1);
    }
    Drone(b, 0, len, 55, .3f, .35f);
    Drone(b, 0, len, 82.4f, .18f, .4f);
    Save("music-battle", Reverb(b, .35f));
}
{
    // Rage layer: double-time kendang, dissonant upper bonang, heartbeat sub.
    var len = 43.6f;
    var b = new float[(int)(Rate * len)];
    var beat = 60f / 132 / 2;
    for (var i = 0; i * beat < len - .5f; i++)
    {
        var t = i * beat;
        if (i % 2 == 0) Kendang(b, t, i % 4 == 0, .45f);
        if (i % 3 == 0) Bonang(b, t, Pelog(14 + (i % 5 == 0 ? 1 : 0)), .1f);
        if (i % 8 == 0) Taiko(b, t, .8f);
    }
    Drone(b, 0, len, 58.3f, .35f, .5f);
    Save("music-rage", Reverb(b, .3f));
}
{
    // Victory: gong, rising saron line, choir resolution.
    var len = 16f;
    var b = new float[(int)(Rate * len)];
    Gong(b, 0, 49, 1, 10);
    int[] rise = [0, 1, 2, 4, 5, 7, 9, 11, 12];
    for (var i = 0; i < rise.Length; i++) Saron(b, .8f + i * .45f, Pelog(rise[i] + 7), .3f, 2.4f);
    Choir(b, 2, 13, 146.8f, .35f);
    Choir(b, 2.5f, 12.5f, 220.4f, .25f);
    Save("music-victory", Reverb(b, .6f));
}

// ------------------------------------------------------------------ ambience
Console.WriteLine("Ambience");
Save("amb-wind", Loopable(FilteredNoise(20, .004f, .02f, 0.3f), 1.5f), .5f);
Save("amb-rain", Loopable(FilteredNoise(20, .25f, .6f, 0.04f), 1), .45f);
Save("amb-sea", Loopable(Waves(24), 2), .55f);
Save("amb-jungle", Loopable(Jungle(24), 1.5f), .45f);
Save("amb-city", Loopable(City(24), 1.5f), .45f);
Save("amb-fire", Loopable(Crackle(16), 1), .5f);

// ------------------------------------------------------------------ sfx
Console.WriteLine("Effects");
Save("step", Shot(.18f, x => Noise() * MathF.Exp(-x * 45) * .8f + MathF.Sin(MathF.Tau * 90 * x) * MathF.Exp(-x * 30)));
Save("climb", Shot(.22f, x => Noise() * MathF.Exp(-x * 25) * (1 + MathF.Sin(x * 300)) * .5f));
Save("jump", Shot(.3f, x => Noise() * MathF.Exp(-x * 14) * .4f + MathF.Sin(MathF.Tau * (180 + 200 * x) * x) * MathF.Exp(-x * 12) * .3f));
Save("land", Shot(.4f, x => Noise() * MathF.Exp(-x * 20) * .6f + MathF.Sin(MathF.Tau * 60 * x) * MathF.Exp(-x * 12)));
Save("grab", Shot(.25f, x => Noise() * MathF.Exp(-x * 30) * .7f + MathF.Sin(MathF.Tau * 140 * x) * MathF.Exp(-x * 25) * .5f));
Save("dodge", Shot(.35f, x => Lowpass(Noise(), .2f + x) * MathF.Sin(MathF.PI * x / .35f) * .8f));
Save("splash", Shot(.9f, x => Noise() * MathF.Exp(-x * 5) * (.6f + .4f * MathF.Sin(x * 90))));
Save("stab", Shot(.5f, x => Noise() * MathF.Exp(-x * 18) * .8f + MathF.Sin(MathF.Tau * 70 * x) * MathF.Exp(-x * 9) * .8f));
{
    var b = new float[Rate * 3];
    Bonang(b, 0, 440, .5f); Saron(b, 0, 660, .4f, 1.2f);
    Add(b, 0, .4f, x => Noise() * MathF.Exp(-x * 25) * .5f);
    Save("core-hit", b);
}
{
    var b = new float[Rate * 4];
    Bonang(b, 0, 523, .5f); Bonang(b, .06f, 784, .4f); Saron(b, .12f, 1046, .35f, 1.5f);
    Add(b, 0, .3f, x => Noise() * MathF.Exp(-x * 30) * .4f);
    Save("core-resonance", Reverb(b, .5f));
}
{
    var b = new float[Rate * 9];
    Gong(b, 0, 41, 1, 8); Taiko(b, 0, .9f);
    Add(b, 0, 2.5f, x => Lowpass(Noise(), .05f) * MathF.Exp(-x * 1.5f) * 3);
    Save("core-destroy", Reverb(b, .5f));
}
Save("sealed", Shot(.6f, x => (MathF.Sin(MathF.Tau * 300 * x) * MathF.Sin(MathF.Tau * 317 * x)) * MathF.Exp(-x * 7) * .7f));
Save("deflect", Shot(.7f, x => (MathF.Sin(MathF.Tau * 1200 * x) + MathF.Sin(MathF.Tau * 1830 * x) * .6f) * MathF.Exp(-x * 9) * .5f + Noise() * MathF.Exp(-x * 40) * .5f));
Save("stomp", Reverb(Shot(3.5f, x => MathF.Sin(MathF.Tau * (34 + 40 * MathF.Exp(-x * 6)) * x) * MathF.Exp(-x * 1.4f) * 1.4f + Lowpass(Noise(), .03f) * MathF.Exp(-x * 1.2f) * 4), .3f));
Save("roar", Reverb(Shot(3.2f, x =>
{
    var env = MathF.Min(1, x / .4f) * MathF.Exp(-MathF.Max(0, x - 1.6f) * 2);
    var f = 62 + 18 * MathF.Sin(x * 3) ;
    var growl = Saw(f * x) * (1 + .5f * MathF.Sin(MathF.Tau * 31 * x)) + Lowpass(Noise(), .1f) * 3;
    return growl * env;
}), .45f));
Save("rumble", Loopable(Shot(6, x => Lowpass(Noise(), .02f) * 6 * (.7f + .3f * MathF.Sin(x * 9)) + MathF.Sin(MathF.Tau * 30 * x) * .5f), 1));
Save("sweep", Shot(1.6f, x => Lowpass(Noise(), .05f + .3f * MathF.Sin(MathF.PI * x / 1.6f)) * MathF.Sin(MathF.PI * x / 1.6f) * 3));
Save("charge", Shot(3, x => (Lowpass(Noise(), .04f) * 5 + MathF.Sin(MathF.Tau * 40 * x) * .6f) * MathF.Min(1, x * 2) * MathF.Exp(-MathF.Max(0, x - 2) * 3)));
{
    var b = new float[Rate * 7];
    Gong(b, 0, 36, 1, 7); Choir(b, .5f, 6, 98, .5f);
    Save("awaken", Reverb(b, .6f));
}
Save("collapse", Reverb(Shot(7, x => Lowpass(Noise(), .025f) * 6 * MathF.Exp(-x * .45f) + MathF.Sin(MathF.Tau * 28 * x) * MathF.Exp(-x * .5f)), .5f));
Save("scan", Shot(1.6f, x => (MathF.Sin(MathF.Tau * (400 + 1400 * x) * x) * .5f + MathF.Sin(MathF.Tau * (800 + 2800 * x) * x) * .2f) * MathF.Exp(-x * 1.6f) * MathF.Min(1, x * 30)));
Save("hack", Shot(1.2f, x => MathF.Sign(MathF.Sin(MathF.Tau * (220 + 60 * MathF.Floor(x * 24 % 5)) * x)) * .35f * MathF.Exp(-x * 2) + Noise() * .15f * MathF.Exp(-x * 3)));
Save("decoy", Shot(1, x => MathF.Sin(MathF.Tau * (600 - 300 * x) * x) * MathF.Sin(MathF.Tau * 7 * x) * MathF.Exp(-x * 2) * .6f));
Save("denied", Shot(.35f, x => MathF.Sign(MathF.Sin(MathF.Tau * 140 * x)) * .3f * MathF.Exp(-x * 8)));
Save("hurt", Shot(.5f, x => Noise() * MathF.Exp(-x * 14) * .6f + MathF.Sin(MathF.Tau * 110 * x) * MathF.Exp(-x * 10) * .8f));
Save("death", Reverb(Shot(3, x => MathF.Sin(MathF.Tau * (220 - 120 * x / 3) * x) * MathF.Exp(-x * .9f) * .6f), .6f));
Save("gust", Shot(3, x => Lowpass(Noise(), .08f + .2f * MathF.Sin(MathF.PI * x / 3)) * MathF.Sin(MathF.PI * x / 3) * 4));
Save("thunder", Reverb(Shot(5, x => Lowpass(Noise(), .03f + .2f * MathF.Exp(-x * 4)) * 6 * MathF.Exp(-x * .8f) * (1 + MathF.Sin(x * 7))), .5f));
Save("heartbeat", Shot(.6f, x => MathF.Sin(MathF.Tau * 48 * x) * (MathF.Exp(-x * 14) + .7f * MathF.Exp(-MathF.Max(0, x - .22f) * 14) * (x > .22f ? 1 : 0))));
Save("ui-hover", Shot(.12f, x => MathF.Sin(MathF.Tau * 1320 * x) * MathF.Exp(-x * 40) * .25f));
{
    var b = new float[Rate];
    Saron(b, 0, Pelog(14), .25f, .5f);
    Save("ui-click", b, .5f);
}
{
    var b = new float[Rate * 2];
    Bonang(b, 0, Pelog(14), .3f); Bonang(b, .08f, Pelog(18), .25f);
    Save("ui-confirm", b, .6f);
}

Console.WriteLine($"Done: {written} files.");

// ------------------------------------------------------------------ DSP helpers
float Noise() => rng.NextSingle() * 2 - 1;
static float Saw(float phase) => 2 * (phase - MathF.Floor(phase + .5f));
static float Attack(float x, float time) => MathF.Min(1, x / time);

float Lowpass(float input, float k)
{
    lpState += (input - lpState) * Math.Clamp(k, 0, 1);
    return lpState;
}

static void Add(float[] buf, float t, float length, Func<float, float> voice)
{
    var start = (int)(t * Rate);
    var count = (int)(length * Rate);
    for (var i = 0; i < count && start + i < buf.Length; i++) buf[start + i] += voice(i / (float)Rate);
}

static float[] Shot(float length, Func<float, float> f)
{
    var b = new float[(int)(length * Rate)];
    for (var i = 0; i < b.Length; i++) b[i] = f(i / (float)Rate);
    // Short fade-out avoids clicks.
    var fade = Math.Min(b.Length / 4, Rate / 50);
    for (var i = 0; i < fade; i++) b[b.Length - 1 - i] *= i / (float)fade;
    return b;
}

float[] FilteredNoise(float length, float kLow, float kHigh, float wobble)
{
    var b = new float[(int)(length * Rate)];
    float lo = 0, hi = 0;
    for (var i = 0; i < b.Length; i++)
    {
        var x = i / (float)Rate;
        var n = Noise();
        var k = kLow + (kHigh - kLow) * (.5f + .5f * MathF.Sin(x * .7f) * MathF.Sin(x * .23f));
        lo += (n - lo) * k;
        hi += (lo - hi) * .5f;
        b[i] = hi * (1 + wobble * MathF.Sin(x * 1.3f));
    }
    return b;
}

float[] Waves(float length)
{
    var b = FilteredNoise(length, .01f, .08f, 0);
    for (var i = 0; i < b.Length; i++)
    {
        var x = i / (float)Rate;
        b[i] *= .3f + .7f * MathF.Pow(.5f + .5f * MathF.Sin(MathF.Tau * x / 6.5f), 3);
    }
    return b;
}

float[] Jungle(float length)
{
    var b = FilteredNoise(length, .02f, .05f, .2f);
    for (var i = 0; i < b.Length; i++) b[i] *= .4f;
    // Insects and distant birds.
    for (var c = 0; c < 26; c++)
    {
        var t = rng.NextSingle() * (length - 2);
        var f = 2200 + rng.NextSingle() * 2600;
        Add(b, t, 1.2f, x => MathF.Sin(MathF.Tau * f * x) * MathF.Max(0, MathF.Sin(MathF.Tau * 18 * x)) * MathF.Sin(MathF.PI * x / 1.2f) * .12f);
    }
    for (var c = 0; c < 8; c++)
    {
        var t = rng.NextSingle() * (length - 2);
        var f = 900 + rng.NextSingle() * 700;
        Add(b, t, .5f, x => MathF.Sin(MathF.Tau * (f + 500 * MathF.Sin(x * 30)) * x) * MathF.Sin(MathF.PI * x / .5f) * .15f);
    }
    return b;
}

float[] City(float length)
{
    var b = FilteredNoise(length, .006f, .02f, .1f);
    for (var c = 0; c < 6; c++)
    {
        var t = rng.NextSingle() * (length - 4);
        Add(b, t, 4, x => Lowpass(Noise(), .03f) * MathF.Sin(MathF.PI * x / 4) * 3);
    }
    return b;
}

float[] Crackle(float length)
{
    var b = FilteredNoise(length, .03f, .06f, 0);
    for (var i = 0; i < b.Length; i++) b[i] *= .5f;
    for (var c = 0; c < 400; c++)
    {
        var t = rng.NextSingle() * (length - .1f);
        var g = rng.NextSingle() * .8f;
        Add(b, t, .02f, x => Noise() * MathF.Exp(-x * 300) * g);
    }
    return b;
}

static float[] Loopable(float[] b, float crossfade)
{
    var n = (int)(crossfade * Rate);
    var result = new float[b.Length - n];
    Array.Copy(b, result, result.Length);
    for (var i = 0; i < n; i++)
    {
        var a = i / (float)n;
        result[i] = b[i] * a + b[result.Length + i] * (1 - a);
    }
    return result;
}

static float[] Reverb(float[] dry, float mix)
{
    // Schroeder reverb: four combs into two all-passes. Cheap, warm, a stone-temple space.
    int[] combs = [1557, 1617, 1491, 1422];
    int[] alls = [225, 556];
    var wet = new float[dry.Length];
    foreach (var d in combs)
    {
        var buffer = new float[d];
        var idx = 0;
        float filt = 0;
        for (var i = 0; i < dry.Length; i++)
        {
            var y = buffer[idx];
            filt = y * .8f + filt * .2f;
            buffer[idx] = dry[i] + filt * .84f;
            idx = (idx + 1) % d;
            wet[i] += y * .25f;
        }
    }
    foreach (var d in alls)
    {
        var buffer = new float[d];
        var idx = 0;
        for (var i = 0; i < wet.Length; i++)
        {
            var b = buffer[idx];
            var y = -wet[i] + b;
            buffer[idx] = wet[i] + b * .5f;
            idx = (idx + 1) % d;
            wet[i] = y;
        }
    }
    var result = new float[dry.Length];
    for (var i = 0; i < dry.Length; i++) result[i] = dry[i] * (1 - mix * .5f) + wet[i] * mix;
    return result;
}

static void Normalize(float[] b, float peak)
{
    var max = 1e-6f;
    foreach (var s in b) max = MathF.Max(max, MathF.Abs(s));
    var g = peak / max;
    for (var i = 0; i < b.Length; i++) b[i] = MathF.Tanh(b[i] * g * 1.1f) / MathF.Tanh(1.1f);
}

static void WriteWav(string path, float[] samples)
{
    using var stream = File.Create(path);
    using var w = new BinaryWriter(stream, Encoding.ASCII);
    var dataBytes = samples.Length * 2;
    w.Write("RIFF"u8.ToArray()); w.Write(36 + dataBytes); w.Write("WAVE"u8.ToArray());
    w.Write("fmt "u8.ToArray()); w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
    w.Write("data"u8.ToArray()); w.Write(dataBytes);
    foreach (var s in samples) w.Write((short)Math.Clamp(s * 32767, -32768, 32767));
}

static string FindAssets()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "NusantaraColossi.slnx"))) dir = dir.Parent;
    return Path.Combine(dir?.FullName ?? ".", "assets", "audio");
}
