"""Procedurally synthesizes the soul SFX (flight, land, gurgle, magic) as 44.1kHz 16-bit WAVs.

Usage: python3 Tools/generate_soul_sfx.py Assets/Audio/Souls   (requires numpy)
"""
import sys, wave, numpy as np

SR = 44100
rng = np.random.default_rng(7)
OUT = sys.argv[1]

def t_axis(dur): return np.arange(int(dur * SR)) / SR

def env_adsr(n, a, d, s, r, curve=3.0):
    """Attack/decay/sustain-level/release envelope in seconds."""
    A, D, R = int(a*SR), int(d*SR), int(r*SR)
    S = max(n - A - D - R, 0)
    e = np.concatenate([
        np.linspace(0, 1, A, endpoint=False) ** 1.5,
        1 - (1 - s) * (1 - np.exp(-curve * np.linspace(0, 1, D))) / (1 - np.exp(-curve)),
        np.full(S, s),
        s * np.exp(-curve * np.linspace(0, 1, R)),
    ])
    return np.pad(e, (0, max(n - len(e), 0)))[:n]

def svf_bandpass(x, fc, q):
    """Time-varying state-variable band-pass. fc may be an array (Hz)."""
    fc = np.broadcast_to(np.asarray(fc, float), x.shape)
    f = 2 * np.sin(np.pi * np.clip(fc, 20, SR / 6) / SR)
    damp = 1.0 / q
    low = band = 0.0
    y = np.empty_like(x)
    for i in range(len(x)):
        high = x[i] - low - damp * band
        band += f[i] * high
        low += f[i] * band
        y[i] = band
    return y

def onepole_lp(x, fc):
    a = np.exp(-2 * np.pi * fc / SR)
    y = np.empty_like(x); z = 0.0
    for i in range(len(x)):
        z = (1 - a) * x[i] + a * z; y[i] = z
    return y

def reverb(x, decay=1.2, mix=0.3, predelay=0.012, bright=6000):
    """Convolution with synthetic exponentially-decaying stereo-ish noise IR (mono out)."""
    n = int(decay * SR)
    ir = rng.standard_normal(n) * np.exp(-6.9 * np.arange(n) / n)
    ir = onepole_lp(ir, bright)
    ir = np.concatenate([np.zeros(int(predelay * SR)), ir])
    ir /= np.sqrt(np.sum(ir ** 2))
    L = len(x) + len(ir) - 1
    N = 1 << (L - 1).bit_length()
    wet = np.fft.irfft(np.fft.rfft(x, N) * np.fft.rfft(ir, N), N)[:L]
    dry = np.pad(x, (0, L - len(x)))
    return (1 - mix) * dry + mix * wet * 0.5

def bell(t, f0, decay, partials=((1, 1), (2.76, .45), (5.4, .25), (8.93, .12))):
    out = np.zeros_like(t)
    for ratio, amp in partials:
        out += amp * np.sin(2 * np.pi * f0 * ratio * t) * np.exp(-t * ratio ** 0.6 / decay)
    return out

def tail(x, secs=0.04):
    """Fade the end of a dry signal so truncation never clicks."""
    x = x.copy(); f = min(int(secs * SR), len(x)); x[-f:] *= np.linspace(1, 0, f) ** 2
    return x

def place(dst, src, start):
    src = tail(src, 0.01)
    i = int(start * SR); j = min(len(dst), i + len(src))
    if i < len(dst): dst[i:j] += src[:j - i]

def finish(x, peak=0.89, fade=0.02):
    x = x - np.mean(x)
    x = x / np.max(np.abs(x))
    loud = np.nonzero(np.abs(x) > 10 ** (-48 / 20))[0]          # trim silent reverb tail
    x = x[:loud[-1] + int(0.03 * SR)]
    f = int(fade * SR); x[-f:] *= np.linspace(1, 0, f)
    x[:64] *= np.linspace(0, 1, 64)
    return x / np.max(np.abs(x)) * peak

def write(name, x):
    pcm = (np.clip(x, -1, 1) * 32767).astype('<i2')
    with wave.open(f"{OUT}/{name}.wav", "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR); w.writeframes(pcm.tobytes())
    print(name, f"{len(x)/SR:.2f}s")

# ---------- SOUL FLIGHT: airy ghostly whoosh with a rising, wavering wail ----------
def soul_flight():
    dur = 0.9; t = t_axis(dur); n = len(t)
    shape = np.sin(np.pi * np.clip(t / 0.8, 0, 1)) ** 1.6           # swell in & out
    sweep = 500 + 2600 * (t / dur) ** 1.3                           # whoosh rises as it flies
    noise = svf_bandpass(rng.standard_normal(n), sweep, 2.2)
    noise2 = svf_bandpass(rng.standard_normal(n), sweep * 1.9, 4.0) * 0.4
    vib = 1 + 0.025 * np.sin(2 * np.pi * 6.5 * t)
    f = (620 + 520 * (t / dur) ** 1.2) * vib                        # ghostly wail
    ph = 2 * np.pi * np.cumsum(f) / SR
    wail = (np.sin(ph) + 0.35 * np.sin(2 * ph + 0.3) + 0.6 * np.sin(ph * 1.007)) * 0.18
    x = tail((noise + noise2) * shape + wail * shape ** 1.5)
    return finish(reverb(x, decay=0.8, mix=0.35))

# ---------- SOUL LAND: soft thump + glassy ethereal ping ----------
def soul_land():
    dur = 0.6; t = t_axis(dur); n = len(t)
    thump_f = 140 * np.exp(-t * 18) + 70
    thump = np.sin(2 * np.pi * np.cumsum(thump_f) / SR) * np.exp(-t * 22) * 0.9
    puff = svf_bandpass(rng.standard_normal(n), 1800, 1.2) * np.exp(-t * 40) * 0.6
    ping = bell(t, 1046.5, 0.18) * 0.55 + bell(t, 1567.98, 0.12) * 0.3   # C6 + G6
    shimmer = np.sin(2*np.pi*3136*t) * np.exp(-t*14) * 0.12 * (1 + np.sin(2*np.pi*28*t)) / 2
    x = tail(thump + puff + ping + shimmer, 0.15)
    return finish(reverb(x, decay=1.0, mix=0.3), peak=0.85)

# ---------- SOUL GURGLE: liquid bubbling as the bar fills ----------
def soul_gurgle():
    dur = 1.0; t = t_axis(dur); n = len(t)
    x = np.zeros(n)
    # Minnaert-style bubbles: decaying sines whose pitch rises as they pop
    times = np.sort(rng.uniform(0.0, 0.8, 26))
    for st in times:
        bd = rng.uniform(0.03, 0.08); bt = t_axis(bd)
        f0 = rng.uniform(260, 900)
        f = f0 * (1 + 1.6 * bt / bd)
        b = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-bt / (bd * 0.35))
        b *= np.minimum(1, bt / 0.002)
        place(x, b * rng.uniform(0.35, 1.0), st)
    # low ghostly undertone + filtered churning slosh
    wob = 1 + 0.08 * np.sin(2 * np.pi * 9 * t)
    under = np.sin(2 * np.pi * np.cumsum(110 * wob) / SR) * 0.18
    slosh = svf_bandpass(rng.standard_normal(n), 300 + 200 * np.sin(2 * np.pi * 7 * t), 3) * 0.5
    x = tail(x + (under + slosh) * env_adsr(n, 0.06, 0.1, 0.8, 0.25))
    return finish(reverb(x, decay=0.6, mix=0.22), peak=0.8)

# ---------- SOUL MAGIC: shimmering arpeggio + sparkles when the bar completes ----------
def soul_magic():
    dur = 2.0; t = t_axis(dur); n = len(t)
    x = np.zeros(n)
    notes = [523.25, 659.25, 783.99, 1046.5, 1318.5, 1567.98]   # C major arpeggio up
    for k, f in enumerate(notes):
        bt = t_axis(1.4)
        tone = bell(bt, f, 0.45) + 0.5 * bell(bt, f * 1.004, 0.45)  # slight detune chorus
        place(x, tail(tone, 0.6) * (0.5 + 0.08 * k), 0.065 * k)
    # sustained pad swell underneath (C/E/G with slow beating)
    pad_env = env_adsr(n, 0.25, 0.3, 0.6, 1.2)
    pad = sum(np.sin(2*np.pi*f*t) + np.sin(2*np.pi*f*1.003*t) for f in (261.63, 329.63, 392.0)) * 0.08
    x += pad * pad_env
    # random high sparkles
    for st in np.sort(rng.uniform(0.25, 1.4, 40)):
        bt = t_axis(0.12)
        f = rng.uniform(3000, 7500)
        place(x, np.sin(2*np.pi*f*bt) * np.exp(-bt * 45) * rng.uniform(0.05, 0.16), st)
    # airy rising whoosh
    air = svf_bandpass(rng.standard_normal(n), 1500 + 5000 * np.clip(t / 0.6, 0, 1), 3) * env_adsr(n, 0.3, 0.3, 0.0, 0.1) * 0.25
    x = tail(x + air, 0.3)
    return finish(reverb(x, decay=1.8, mix=0.4, bright=9000), peak=0.85)

for name, fn in [("SoulFlight", soul_flight), ("SoulLand", soul_land),
                 ("SoulGurgle", soul_gurgle), ("SoulMagic", soul_magic)]:
    write(name, fn())
