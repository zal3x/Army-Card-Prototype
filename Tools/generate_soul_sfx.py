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
def soul_flight(dur=0.9, swell=0.8, sweep_lo=500, sweep_hi=2600, wail_lo=620, wail_rise=520,
                vib_rate=6.5, vib_depth=0.025, flutter_rate=0.0, wail_amp=0.18):
    t = t_axis(dur); n = len(t)
    shape = np.sin(np.pi * np.clip(t / swell, 0, 1)) ** 1.6         # swell in & out
    if flutter_rate:                                                # wing-beat style pulsing
        shape *= 0.6 + 0.4 * np.sin(2 * np.pi * flutter_rate * t) ** 2
    sweep = sweep_lo + sweep_hi * (t / dur) ** 1.3                  # whoosh rises as it flies
    noise = svf_bandpass(rng.standard_normal(n), sweep, 2.2)
    noise2 = svf_bandpass(rng.standard_normal(n), sweep * 1.9, 4.0) * 0.4
    vib = 1 + vib_depth * np.sin(2 * np.pi * vib_rate * t)
    f = (wail_lo + wail_rise * (t / dur) ** 1.2) * vib              # ghostly wail
    ph = 2 * np.pi * np.cumsum(f) / SR
    wail = (np.sin(ph) + 0.35 * np.sin(2 * ph + 0.3) + 0.6 * np.sin(ph * 1.007)) * wail_amp
    x = tail((noise + noise2) * shape + wail * shape ** 1.5)
    return finish(reverb(x, decay=0.8, mix=0.35))

# ---------- SOUL LAND: soft thump + glassy ethereal ping ----------
def soul_land(ping_hz=(1046.5, 1567.98), thump_hz=70, thump_amp=0.9, shimmer_hz=3136, puff_hz=1800):
    dur = 0.6; t = t_axis(dur); n = len(t)
    thump_f = 2 * thump_hz * np.exp(-t * 18) + thump_hz
    thump = np.sin(2 * np.pi * np.cumsum(thump_f) / SR) * np.exp(-t * 22) * thump_amp
    puff = svf_bandpass(rng.standard_normal(n), puff_hz, 1.2) * np.exp(-t * 40) * 0.6
    ping = bell(t, ping_hz[0], 0.18) * 0.55 + bell(t, ping_hz[1], 0.12) * 0.3
    shimmer = np.sin(2*np.pi*shimmer_hz*t) * np.exp(-t*14) * 0.12 * (1 + np.sin(2*np.pi*28*t)) / 2
    x = tail(thump + puff + ping + shimmer, 0.15)
    return finish(reverb(x, decay=1.0, mix=0.3), peak=0.85)

# ---------- SOUL GURGLE: liquid bubbling as the bar fills ----------
def soul_gurgle(bubbles=26, bubble_hz=(260, 900), bubble_len=(0.03, 0.08), under_hz=110,
                wobble_hz=9, slosh_hz=300, slosh_rate=7):
    dur = 1.0; t = t_axis(dur); n = len(t)
    x = np.zeros(n)
    # Minnaert-style bubbles: decaying sines whose pitch rises as they pop
    times = np.sort(rng.uniform(0.0, 0.8, bubbles))
    for st in times:
        bd = rng.uniform(*bubble_len); bt = t_axis(bd)
        f0 = rng.uniform(*bubble_hz)
        f = f0 * (1 + 1.6 * bt / bd)
        b = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-bt / (bd * 0.35))
        b *= np.minimum(1, bt / 0.002)
        place(x, b * rng.uniform(0.35, 1.0), st)
    # low ghostly undertone + filtered churning slosh
    wob = 1 + 0.08 * np.sin(2 * np.pi * wobble_hz * t)
    under = np.sin(2 * np.pi * np.cumsum(under_hz * wob) / SR) * 0.18
    slosh = svf_bandpass(rng.standard_normal(n), slosh_hz + 200 * np.sin(2 * np.pi * slosh_rate * t), 3) * 0.5
    x = tail(x + (under + slosh) * env_adsr(n, 0.06, 0.1, 0.8, 0.25))
    return finish(reverb(x, decay=0.6, mix=0.22), peak=0.8)

# ---------- SOUL MAGIC: shimmering arpeggio + sparkles when the bar completes ----------
def soul_magic(notes=(523.25, 659.25, 783.99, 1046.5, 1318.5, 1567.98),   # C major arpeggio up
               pad=(261.63, 329.63, 392.0), step=0.065, sparkles=40, sparkle_hz=(3000, 7500)):
    dur = 2.0; t = t_axis(dur); n = len(t)
    x = np.zeros(n)
    for k, f in enumerate(notes):
        bt = t_axis(1.4)
        tone = bell(bt, f, 0.45) + 0.5 * bell(bt, f * 1.004, 0.45)  # slight detune chorus
        place(x, tail(tone, 0.6) * (0.5 + 0.08 * k), step * k)
    # sustained pad swell underneath with slow beating
    pad_env = env_adsr(n, 0.25, 0.3, 0.6, 1.2)
    pad_sig = sum(np.sin(2*np.pi*f*t) + np.sin(2*np.pi*f*1.003*t) for f in pad) * 0.08
    x += pad_sig * pad_env
    # random high sparkles
    for st in np.sort(rng.uniform(0.25, 1.4, sparkles)):
        bt = t_axis(0.12)
        f = rng.uniform(*sparkle_hz)
        place(x, np.sin(2*np.pi*f*bt) * np.exp(-bt * 45) * rng.uniform(0.05, 0.16), st)
    # airy rising whoosh
    air = svf_bandpass(rng.standard_normal(n), 1500 + 5000 * np.clip(t / 0.6, 0, 1), 3) * env_adsr(n, 0.3, 0.3, 0.0, 0.1) * 0.25
    x = tail(x + air, 0.3)
    return finish(reverb(x, decay=1.8, mix=0.4, bright=9000), peak=0.85)

# Originals first (in this order) so the shared RNG reproduces them exactly.
for name, fn in [("SoulFlight", soul_flight), ("SoulLand", soul_land),
                 ("SoulGurgle", soul_gurgle), ("SoulMagic", soul_magic)]:
    write(name, fn())

# ---------- Alternates ----------
ALTERNATES = [
    # Flight 2: lower, slower, mournful wail with a longer drift
    ("SoulFlight2", lambda: soul_flight(dur=1.1, swell=1.0, sweep_lo=350, sweep_hi=1900,
                                        wail_lo=440, wail_rise=330, vib_rate=4.5, vib_depth=0.035)),
    # Flight 3: quick, bright dart with a fluttering pulse
    ("SoulFlight3", lambda: soul_flight(dur=0.7, swell=0.62, sweep_lo=800, sweep_hi=3600,
                                        wail_lo=780, wail_rise=700, vib_rate=8.0, flutter_rate=11, wail_amp=0.14)),
    # Land 2: E6 + B6 ping, lighter thump
    ("SoulLand2", lambda: soul_land(ping_hz=(1318.5, 1975.5), thump_hz=85, thump_amp=0.7, shimmer_hz=3951)),
    # Land 3: lower A5 + E6 ping, heavier thump
    ("SoulLand3", lambda: soul_land(ping_hz=(880.0, 1318.5), thump_hz=58, thump_amp=1.1, shimmer_hz=2637, puff_hz=1300)),
    # Gurgle 2: deeper, slower, fewer bigger bubbles
    ("SoulGurgle2", lambda: soul_gurgle(bubbles=18, bubble_hz=(180, 600), bubble_len=(0.05, 0.11),
                                        under_hz=82, wobble_hz=6, slosh_hz=220, slosh_rate=5)),
    # Gurgle 3: busy, higher fizzing bubbles
    ("SoulGurgle3", lambda: soul_gurgle(bubbles=38, bubble_hz=(400, 1300), bubble_len=(0.02, 0.05),
                                        under_hz=130, wobble_hz=12, slosh_hz=380, slosh_rate=9)),
    # Magic 2: F major arpeggio, a touch faster
    ("SoulMagic2", lambda: soul_magic(notes=(698.46, 880.0, 1046.5, 1396.9, 1760.0, 2093.0),
                                      pad=(349.23, 440.0, 523.25), step=0.055)),
    # Magic 3: mystical D sus2 pentatonic run, slower and sparklier
    ("SoulMagic3", lambda: soul_magic(notes=(587.33, 659.25, 880.0, 987.77, 1174.66, 1318.5, 1760.0),
                                      pad=(293.66, 329.63, 440.0), step=0.075, sparkles=55,
                                      sparkle_hz=(3500, 9000))),
]
for name, fn in ALTERNATES:
    write(name, fn())
