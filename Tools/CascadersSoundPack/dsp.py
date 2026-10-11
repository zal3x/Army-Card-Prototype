"""Core DSP building blocks for the Cascaders sound pack generator.

Conventions:
  * Mono signals are 1-D float arrays, stereo signals are shape (2, n).
  * Everything runs at SR (44.1 kHz) in float64; mastering happens at export.
"""
import numpy as np
from scipy import signal
from scipy.ndimage import minimum_filter1d, uniform_filter1d

SR = 44100


# ----------------------------------------------------------------------------
# Time / envelope helpers
# ----------------------------------------------------------------------------

def n_samples(dur):
    return max(1, int(round(dur * SR)))


def t_axis(dur):
    return np.arange(n_samples(dur)) / SR


def as_array(v, n):
    """Scalar -> constant array; array of wrong length -> stretched to n."""
    if np.isscalar(v):
        return np.full(n, float(v))
    v = np.asarray(v, dtype=float)
    if len(v) == n:
        return v
    return np.interp(np.linspace(0, 1, n), np.linspace(0, 1, len(v)), v)


def ramp(points, dur):
    """Linear breakpoint envelope. points = [(time, value), ...]."""
    ts, vs = zip(*points)
    return np.interp(t_axis(dur), ts, vs)


def exp_ramp(points, dur):
    """Breakpoint envelope interpolated in the log domain (good for pitch)."""
    return np.exp(ramp([(t, np.log(v)) for t, v in points], dur))


def env_perc(dur, attack=0.002, decay=0.3):
    t = t_axis(dur)
    return np.minimum(1.0, t / max(attack, 1e-5)) * np.exp(-t / decay)


def env_adsr(dur, a=0.01, d=0.1, s=0.7, r=0.3):
    """ADSR where the release starts at dur - r."""
    rel_start = max(a + d, dur - r)
    return ramp([(0, 0), (a, 1), (a + d, s), (rel_start, s), (dur, 0)], dur)


def mtof(m):
    return 440.0 * 2 ** ((np.asarray(m, dtype=float) - 69) / 12)


def cents(c):
    return 2 ** (c / 1200)


# ----------------------------------------------------------------------------
# Oscillators (band-limited where it matters)
# ----------------------------------------------------------------------------

def _phase(freq, n, phase0=0.0):
    f = as_array(freq, n)
    return (phase0 + np.cumsum(f) / SR) % 1.0, np.clip(np.abs(f) / SR, 1e-7, 0.5)


def _blep(ph, dt):
    out = np.zeros_like(ph)
    m = ph < dt
    x = ph[m] / dt[m]
    out[m] = x + x - x * x - 1
    m = ph > 1 - dt
    x = (ph[m] - 1) / dt[m]
    out[m] = x * x + x + x + 1
    return out


def osc_sine(freq, dur, phase0=0.0):
    n = n_samples(dur)
    f = as_array(freq, n)
    return np.sin(2 * np.pi * (phase0 + np.cumsum(f) / SR))


def osc_saw(freq, dur, phase0=0.0):
    ph, dt = _phase(freq, n_samples(dur), phase0)
    return 2 * ph - 1 - _blep(ph, dt)


def osc_pulse(freq, dur, width=0.5, phase0=0.0):
    ph, dt = _phase(freq, n_samples(dur), phase0)
    ph2 = (ph + width) % 1.0
    return (2 * ph - 1 - _blep(ph, dt)) - (2 * ph2 - 1 - _blep(ph2, dt))


def osc_tri(freq, dur, phase0=0.0):
    n = n_samples(dur)
    ph = (phase0 + np.cumsum(as_array(freq, n)) / SR) % 1.0
    return 4 * np.abs(ph - 0.5) - 1


def supersaw(freq, dur, rng, voices=7, detune=22.0, stereo=True):
    """Detuned saw stack. detune is the outer spread in cents."""
    n = n_samples(dur)
    f = as_array(freq, n)
    out = np.zeros((2, n))
    for i in range(voices):
        spread = (i / (voices - 1) - 0.5) * 2 if voices > 1 else 0.0
        v = osc_saw(f * cents(spread * detune), dur, rng.random())
        p = spread * 0.8 if stereo else 0.0
        out += pan(v, p)
    return out / np.sqrt(voices)


def additive(freq, dur, partials, phase_jitter=None):
    """partials = [(ratio, amp), ...] on a shared (possibly gliding) pitch."""
    n = n_samples(dur)
    ph = np.cumsum(as_array(freq, n)) / SR
    out = np.zeros(n)
    for i, (ratio, amp) in enumerate(partials):
        p0 = 0.0 if phase_jitter is None else phase_jitter.random()
        out += amp * np.sin(2 * np.pi * (ratio * ph + p0))
    return out


def fm(carrier, ratio, index, dur, feedback_env=None):
    """Two-op FM: carrier frequency (array ok), modulator = carrier * ratio."""
    n = n_samples(dur)
    fc = as_array(carrier, n)
    idx = as_array(index, n)
    mod = np.sin(2 * np.pi * np.cumsum(fc * ratio) / SR)
    return np.sin(2 * np.pi * np.cumsum(fc) / SR + idx * mod)


# ----------------------------------------------------------------------------
# Noise
# ----------------------------------------------------------------------------

def white(dur, rng):
    return rng.standard_normal(n_samples(dur))


def pink(dur, rng):
    b = [0.049922035, -0.095993537, 0.050612699, -0.004408786]
    a = [1, -2.494956002, 2.017265875, -0.522189400]
    return signal.lfilter(b, a, white(dur, rng)) * 4.0


def brown(dur, rng):
    x = signal.lfilter([1], [1, -0.995], white(dur, rng))
    return x / (np.max(np.abs(x)) + 1e-9)


def smooth_random(dur, rng, rate=5.0):
    """Smooth random control signal in [-1, 1] changing ~rate times per sec."""
    pts = max(4, int(dur * rate) + 3)
    vals = rng.uniform(-1, 1, pts)
    x = np.linspace(0, pts - 1, n_samples(dur))
    i = np.floor(x).astype(int).clip(0, pts - 2)
    fr = x - i
    fr = fr * fr * (3 - 2 * fr)
    return vals[i] * (1 - fr) + vals[i + 1] * fr


# ----------------------------------------------------------------------------
# Filters
# ----------------------------------------------------------------------------

def _clipf(f):
    return float(np.clip(f, 10.0, SR * 0.49))


def butter(x, kind, f, order=2):
    if isinstance(f, (tuple, list)):
        f = [_clipf(v) for v in f]
    else:
        f = _clipf(f)
    sos = signal.butter(order, f, btype=kind, fs=SR, output="sos")
    return signal.sosfilt(sos, x, axis=-1)


def lp(x, f, order=2):
    return butter(x, "lowpass", f, order)


def hp(x, f, order=2):
    return butter(x, "highpass", f, order)


def bp(x, lo, hi, order=2):
    return butter(x, "bandpass", (lo, hi), order)


def biquad(kind, f, q=0.707, gain_db=0.0):
    """RBJ cookbook biquad coefficients."""
    f = _clipf(f)
    w0 = 2 * np.pi * f / SR
    cw, sw = np.cos(w0), np.sin(w0)
    alpha = sw / (2 * q)
    A = 10 ** (gain_db / 40)
    if kind == "lowpass":
        b = [(1 - cw) / 2, 1 - cw, (1 - cw) / 2]
        a = [1 + alpha, -2 * cw, 1 - alpha]
    elif kind == "highpass":
        b = [(1 + cw) / 2, -(1 + cw), (1 + cw) / 2]
        a = [1 + alpha, -2 * cw, 1 - alpha]
    elif kind == "bandpass":
        b = [alpha, 0, -alpha]
        a = [1 + alpha, -2 * cw, 1 - alpha]
    elif kind == "notch":
        b = [1, -2 * cw, 1]
        a = [1 + alpha, -2 * cw, 1 - alpha]
    elif kind == "peak":
        b = [1 + alpha * A, -2 * cw, 1 - alpha * A]
        a = [1 + alpha / A, -2 * cw, 1 - alpha / A]
    elif kind == "lowshelf":
        sa = 2 * np.sqrt(A) * alpha
        b = [A * ((A + 1) - (A - 1) * cw + sa), 2 * A * ((A - 1) - (A + 1) * cw),
             A * ((A + 1) - (A - 1) * cw - sa)]
        a = [(A + 1) + (A - 1) * cw + sa, -2 * ((A - 1) + (A + 1) * cw),
             (A + 1) + (A - 1) * cw - sa]
    elif kind == "highshelf":
        sa = 2 * np.sqrt(A) * alpha
        b = [A * ((A + 1) + (A - 1) * cw + sa), -2 * A * ((A - 1) + (A + 1) * cw),
             A * ((A + 1) + (A - 1) * cw - sa)]
        a = [(A + 1) - (A - 1) * cw + sa, 2 * ((A - 1) - (A + 1) * cw),
             (A + 1) - (A - 1) * cw - sa]
    else:
        raise ValueError(kind)
    b = np.array(b) / a[0]
    a = np.array(a) / a[0]
    return b, a


def eq(x, kind, f, q=0.707, gain_db=0.0):
    b, a = biquad(kind, f, q, gain_db)
    return signal.lfilter(b, a, x, axis=-1)


def sweep(x, kind, freq, q=0.707, gain_db=0.0, block=64):
    """Time-varying biquad: freq may be an array (one value per sample)."""
    if x.ndim == 2:
        return np.vstack([sweep(c, kind, freq, q, gain_db, block) for c in x])
    n = len(x)
    f = as_array(freq, n)
    qa = as_array(q, n)
    out = np.empty(n)
    zi = np.zeros(2)
    for i in range(0, n, block):
        b, a = biquad(kind, f[i], qa[i], gain_db)
        out[i:i + block], zi = signal.lfilter(b, a, x[i:i + block], zi=zi)
    return out


def formant_filter(x, formants, block=64):
    """formants = [(freq_or_array, bandwidth, gain_db), ...] in parallel."""
    out = np.zeros_like(x)
    for f, bw, g in formants:
        fa = as_array(f, x.shape[-1])
        q = max(0.5, float(np.mean(fa)) / bw)
        if np.ptp(fa) < 1e-6:
            y = eq(x, "bandpass", fa[0], q)
        else:
            y = sweep(x, "bandpass", fa, q, block=block)
        out += y * 10 ** (g / 20)
    return out


# ----------------------------------------------------------------------------
# Nonlinear / colour
# ----------------------------------------------------------------------------

def oversample(fn, x, factor=4):
    n = x.shape[-1]
    up = signal.resample_poly(x, factor, 1, axis=-1)
    return signal.resample_poly(fn(up), 1, factor, axis=-1)[..., :n]


def saturate(x, drive=2.0, asym=0.0):
    return (np.tanh(drive * x + asym) - np.tanh(asym)) / np.tanh(drive)


def fold(x, amount=2.0):
    return np.sin(x * amount * np.pi / 2)


def bitcrush(x, bits=8, hold=4):
    q = 2 ** (bits - 1)
    y = np.round(x * q) / q
    if hold > 1:
        idx = (np.arange(y.shape[-1]) // hold) * hold
        y = y[..., idx]
    return y


def ring_mod(x, freq):
    return x * osc_sine(freq, x.shape[-1] / SR)


# ----------------------------------------------------------------------------
# Stereo / space
# ----------------------------------------------------------------------------

def pan(x, p=0.0):
    """Equal-power pan of a mono signal; p in [-1, 1] (may be an array)."""
    p = np.clip(p, -1, 1)
    ang = (p + 1) * np.pi / 4
    return np.vstack([x * np.cos(ang), x * np.sin(ang)]) * np.sqrt(2)


def stereo(x):
    return x if x.ndim == 2 else np.vstack([x, x])


def widen(x, amount=1.5):
    x = stereo(x)
    mid = (x[0] + x[1]) / 2
    side = (x[0] - x[1]) / 2 * amount
    return np.vstack([mid + side, mid - side])


def haas(x, ms=12.0, p=0.0):
    """Mono -> wide stereo with a short inter-channel delay."""
    d = int(ms * SR / 1000)
    r = np.concatenate([np.zeros(d), x])[:len(x)]
    return pan(x, -0.3 + p) * 0.5 + pan(r, 0.3 + p) * 0.5


_ir_cache = {}


def make_ir(rt60=2.0, predelay=0.02, damping=0.45, seed=7, early=True):
    """Synthetic stereo hall IR: decorrelated noise with frequency-dependent decay."""
    key = (round(rt60, 3), round(predelay, 3), round(damping, 3), seed, early)
    if key in _ir_cache:
        return _ir_cache[key]
    rng = np.random.default_rng(seed)
    dur = rt60 * 1.1 + predelay
    t = t_axis(dur)
    ir = np.zeros((2, len(t)))
    pd = n_samples(predelay)
    for ch in range(2):
        nz = rng.standard_normal(len(t))
        lo = lp(nz, 1800)
        hi = nz - lo
        k = 6.91 / rt60
        tail = lo * np.exp(-k * t) + hi * np.exp(-k * t / damping)
        tail = lp(tail, 9000)
        tail[:pd] = 0
        tail[pd:] *= np.minimum(1, (t[pd:] - predelay) / 0.01)
        if early:
            for _ in range(10):
                i = pd + int(rng.uniform(0.003, 0.07) * SR)
                if i < len(tail):
                    tail[i] += rng.uniform(-1, 1) * 6 * np.exp(-(i / SR) * 8)
        ir[ch] = tail
    ir /= np.sqrt(np.sum(ir ** 2) / 2)
    _ir_cache[key] = ir
    return ir


def reverb(x, mix=0.3, rt60=2.0, predelay=0.02, damping=0.45, seed=7, hp_wet=150):
    """Returns dry + wet (with tail). x mono or stereo."""
    x = stereo(x)
    ir = make_ir(rt60, predelay, damping, seed)
    wet = np.vstack([signal.fftconvolve(x[c], ir[c]) for c in range(2)])
    if hp_wet:
        wet = hp(wet, hp_wet)
    out = wet * mix
    out[:, :x.shape[1]] += x
    return out


def delay(x, time=0.3, feedback=0.4, mix=0.3, lp_f=4000, ping_pong=True, repeats=None):
    x = stereo(x)
    d = n_samples(time)
    if repeats is None:
        repeats = int(np.ceil(np.log(0.001) / np.log(max(feedback, 1e-3))))
    out = np.zeros((2, x.shape[1] + d * repeats))
    out[:, :x.shape[1]] += x
    tap = x.copy()
    for k in range(1, repeats + 1):
        tap = lp(tap, lp_f) * feedback
        if ping_pong:
            tap = tap[::-1]
        out[:, k * d:k * d + tap.shape[1]] += tap * (mix / feedback)
    return out


def reverse(x):
    return x[..., ::-1].copy()


# ----------------------------------------------------------------------------
# Mixing
# ----------------------------------------------------------------------------

class Mix:
    """Growable stereo bus."""

    def __init__(self, dur=1.0):
        self.buf = np.zeros((2, n_samples(dur)))

    def add(self, x, t=0.0, gain=1.0, p=0.0, tail=True):
        """Mix x in at time t. tail=True fades the clip's last 25% (max 0.6 s) so
        layers whose decay outlives their buffer never end in a hard cut; pass
        tail=False for sounds meant to stop dead (reverse swells, risers)."""
        if x.ndim == 1:
            x = pan(x, p)
        if tail:
            x = x.copy()
            k = min(int(x.shape[1] * 0.25), n_samples(0.6))
            if k > 1:
                x[:, -k:] *= 0.5 + 0.5 * np.cos(np.linspace(0, np.pi, k))
        i = int(round(t * SR))
        if i < 0:
            x = x[:, -i:]
            i = 0
        end = i + x.shape[1]
        if end > self.buf.shape[1]:
            self.buf = np.pad(self.buf, ((0, 0), (0, end - self.buf.shape[1])))
        self.buf[:, i:end] += gain * x
        return self

    @property
    def out(self):
        return self.buf


def pad_to(x, n):
    x = stereo(x) if x.ndim == 2 else x
    if x.shape[-1] >= n:
        return x[..., :n]
    widths = ((0, 0), (0, n - x.shape[-1])) if x.ndim == 2 else (0, n - len(x))
    return np.pad(x, widths)


def fade(x, fin=0.002, fout=0.01):
    x = x.copy()
    n = x.shape[-1]
    a, b = min(n, n_samples(fin)), min(n, n_samples(fout))
    x[..., :a] *= np.linspace(0, 1, a)
    x[..., n - b:] *= np.linspace(1, 0, b)
    return x


def normalize(x, peak=1.0):
    m = np.max(np.abs(x))
    return x if m < 1e-12 else x * (peak / m)


def compress(x, threshold_db=-18.0, ratio=3.0, attack=0.01, release=0.15, makeup_db=0.0):
    """Linked-stereo feed-forward compressor on a smoothed RMS detector."""
    xs = stereo(x)
    lvl = np.sqrt(np.mean(xs ** 2, axis=0))
    win = max(1, n_samples(attack))
    lvl = np.sqrt(uniform_filter1d(lvl ** 2, win) + 1e-12)
    a_rel = np.exp(-1 / (release * SR))
    env = signal.lfilter([1 - a_rel], [1, -a_rel], lvl)
    env = np.maximum(env, lvl)
    db = 20 * np.log10(env + 1e-12)
    over = np.maximum(0, db - threshold_db)
    gain_db = -over * (1 - 1 / ratio) + makeup_db
    g = 10 ** (gain_db / 20)
    return x * g


def limiter(x, ceiling_db=-1.0, lookahead=0.005):
    """Look-ahead peak limiter (vectorised min-filter gain computer)."""
    xs = stereo(x)
    ceil = 10 ** (ceiling_db / 20)
    peak = np.max(np.abs(xs), axis=0)
    g = np.minimum(1.0, ceil / (peak + 1e-12))
    w = max(1, n_samples(lookahead))
    g = minimum_filter1d(g, size=2 * w + 1)
    g = uniform_filter1d(g, size=w)
    g = minimum_filter1d(g, size=w)  # never let smoothing overshoot the ceiling
    y = x * g
    return np.clip(y, -ceil, ceil)


def trim_silence(x, thresh_db=-66.0, keep=0.05, lead_db=None):
    """Trim the silent tail (and, with lead_db, the near-silent start)."""
    xs = stereo(x)
    level = np.max(np.abs(xs), axis=0)
    idx = np.nonzero(level > 10 ** (thresh_db / 20))[0]
    if len(idx) == 0:
        return x
    end = min(xs.shape[1], idx[-1] + n_samples(keep))
    start = 0
    if lead_db is not None:
        lead = np.nonzero(level > 10 ** (lead_db / 20) * level.max())[0]
        start = max(0, lead[0] - n_samples(0.005))
    return x[..., start:end]


def active_rms_db(x, floor_db=-40.0):
    """RMS over the part of the sound that is within floor_db of its loudest moment."""
    xs = stereo(x)
    p = uniform_filter1d(np.mean(xs ** 2, axis=0), n_samples(0.05))
    active = p > p.max() * 10 ** (floor_db / 10)
    return 10 * np.log10(np.mean(p[active]) + 1e-20)


def master_sfx(x, ceiling_db=-1.0, target_db=-16.0, max_push_db=4.0, glue=True):
    """Glue-compress, then level every SFX to a common loudness (active RMS) so the
    whole pack sits together; peaks are held under the ceiling by the limiter,
    which is never asked for more than max_push_db of gain reduction."""
    x = stereo(x)
    x = hp(x, 28, order=2)
    x = x - np.mean(x, axis=1, keepdims=True)
    x = normalize(x, 1.0)
    if glue:
        x = compress(x, threshold_db=-14, ratio=2.5, attack=0.005, release=0.12)
    x = normalize(x, 10 ** (ceiling_db / 20))
    push = min(target_db - active_rms_db(x), max_push_db)
    x = limiter(x * 10 ** (push / 20), ceiling_db)
    x = trim_silence(x, lead_db=-50)
    return fade(x, 0.001, 0.03)


def master_music(x, ceiling_db=-1.0, loop=False):
    x = stereo(x)
    x = hp(x, 30, order=2)
    x = normalize(x, 1.0)
    x = compress(x, threshold_db=-16, ratio=2.2, attack=0.02, release=0.25)
    x = normalize(x, 1.0)
    rms = np.sqrt(np.mean(x ** 2))
    x = x * (10 ** (-13.5 / 20) / rms)  # push into the limiter for density
    x = limiter(x, ceiling_db)
    return x if loop else fade(trim_silence(x, -60, 0.3), 0.002, 0.5)
