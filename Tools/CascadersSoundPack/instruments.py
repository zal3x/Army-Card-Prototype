"""Instrument voices: drums, distorted guitar, synths, horns, choir, souls."""
import numpy as np

from dsp import (SR, n_samples, t_axis, as_array, ramp, exp_ramp, env_perc, env_adsr,
                 mtof, cents, osc_sine, osc_saw, osc_pulse, osc_tri, supersaw, additive,
                 fm, white, pink, brown, smooth_random, lp, hp, bp, eq, sweep,
                 formant_filter, oversample, saturate, pan, stereo, reverb, Mix,
                 reverse, normalize)


# ============================================================================
# DRUMS & PERCUSSION
# ============================================================================

def kick(rng, f0=150, f1=46, decay=0.42, click=0.6, punch=0.035, dur=0.9, drive=1.8):
    t = t_axis(dur)
    f = f1 + (f0 - f1) * np.exp(-t / punch)
    body = osc_sine(f, dur) * env_perc(dur, 0.0008, decay)
    clk = hp(white(dur, rng), 2500) * np.exp(-t / 0.004) * click
    return saturate(body + clk, drive)


def snare(rng, tone=190, decay=0.2, dur=0.7, snap=1.0, body=0.6, crack=3500):
    t = t_axis(dur)
    tn = (osc_sine(tone * (1 + 0.15 * np.exp(-t / 0.01)), dur)
          + 0.5 * osc_sine(tone * 1.72, dur)) * np.exp(-t / 0.07) * body
    nz = bp(white(dur, rng), 900, 9000) * np.exp(-t / decay) * snap
    cr = hp(white(dur, rng), crack) * np.exp(-t / 0.012) * 0.6
    return saturate(tn + nz + cr, 1.6)


def tom(rng, f=110, decay=0.45, dur=1.0):
    t = t_axis(dur)
    fr = f * (1 + 0.35 * np.exp(-t / 0.03))
    s = osc_sine(fr, dur) + 0.3 * osc_sine(fr * 1.5, dur) * np.exp(-t / 0.08)
    s *= env_perc(dur, 0.001, decay)
    s += lp(white(dur, rng), 2500) * np.exp(-t / 0.015) * 0.4
    return saturate(s, 1.4)


def hat(rng, open_=False, dur=None):
    dur = dur or (0.6 if open_ else 0.12)
    t = t_axis(dur)
    metal = sum(osc_pulse(f, dur, 0.5, rng.random()) for f in
                (205.3 * 2, 304.4 * 2, 369.6 * 2, 522.7 * 2, 540 * 2, 800 * 2))
    s = hp(metal * 0.3 + white(dur, rng), 7000, order=4)
    return s * np.exp(-t / (0.22 if open_ else 0.035))


def crash(rng, dur=3.0, bright=4000, decay=1.1):
    t = t_axis(dur)
    metal = sum(osc_pulse(f * rng.uniform(0.97, 1.03), dur, 0.5, rng.random())
                for f in (205.3, 304.4, 369.6, 522.7, 540, 800, 1127, 1442))
    nz = white(dur, rng)
    s = hp(metal * 0.25 + nz, bright, order=3)
    s = eq(s, "peak", 6500, 0.7, 4)
    env = np.exp(-t / decay) * (0.6 + 0.4 * np.exp(-t / 0.05))
    return stereo(s * env) * 0.5 + np.vstack([hp(white(dur, rng), bright), hp(white(dur, rng), bright)]) * env * 0.15


def ride(rng, dur=1.2):
    t = t_axis(dur)
    s = sum(osc_sine(f, dur) for f in (3150, 4733, 5810, 7370)) * 0.2
    s += hp(white(dur, rng), 6000) * 0.4
    return s * np.exp(-t / 0.35) * (0.5 + 0.5 * np.exp(-t / 0.02))


def war_drum(rng, f=58, decay=1.3, slap=0.5, dur=2.4, drive=1.5):
    """Big taiko-style membrane: circular-membrane modes + skin slap."""
    t = t_axis(dur)
    bend = 1 + 0.28 * np.exp(-t / 0.025)
    s = np.zeros_like(t)
    for ratio, amp, dk in ((1.0, 1.0, 1.0), (1.59, 0.45, 0.45), (2.14, 0.3, 0.3),
                           (2.30, 0.22, 0.25), (2.65, 0.15, 0.18), (2.92, 0.1, 0.12)):
        s += amp * osc_sine(f * ratio * bend, dur, rng.random()) * np.exp(-t / (decay * dk))
    s *= np.minimum(1, t / 0.002)
    skin = bp(white(dur, rng), 180, 2200) * np.exp(-t / 0.035) * slap
    stick = hp(white(dur, rng), 3000) * np.exp(-t / 0.004) * slap * 0.5
    return saturate(s + skin + stick, drive)


def boom(rng, f0=70, f1=28, decay=2.2, dur=4.0):
    """Cinematic sub boom."""
    t = t_axis(dur)
    f = f1 + (f0 - f1) * np.exp(-t / 0.25)
    s = osc_sine(f, dur) * env_perc(dur, 0.003, decay)
    rumble = lp(brown(dur, rng), 140) * np.exp(-t / (decay * 0.6)) * 1.5
    return saturate(s + rumble, 1.6)


def impact(rng, size=1.0, dur=4.0):
    """Layered trailer hit: boom + war drum + crack + crash."""
    m = Mix(dur)
    m.add(boom(rng, dur=dur), 0, 0.9 * size)
    m.add(war_drum(rng, f=52, decay=1.0, dur=min(dur, 2.5)), 0, 0.8)
    m.add(kick(rng, f0=180, f1=45, decay=0.5), 0, 0.7)
    t = t_axis(0.4)
    crack = hp(white(0.4, rng), 1200) * np.exp(-t / 0.03)
    m.add(stereo(crack), 0, 0.5)
    m.add(crash(rng, dur=dur, decay=1.4), 0, 0.35)
    return m.out


def reverse_cymbal(rng, dur=1.5):
    c = crash(rng, dur=dur + 0.5, decay=dur * 0.7)[:, :n_samples(dur)]
    c = reverse(c)
    return c * ramp([(0, 0), (dur * 0.6, 0.6), (dur, 1)], dur)


def riser(rng, dur=2.0, f_lo=300, f_hi=6000, pitched=True):
    t = t_axis(dur)
    sweep_f = exp_ramp([(0, f_lo), (dur, f_hi)], dur)
    nz = sweep(white(dur, rng), "bandpass", sweep_f, q=2.0)
    s = nz * 1.5
    if pitched:
        s += supersaw(exp_ramp([(0, 110), (dur, 440)], dur), dur, rng, 5, 30, stereo=False)[0] * 0.15
    env = (t / dur) ** 2
    return stereo(s * env)


def whoosh(rng, dur=1.0, f_lo=250, f_hi=3000, peak=0.5, q=1.2, pan_from=-0.8, pan_to=0.8):
    t = t_axis(dur)
    env = np.exp(-((t - dur * peak) / (dur * 0.22)) ** 2)
    f = f_lo + (f_hi - f_lo) * env
    s = sweep(pink(dur, rng), "bandpass", f, q=q) * env
    return pan(s, np.linspace(pan_from, pan_to, len(t)))


# ============================================================================
# DISTORTED GUITAR
# ============================================================================

def ks_string(freq, dur, rng, t60=4.0, bright=5000, pick=0.13, mute_at=None, mute_t60=0.06,
              palm=False):
    """Karplus-Strong string, vectorised one period at a time, with
    linear-interpolated fractional delay for tuning."""
    n = n_samples(dur)
    N = SR / freq
    L = int(np.floor(N - 0.5))
    frac = N - 0.5 - L
    w0, w1, w2 = 0.5 * (1 - frac), 0.5, 0.5 * frac
    if palm:
        t60, bright = min(t60, 0.22), min(bright, 1400)
    exc = lp(rng.uniform(-1, 1, L + 64), bright)[64:]
    k = max(1, int(pick * L))
    exc = exc - np.concatenate([np.zeros(k), exc[:-k]])
    x = np.zeros(n)
    x[:min(L, n)] = exc[:min(L, n)]
    g = np.full(n, 10 ** (-3 / (t60 * freq)))
    if mute_at is not None and mute_at < dur:
        i = n_samples(mute_at)
        g[i:] = 10 ** (-3 / (mute_t60 * freq))
    off = L + 2
    y = np.zeros(n + off)
    for s in range(0, n, L):
        e = min(s + L, n)
        a, b = s + off, e + off
        y[a:b] = x[s:e] + g[s:e] * (w0 * y[a - L:b - L] + w1 * y[a - L - 1:b - L - 1]
                                    + w2 * y[a - L - 2:b - L - 2])
    return y[off:]


def power_chord(root_midi, dur, rng, intervals=(0, 7, 12), palm=False, strum=0.007,
                mute_at=None, t60=4.0, bend=None):
    n = n_samples(dur)
    out = np.zeros(n)
    for i, iv in enumerate(intervals):
        f = mtof(root_midi + iv) * cents(rng.normal(0, 3))
        d = i * strum * rng.uniform(0.6, 1.4)
        s = ks_string(f, dur, rng, t60=t60, palm=palm, mute_at=mute_at,
                      bright=rng.uniform(4000, 6500), pick=rng.uniform(0.08, 0.2))
        di = n_samples(d)
        out[di:] += s[:n - di] * (1.0 if i == 0 else 0.85)
    return out


def amp(x, gain=28.0, tight=120, mid_db=5.0, scoop_db=-4.0, presence_db=3.0, fizz=5200,
        os=4, level=0.6):
    """High-gain amp + 4x12 cab approximation."""
    x = hp(x, tight, order=2)
    x = eq(x, "peak", 750, 0.9, mid_db)

    def stages(v):
        v = np.tanh(gain * v)
        v = lp(v, 9000)
        v = np.tanh(2.2 * v + 0.15) - np.tanh(0.15)
        return v

    y = oversample(stages, x, os)
    # Cabinet
    y = hp(y, 75, order=2)
    y = eq(y, "peak", 115, 1.0, 3.5)
    y = eq(y, "peak", 420, 1.0, scoop_db)
    y = eq(y, "peak", 2600, 1.2, presence_db)
    y = lp(y, fizz, order=4)
    return y * level


def lead_tone(freq, dur, rng, vibrato=5.5, vib_depth=25, vib_delay=0.25, harmonics=12):
    """Sustained additive tone meant to be pushed through amp() for squeals."""
    n = n_samples(dur)
    t = t_axis(dur)
    f = as_array(freq, n)
    vib = np.minimum(1, np.maximum(0, t - vib_delay) / 0.3) * vib_depth
    f = f * cents(vib * np.sin(2 * np.pi * vibrato * t))
    partials = [(k, 1 / k ** 1.1) for k in range(1, harmonics + 1)]
    return additive(f, dur, partials, rng) * 0.25


def guitar_bus(events, dur, rng):
    """events: list of (time, kind, params). Renders the DI track."""
    m = np.zeros(n_samples(dur) + SR * 4)
    for t0, kind, p in events:
        if kind == "chord":
            s = power_chord(p["root"], p["len"] + p.get("ring", 0.05), rng,
                            intervals=p.get("iv", (0, 7, 12)), palm=p.get("palm", False),
                            mute_at=p["len"], t60=p.get("t60", 4.0))
        elif kind == "note":
            s = ks_string(mtof(p["midi"]), p["len"] + 0.05, rng, mute_at=p["len"],
                          palm=p.get("palm", False), t60=p.get("t60", 4.0))
        elif kind == "lead":
            s = lead_tone(p["freq"], p["len"], rng, vib_depth=p.get("vib", 25))
            s *= env_adsr(p["len"], 0.01, 0.1, 0.9, p.get("rel", 0.15))
        else:
            continue
        i = n_samples(t0)
        m[i:i + len(s)] += s * p.get("vel", 1.0)
    return m


def double_tracked_guitar(events, dur, rng, gain=28.0, spread=0.85, **amp_kw):
    """Two independent takes hard-ish panned L/R, the classic metal wall."""
    left = amp(guitar_bus(jitter(events, rng), dur, rng), gain=gain, **amp_kw)
    right = amp(guitar_bus(jitter(events, rng), dur, rng), gain=gain, **amp_kw)
    return pan(left, -spread) + pan(right, spread)


def jitter(events, rng, amount=0.006):
    return [(max(0, t + rng.normal(0, amount / 2)), k, p) for t, k, p in events]


# ============================================================================
# SYNTHS
# ============================================================================

def synth_pluck(freq, dur, rng, peak=5500, base=300, decay=0.18, voices=5, detune=18, q=2.0):
    t = t_axis(dur)
    s = supersaw(freq, dur, rng, voices, detune)
    cut = base + peak * np.exp(-t / decay)
    s = sweep(s, "lowpass", cut, q=q, block=32)
    return s * env_perc(dur, 0.002, decay * 2.5)


def synth_pad(midis, dur, rng, attack=1.0, release=1.5, cutoff=1800, detune=20, voices=5):
    out = np.zeros((2, n_samples(dur)))
    for m in midis:
        out += supersaw(mtof(m), dur, rng, voices, detune)
    out = lp(out, cutoff)
    return out * env_adsr(dur, attack, 0.2, 0.85, release) / max(1, len(midis))


def sub_bass(freq, dur, drive=1.3):
    s = osc_sine(freq, dur) + 0.25 * osc_sine(np.asarray(freq) * 2, dur)
    return saturate(s, drive)


def synth_bass(freq, dur, rng, cutoff=900, drive=3.0, decay=0.25):
    t = t_axis(dur)
    s = osc_saw(freq, dur) * 0.6 + osc_pulse(np.asarray(freq) * 0.5, dur, 0.4) * 0.5
    s = sweep(s, "lowpass", cutoff * 0.35 + cutoff * np.exp(-t / decay), q=1.6, block=32)
    return saturate(s, drive)


def synth_lead(freq, dur, rng, cutoff=4000, vib=12):
    t = t_axis(dur)
    f = as_array(freq, n_samples(dur)) * cents(vib * np.sin(2 * np.pi * 6 * t))
    s = osc_saw(f, dur) * 0.5 + osc_pulse(f * cents(7), dur, 0.3) * 0.5
    return saturate(lp(s, cutoff), 1.8)


def bell(freq, dur, rng, decay=1.2, bright=1.0):
    """Inharmonic bell (church/handbell partials)."""
    t = t_axis(dur)
    out = np.zeros_like(t)
    for ratio, amp, dk in ((0.5, 0.5, 1.4), (1.0, 1.0, 1.0), (1.183, 0.6, 0.8),
                           (1.506, 0.45, 0.6), (2.0, 0.4, 0.5), (2.514, 0.3, 0.4),
                           (2.662, 0.25, 0.35), (3.011, 0.2, 0.3), (4.166, 0.15 * bright, 0.2),
                           (5.433, 0.1 * bright, 0.15)):
        out += amp * osc_sine(freq * ratio * cents(rng.normal(0, 2)), dur, rng.random()) \
            * np.exp(-t / (decay * dk))
    return out * np.minimum(1, t / 0.001) * 0.35


def fm_bell(freq, dur, ratio=3.5, index=4.0, decay=0.8):
    t = t_axis(dur)
    idx = index * np.exp(-t / (decay * 0.4))
    return fm(freq, ratio, idx, dur) * env_perc(dur, 0.001, decay)


def coin(rng, dur=0.6, base=None):
    """Bright metallic coin ping."""
    base = base or rng.uniform(2400, 3600)
    t = t_axis(dur)
    out = np.zeros_like(t)
    for ratio, amp in ((1.0, 1.0), (1.47, 0.6), (2.09, 0.5), (2.56, 0.35), (3.2, 0.25)):
        out += amp * osc_sine(base * ratio, dur, rng.random()) * np.exp(-t / rng.uniform(0.08, 0.25))
    out += hp(white(dur, rng), 6000) * np.exp(-t / 0.004) * 0.6
    return out * 0.3


def sparkle(rng, dur=1.5, density=18, f_lo=2500, f_hi=9000, env=None):
    """Cloud of tiny bell pings scattered in time and stereo."""
    m = Mix(dur + 0.6)
    count = int(density * dur)
    for _ in range(count):
        t0 = rng.uniform(0, dur)
        f = np.exp(rng.uniform(np.log(f_lo), np.log(f_hi)))
        g = rng.uniform(0.2, 1.0) * (1.0 if env is None else float(np.interp(t0, env[0], env[1])))
        ping = fm_bell(f, 0.5, ratio=rng.choice([1.4, 2.0, 3.5]), index=1.5,
                       decay=rng.uniform(0.08, 0.25))
        m.add(ping, t0, g * 0.25, rng.uniform(-0.9, 0.9))
    return m.out


def brass(freq, dur, rng, bright=2500, voices=3, detune=8, growl=0.0, breath=0.15,
          attack=0.08, release=0.3, scoop=-80):
    """Horn/brass: saw stack with brightness tracking the envelope."""
    n = n_samples(dur)
    t = t_axis(dur)
    f = as_array(freq, n)
    f = f * cents(scoop * np.exp(-t / 0.05))  # lip scoop up into the note
    vib = np.minimum(1, np.maximum(0, t - 0.35) / 0.4)
    f = f * cents(vib * 9 * np.sin(2 * np.pi * 5.2 * t))
    env = env_adsr(dur, attack, 0.15, 0.85, release)
    src = np.zeros(n)
    for i in range(voices):
        dv = (i / max(1, voices - 1) - 0.5) * 2 * detune if voices > 1 else 0
        src += osc_saw(f * cents(dv + rng.normal(0, 2)), dur, rng.random())
    src /= voices
    if growl > 0:
        am = 1 + growl * smooth_random(dur, rng, 35) * 0.8
        src = src * am + growl * 0.6 * osc_pulse(f * 0.5, dur, 0.3)
    cut = 180 + bright * env ** 1.6
    s = sweep(src, "lowpass", cut, q=1.1, block=32)
    s = eq(s, "peak", 1100, 1.0, 3)
    s += bp(white(dur, rng), 800, 4000) * env_perc(dur, 0.01, 0.08) * breath
    return saturate(s * env, 1.4)


# ============================================================================
# VOICES / SOULS
# ============================================================================

VOWELS = {
    "a": [(700, 90, 0), (1150, 110, -5), (2650, 140, -12), (3300, 160, -20)],
    "e": [(450, 70, 0), (1750, 100, -9), (2600, 120, -10), (3400, 150, -20)],
    "i": [(300, 50, 0), (2000, 100, -14), (2900, 120, -14), (3600, 150, -22)],
    "o": [(450, 70, 0), (800, 80, -8), (2700, 120, -16), (3300, 150, -24)],
    "u": [(330, 50, 0), (650, 70, -14), (2700, 120, -20), (3300, 150, -28)],
}


def vowel_formants(seq, dur, scale=1.0):
    """seq: [(time, vowel), ...] -> list of (freq_array, bw, gain) for formant_filter."""
    out = []
    for k in range(4):
        pts_f = [(t, VOWELS[v][k][0] * scale) for t, v in seq]
        pts_g = [(t, VOWELS[v][k][2]) for t, v in seq]
        f = ramp(pts_f, dur) if len(seq) > 1 else VOWELS[seq[0][1]][k][0] * scale
        g = float(np.mean([p[1] for p in pts_g]))
        out.append((f, VOWELS[seq[0][1]][k][1], g))
    return out


def choir(midis, dur, rng, vowel="a", voices=4, attack=0.6, release=1.0, breath=0.12,
          detune=12, vib_depth=18, vowel_seq=None, pitch=None, formant_scale=1.0):
    """Formant choir. midis: list of chord tones. pitch: optional extra cents curve."""
    n = n_samples(dur)
    t = t_axis(dur)
    src = np.zeros(n)
    for m in midis:
        for v in range(voices):
            f = mtof(m) * cents(rng.normal(0, detune))
            rate = rng.uniform(4.6, 5.8)
            fv = f * cents(vib_depth * np.sin(2 * np.pi * rate * t + rng.random() * 6.28)
                           + 6 * smooth_random(dur, rng, 2))
            if pitch is not None:
                fv = fv * cents(as_array(pitch, n))
            src += osc_saw(fv, dur, rng.random()) * rng.uniform(0.7, 1.0)
    src /= np.sqrt(len(midis) * voices)
    src += pink(dur, rng) * breath
    seq = vowel_seq or [(0, vowel)]
    s = formant_filter(src, vowel_formants(seq, dur, formant_scale))
    s = lp(s, 6000)
    return s * env_adsr(dur, attack, 0.2, 0.9, release) * 3.0


def whisper(dur, rng, vowel_seq=None, flutter=9.0, scale=1.2):
    """Breathy unvoiced formant noise with syllable-like flutter."""
    nz = hp(white(dur, rng), 400)
    seq = vowel_seq or [(0, "a"), (dur * 0.3, "o"), (dur * 0.6, "i"), (dur, "u")]
    s = formant_filter(nz, vowel_formants(seq, dur, scale))
    s += hp(white(dur, rng), 4500) * 0.15  # sibilance
    syll = np.clip(0.5 + 0.8 * smooth_random(dur, rng, flutter), 0, 1) ** 1.5
    return s * syll * env_adsr(dur, 0.2, 0.1, 1.0, 0.4)


def ghost_wail(freq_curve, dur, rng, vowel_seq=None, vib=30):
    """Single eerie voice with portamento; freq_curve = Hz array or scalar."""
    n = n_samples(dur)
    t = t_axis(dur)
    f = as_array(freq_curve, n) * cents(vib * np.sin(2 * np.pi * 5.0 * t)
                                        + 15 * smooth_random(dur, rng, 3))
    src = osc_saw(f, dur) * 0.6 + osc_tri(f * 2, dur) * 0.2 + pink(dur, rng) * 0.08
    seq = vowel_seq or [(0, "u"), (dur * 0.5, "o"), (dur, "u")]
    s = formant_filter(src, vowel_formants(seq, dur, 1.15))
    return s * 3.0


def soul_tone(freq, dur, rng, shimmer=0.4):
    """Glassy, slowly beating sine cluster (the 'soul' timbre)."""
    t = t_axis(dur)
    out = np.zeros((2, n_samples(dur)))
    for ratio, amp in ((1.0, 1.0), (2.0, 0.4), (3.01, 0.25), (4.97, 0.15), (7.02, 0.08)):
        for side in (-1, 1):
            beat = cents(side * rng.uniform(3, 9))
            s = osc_sine(freq * ratio * beat, dur, rng.random()) * amp
            s *= 1 + shimmer * np.sin(2 * np.pi * rng.uniform(0.2, 1.2) * t + rng.random() * 6.28)
            out += pan(s, side * 0.7)
    return out * 0.15


def bubble(rng, f0=None, dur=None):
    """A single bubble 'bloop': sine whose pitch rises as it decays."""
    f0 = f0 or rng.uniform(250, 900)
    dur = dur or rng.uniform(0.04, 0.12)
    t = t_axis(dur)
    f = f0 * (1 + rng.uniform(1.5, 4.0) * t / dur)
    return osc_sine(f, dur) * env_perc(dur, 0.001, dur * 0.35)


def gurgle_bed(dur, rng, rate=40, f_lo=150, f_hi=700, wet=True):
    m = Mix(dur + 0.2)
    count = int(rate * dur)
    for _ in range(count):
        t0 = rng.uniform(0, dur)
        f0 = np.exp(rng.uniform(np.log(f_lo), np.log(f_hi)))
        m.add(bubble(rng, f0), t0, rng.uniform(0.2, 1.0), rng.uniform(-0.6, 0.6))
    return m.out
