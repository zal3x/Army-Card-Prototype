"""Music cues: Soul Altar loop, Devil's Deal riff, Pre-Battle loop, VS screen loop + sting.

Each bus is levelled to a target RMS (dB) before the mixdown so the balance is
predictable, then the mix is mastered. Loops are rendered with an extra tail that
is folded back onto the start so reverb/ringing chords wrap seamlessly.
"""
import numpy as np

from dsp import (SR, n_samples, t_axis, ramp, exp_ramp, env_perc, env_adsr, mtof, cents,
                 osc_sine, supersaw, white, lp, hp, eq, sweep, saturate, pan, stereo, widen,
                 haas, reverb, delay, Mix)
from instruments import (kick, snare, tom, hat, crash, ride, war_drum, boom, impact,
                         reverse_cymbal, riser, power_chord, amp, guitar_bus,
                         double_tracked_guitar, synth_pluck, synth_pad, sub_bass, synth_bass,
                         synth_lead, brass, choir, whisper, soul_tone, bell)


class Song:
    def __init__(self, bpm, bars, tail=5.0, beats_per_bar=4):
        self.beat = 60.0 / bpm
        self.bpb = beats_per_bar
        self.bars = bars
        self.length = bars * beats_per_bar * self.beat
        self.dur = self.length + tail
        self.buses = {}

    def t(self, bar, beat=0.0):
        return (bar * self.bpb + beat) * self.beat

    def bus(self, name):
        if name not in self.buses:
            self.buses[name] = Mix(self.dur)
        return self.buses[name]

    def mixdown(self, levels, loop):
        n = n_samples(self.dur)
        out = np.zeros((2, n))
        for name, mix in self.buses.items():
            x = mix.out[:, :n]
            x = np.pad(x, ((0, 0), (0, n - x.shape[1])))
            out += level(x, levels.get(name, -24.0))
        if loop:
            L = n_samples(self.length)
            body = out[:, :L].copy()
            tail = out[:, L:]
            k = min(L, tail.shape[1])
            body[:, :k] += tail[:, :k]
            return body
        return out


def level(x, target_db):
    """Scale to a target RMS measured over the audible part of the signal."""
    mono = np.max(np.abs(x), axis=0)
    active = mono > 10 ** (-50 / 20) * (mono.max() + 1e-12)
    if not np.any(active):
        return x
    rms = np.sqrt(np.mean(x[:, active] ** 2))
    return x * (10 ** (target_db / 20) / (rms + 1e-12))


def duck(x, times, depth=0.55, release=0.16):
    """Sidechain-style ducking keyed to the given hit times."""
    return x * pump(x.shape[-1], times, depth, release)


def pump(n, times, depth=0.55, release=0.16):
    """Ducking envelope of n samples keyed to the given hit times."""
    env = np.ones(n)
    w = n_samples(release * 5)
    seg = np.exp(-np.arange(w) / (release * SR))
    for t0 in times:
        i = n_samples(t0)
        if i >= n:
            continue
        e = min(n, i + w)
        env[i:e] = np.minimum(env[i:e], 1 - depth * seg[:e - i])
    return env


# ============================================================================
# SOUL ALTAR  (loop) — slow drop-D doom/grunge with choir and trapped souls
# ============================================================================

def soul_altar(rng):
    """Drop-D doom/grunge loop at 66 BPM: tritone riff, choir, war drums, trapped-soul whispers, wailing lead in the second half."""
    s = Song(bpm=66, bars=16, tail=6.0)
    D, Eb, F, Ab, A, Bb = 38, 39, 41, 44, 45, 46
    b = s.beat

    def riff(bar0, variant):
        ev = []
        T = lambda bar, beat: s.t(bar0 + bar, beat)
        # Bar A1
        ev.append((T(0, 0), "chord", dict(root=D, len=1.9 * b)))
        ev.append((T(0, 2), "chord", dict(root=D, len=0.45 * b, palm=True, iv=(0, 7))))
        ev.append((T(0, 2.5), "chord", dict(root=D, len=0.45 * b, palm=True, iv=(0, 7))))
        ev.append((T(0, 3), "chord", dict(root=F, len=0.95 * b)))
        # Bar A2
        ev.append((T(1, 0), "chord", dict(root=Eb, len=1.95 * b)))
        ev.append((T(1, 2), "chord", dict(root=D, len=1.95 * b)))
        # Bar B1
        ev.append((T(2, 0), "chord", dict(root=D, len=1.9 * b)))
        ev.append((T(2, 2), "chord", dict(root=Bb, len=0.95 * b)))
        ev.append((T(2, 3), "chord", dict(root=A, len=0.95 * b)))
        # Bar B2 — the tritone
        ev.append((T(3, 0), "chord", dict(root=Ab, len=1.95 * b, iv=(0, 6, 12))))
        if variant == 0:
            for k in range(4):
                ev.append((T(3, 2 + k * 0.5), "chord",
                           dict(root=D, len=0.45 * b, palm=True, iv=(0, 7), vel=0.8 + 0.07 * k)))
        else:
            ev.append((T(3, 2), "chord", dict(root=D, len=1.9 * b, iv=(0, 7, 12))))
        return ev

    verse = riff(0, 0) + riff(4, 1)
    heavy = riff(8, 0) + riff(12, 1)

    # Intro half: a single grungy take, narrow. Second half: full double-tracked wall.
    g1 = amp(guitar_bus(verse, s.dur, rng), gain=16, mid_db=6, scoop_db=-2)
    s.bus("gtr_verse").add(haas(g1, 9, 0.0))
    s.bus("gtr_heavy").add(double_tracked_guitar(heavy, s.dur, rng, gain=30))

    roots = [D, Eb, D, Ab] * 4
    chords = {D: [50, 57, 65], Eb: [51, 58, 67], Ab: [50, 56, 59]}
    for bar in range(16):
        r = roots[bar]
        # Sub + bass
        s.bus("bass").add(sub_bass(mtof(r - 12), 4 * b) * env_adsr(4 * b, 0.02, 0.1, 0.9, 0.2), s.t(bar))
        if bar >= 8:
            s.bus("bass").add(synth_bass(mtof(r), 4 * b, rng, cutoff=500, decay=0.6) * 0.4, s.t(bar))
        # Choir
        vowel = "u" if bar < 8 else "a"
        c = choir(chords[r], 4 * b + 0.4, rng, vowel, attack=0.5, release=0.6, breath=0.2)
        s.bus("choir").add(widen(stereo(c), 1.5), s.t(bar))

    # Drums
    for bar in range(16):
        s.bus("drums").add(war_drum(rng, f=55, decay=1.4), s.t(bar, 0), 1.0, -0.15)
        s.bus("drums").add(war_drum(rng, f=60, decay=1.0), s.t(bar, 2), 0.7, 0.15)
        if bar >= 8:
            for bt in (0, 1.5, 2.75):
                s.bus("drums").add(kick(rng, decay=0.5), s.t(bar, bt), 0.9)
            s.bus("drums").add(snare(rng, decay=0.3), s.t(bar, 2), 0.8)
            for k in range(8):
                s.bus("drums").add(hat(rng, open_=(k == 7)), s.t(bar, k * 0.5), 0.25, 0.3)
            if bar % 4 == 0:
                s.bus("drums").add(crash(rng, 4.0, decay=1.6), s.t(bar), 0.45, -0.3)
        elif bar % 2 == 1:
            s.bus("drums").add(ride(rng), s.t(bar, 3), 0.2, 0.4)
    s.bus("drums").add(crash(rng, 4.0, decay=1.6), 0, 0.35, 0.3)
    # Fill into the loop point
    for k in range(4):
        s.bus("drums").add(tom(rng, f=140 - k * 18), s.t(15, 2 + k * 0.5), 0.8, 0.4 - k * 0.25)

    # Wailing lead over the second half
    lead_ev = []
    phrases = [
        (12, 0, 2.0, [(0, 67), (0.25, 69)]),
        (12, 2, 2.0, [(0, 70), (1.2, 70), (1.6, 69)]),
        (13, 0, 4.0, [(0, 67), (2.5, 67), (3.5, 65)]),
        (14, 0, 3.5, [(0, 74), (0.3, 74)]),
        (15, 0, 3.5, [(0, 73), (1.5, 73), (2.0, 74)]),
    ]
    for bar, beat, beats, curve in phrases:
        length = beats * b
        freq = exp_ramp([(t * b, mtof(m)) for t, m in curve] + [(length, mtof(curve[-1][1]))], length)
        lead_ev.append((s.t(bar, beat), "lead", dict(freq=freq, len=length, vib=35)))
    lead = amp(guitar_bus(lead_ev, s.dur, rng), gain=22, mid_db=7, fizz=6500)
    s.bus("lead").add(reverb(delay(lead, 3 * b / 4, 0.35, 0.3, 3000), 0.35, rt60=3.0)[:, :n_samples(s.dur)])

    # Trapped souls
    for bar in (1, 5, 9, 13):
        w = whisper(3.0, rng, flutter=10)
        s.bus("souls").add(pan(w, np.linspace(-0.8, 0.8, len(w))), s.t(bar, 1))
    drone = soul_tone(mtof(74), s.length, rng, 0.7)
    s.bus("souls").add(drone)
    s.bus("souls").add(soul_tone(mtof(62), s.dur, rng, 0.5), 0, 0.5)

    # Space
    for name, mix, rt in (("choir", 0.55, 3.8), ("souls", 0.6, 4.5), ("drums", 0.25, 2.5),
                          ("gtr_verse", 0.2, 2.0)):
        s.buses[name].buf = reverb(s.buses[name].buf, mix, rt60=rt)
    levels = dict(drums=-15, gtr_verse=-20, gtr_heavy=-17.5, bass=-20, choir=-22, lead=-20,
                  souls=-27)
    return s.mixdown(levels, loop=True)


# ============================================================================
# DEVIL'S DEAL — intro riff (one shot)
# ============================================================================

def devils_deal(rng):
    """Tritone choir swell into a high-gain E riff, sealed by an E/Bb chord, pinch-harmonic squeal and infernal choir."""
    s = Song(bpm=96, bars=4, tail=7.0)
    b = s.beat
    E, F, Fs, G, A, Bb = 40, 41, 42, 43, 45, 46
    t0 = 2.5  # riff downbeat after the swell

    # The pact is offered: low tritone choir, whispers, reverse cymbal.
    c = choir([40, 46], 3.0, rng, "o", attack=1.8, release=0.6, breath=0.3, vib_depth=25)
    s.bus("choir").add(widen(stereo(c), 1.5), 0)
    s.bus("fx").add(haas(whisper(2.5, rng, flutter=12), 15), 0.1)
    s.bus("fx").add(reverse_cymbal(rng, t0), 0, 0.8, tail=False)

    T = lambda bar, beat: t0 + (bar * 4 + beat) * b
    ev = [
        (T(0, 0), "chord", dict(root=E, len=1.45 * b)),
        (T(0, 1.5), "chord", dict(root=E, len=0.22 * b, palm=True, iv=(0, 7))),
        (T(0, 1.75), "chord", dict(root=E, len=0.22 * b, palm=True, iv=(0, 7))),
        (T(0, 2), "chord", dict(root=Bb, len=0.72 * b)),
        (T(0, 2.75), "chord", dict(root=A, len=0.47 * b)),
        (T(0, 3.25), "chord", dict(root=E, len=0.22 * b, palm=True, iv=(0, 7))),
        (T(0, 3.5), "chord", dict(root=E, len=0.22 * b, palm=True, iv=(0, 7))),
        (T(0, 3.75), "chord", dict(root=E, len=0.22 * b, palm=True, iv=(0, 7))),
        (T(1, 0), "chord", dict(root=G, len=0.95 * b)),
        (T(1, 1), "chord", dict(root=Fs, len=0.47 * b)),
        (T(1, 1.5), "chord", dict(root=F, len=0.47 * b)),
        (T(1, 2), "chord", dict(root=E, len=8 * b, iv=(0, 6, 12), t60=6.0)),  # the deal is sealed
    ]
    s.bus("gtr").add(double_tracked_guitar(ev, s.dur, rng, gain=32))
    # Pinch-harmonic squeal over the final chord
    sq_len = 3.0
    sq = exp_ramp([(0, mtof(83)), (0.12, mtof(83)), (0.3, mtof(86)), (sq_len, mtof(86))], sq_len)
    lead = amp(guitar_bus([(T(1, 2.2), "lead", dict(freq=sq, len=sq_len, vib=45, rel=1.0))],
                          s.dur, rng), gain=26, fizz=7000)
    s.bus("lead").add(reverb(lead, 0.4, rt60=3.0)[:, :n_samples(s.dur)])

    for bar, beat in ((0, 0), (0, 2), (0, 2.75), (1, 0), (1, 1), (1, 1.5)):
        s.bus("drums").add(kick(rng), T(bar, beat), 1.0)
    for bar, beat in ((0, 1.5), (0, 1.75), (0, 3.25), (0, 3.5), (0, 3.75)):
        s.bus("drums").add(kick(rng, decay=0.25), T(bar, beat), 0.7)
    s.bus("drums").add(snare(rng), T(0, 2), 0.8)
    s.bus("drums").add(snare(rng), T(1, 1), 0.8)
    s.bus("drums").add(snare(rng), T(1, 1.5), 0.9)
    s.bus("drums").add(crash(rng, 4.0), T(0, 0), 0.6, -0.3)
    s.bus("drums").add(impact(rng, 1.2, 6.0), T(1, 2), 0.9)
    s.bus("drums").add(crash(rng, 5.0, decay=2.0), T(1, 2), 0.6, 0.3)
    s.bus("drums").add(boom(rng, dur=4.0), T(0, 0), 0.6)

    final = choir([40, 46, 52, 58], 6.0, rng, "a", attack=0.05, release=3.0, breath=0.2)
    s.bus("choir").add(widen(stereo(final), 1.6), T(1, 2))
    s.bus("bass").add(sub_bass(mtof(28), 5.0) * env_adsr(5.0, 0.01, 0.2, 0.8, 3.0), T(1, 2))
    s.bus("bass").add(sub_bass(mtof(28), 2.5 * b) * env_adsr(2.5 * b, 0.01, 0.1, 0.9, 0.2), T(0, 0))
    horn = brass(mtof(28), 5.0, rng, bright=800, voices=4, detune=20, growl=0.8,
                 attack=0.1, release=2.5)
    s.bus("fx").add(haas(saturate(horn, 2.0), 12), T(1, 2), 0.7)

    for name, mix, rt in (("choir", 0.5, 4.0), ("drums", 0.25, 3.0), ("fx", 0.4, 3.5)):
        s.buses[name].buf = reverb(s.buses[name].buf, mix, rt60=rt)
    levels = dict(gtr=-16.5, drums=-15, choir=-21, lead=-21, bass=-20, fx=-21)
    return s.mixdown(levels, loop=False)


# ============================================================================
# PRE-BATTLE (loop) — tension builder: taiko, chugs, arp, horns, build to the drop
# ============================================================================

def pre_battle(rng):
    """110 BPM tension builder: taiko bed, synth arp, palm-muted chugs, horns, then a snare-roll build that loops back to the top."""
    s = Song(bpm=110, bars=16, tail=5.0)
    b = s.beat
    E, G, Fs, C, D, B = 40, 43, 42, 36 + 12, 38 + 12, 35 + 12
    prog = ["Em", "Em", "C", "D"] * 4
    arps = {"Em": [52, 55, 59, 64], "C": [48, 52, 55, 60], "D": [50, 54, 57, 62]}
    bass_root = {"Em": 28, "C": 24, "D": 26}

    kick_times = []
    # Taiko bed (all 16 bars)
    for bar in range(16):
        for beat, vel, f in ((0, 1.0, 54), (1.5, 0.7, 60), (2, 0.9, 56), (3, 0.6, 62), (3.5, 0.8, 58)):
            s.bus("taiko").add(war_drum(rng, f=f, decay=1.1), s.t(bar, beat), vel,
                               rng.uniform(-0.4, 0.4))
            kick_times.append(s.t(bar, beat))

    # Synth arp, filter opening over the loop
    pattern = [0, 1, 2, 3, 2, 1, 2, 3, 0, 1, 2, 3, 2, 3, 2, 1]
    for bar in range(16):
        notes = arps[prog[bar]]
        for k in range(16):
            peak = 1200 + 5500 * (bar / 15) ** 1.5
            p = synth_pluck(mtof(notes[pattern[k]] + 12), 0.35, rng, peak=peak, decay=0.09)
            s.bus("arp").add(p, s.t(bar, k * 0.25), 0.8)
        s.bus("pad").add(synth_pad([bass_root[prog[bar]] + 12, bass_root[prog[bar]] + 19],
                                   4 * b + 0.3, rng, attack=0.3, release=0.4, cutoff=900), s.t(bar))
        s.bus("bass").add(sub_bass(mtof(bass_root[prog[bar]]), 4 * b)
                          * env_adsr(4 * b, 0.02, 0.1, 0.9, 0.1), s.t(bar))

    # Guitars
    ev = []
    for bar in range(4, 8):  # palm-muted chug intro
        for k in range(8):
            accent = k in (0, 5)
            ev.append((s.t(bar, k * 0.5), "chord",
                       dict(root=E, len=0.45 * b, palm=not accent, iv=(0, 7) if not accent else (0, 7, 12),
                            vel=1.0 if accent else 0.75)))
    riff_a = [(0, E, 0.45, True), (0.5, E, 0.45, True), (1, E, 0.45, True), (1.5, G, 0.95, False),
              (2.5, Fs, 0.45, False), (3, E, 0.45, True), (3.5, E, 0.45, True)]
    riff_b = [(0, E, 0.45, True), (0.5, E, 0.45, True), (1, C - 12, 0.95, False),
              (2, B - 12, 1.9, False)]
    for bar in range(8, 12):
        for beat, root, ln, palm in (riff_a if bar % 2 == 0 else riff_b):
            ev.append((s.t(bar, beat), "chord", dict(root=root, len=ln * b, palm=palm)))
    for bar in range(12, 16):  # tremolo build
        for k in range(16):
            ev.append((s.t(bar, k * 0.25), "chord",
                       dict(root=E, len=0.22 * b, palm=True, iv=(0, 7), vel=0.6 + 0.4 * (bar - 12 + k / 16) / 4)))
    s.bus("gtr").add(double_tracked_guitar(ev, s.dur, rng, gain=30))

    # Horns
    for bar, midi, beats in ((4, 52, 8), (6, 48, 4), (7, 50, 4), (12, 52, 4), (13, 55, 4),
                             (14, 57, 4), (15, 59, 4)):
        h = brass(mtof(midi), beats * b, rng, bright=2800, voices=4, attack=0.2, release=0.4)
        h += brass(mtof(midi - 12), beats * b, rng, bright=1400, voices=3, attack=0.25, release=0.4) * 0.6
        s.bus("horns").add(haas(h, 11), s.t(bar))

    # Choir in the heavy section
    for bar, ch in zip(range(8, 12), ([52, 59, 64], [48, 55, 64], [52, 59, 64], [47, 54, 63])):
        s.bus("choir").add(widen(stereo(choir(ch, 4 * b + 0.3, rng, "a", attack=0.3, release=0.5)), 1.5),
                           s.t(bar))

    # Kit
    for bar in range(8, 16):
        for beat in (0, 1.5, 2.5):
            s.bus("kit").add(kick(rng), s.t(bar, beat), 1.0)
            kick_times.append(s.t(bar, beat))
        if bar < 12:
            for beat in (1, 3):
                s.bus("kit").add(snare(rng), s.t(bar, beat), 0.9)
            for k in range(8):
                s.bus("kit").add(hat(rng), s.t(bar, k * 0.5), 0.3, 0.3)
    # Snare build: 8ths -> 16ths -> 32nds with crescendo
    for bar, div in ((12, 2), (13, 2), (14, 4), (15, 8)):
        for k in range(4 * div):
            prog_t = (bar - 12 + k / (4 * div)) / 4
            s.bus("kit").add(snare(rng, decay=0.15), s.t(bar, k / div), 0.35 + 0.6 * prog_t,
                             rng.uniform(-0.15, 0.15))
    s.bus("kit").add(crash(rng, 4.0), 0, 0.6, -0.3)
    s.bus("kit").add(crash(rng, 4.0), s.t(8), 0.6, 0.3)
    s.bus("fx").add(riser(rng, 8 * b, 300, 9000), s.t(14), tail=False)
    s.bus("fx").add(reverse_cymbal(rng, 4 * b), s.t(15), 0.8, tail=False)
    s.bus("fx").add(impact(rng, 0.8, 3.0), s.t(8), 0.7)

    for name in ("arp", "pad"):
        s.buses[name].buf = duck(s.buses[name].buf, kick_times, depth=0.5)
    for name, mix, rt in (("arp", 0.3, 2.0), ("horns", 0.45, 3.2), ("choir", 0.5, 3.5),
                          ("taiko", 0.3, 2.8), ("kit", 0.2, 2.0), ("pad", 0.3, 3.0)):
        s.buses[name].buf = reverb(s.buses[name].buf, mix, rt60=rt)
    s.buses["arp"].buf = delay(s.buses["arp"].buf, 0.75 * b, 0.3, 0.2, 3500)
    levels = dict(taiko=-16, arp=-22, pad=-27, bass=-21, gtr=-18, horns=-21, choir=-23,
                  kit=-17, fx=-24)
    return s.mixdown(levels, loop=True)


# ============================================================================
# VS SCREEN (loop) — gallop riff, double kick, synth stabs
# ============================================================================

def vs_screen_loop(rng):
    """150 BPM gallop riff with double kick, sidechained synth stabs and a lead line."""
    s = Song(bpm=150, bars=8, tail=4.0)
    b = s.beat
    roots = [40, 40, 36, 38, 40, 40, 46, 47]
    ev = []
    kick_times = []
    for bar in range(8):
        r = roots[bar]
        for beat in range(4):
            for off, ln in ((0, 0.45), (0.5, 0.22), (0.75, 0.22)):
                t = s.t(bar, beat + off)
                if beat == 0 and off == 0:
                    ev.append((t, "chord", dict(root=r, len=0.95 * b)))
                elif beat == 0:
                    continue
                else:
                    ev.append((t, "chord", dict(root=40, len=ln * b, palm=True, iv=(0, 7),
                                                vel=0.9 if off == 0 else 0.75)))
        # Accent on beat 2.5
        ev.append((s.t(bar, 2.5), "chord", dict(root=r + 3 if bar % 2 else r, len=0.22 * b)))
    s.bus("gtr").add(double_tracked_guitar(ev, s.dur, rng, gain=34))

    for bar in range(8):
        r = roots[bar]
        for beat in range(4):
            if bar < 4:
                hits = (0, 0.5, 0.75)
            else:
                hits = (0, 0.25, 0.5, 0.75)
            for off in hits:
                s.bus("kit").add(kick(rng, decay=0.22, f0=170), s.t(bar, beat + off), 0.85)
                kick_times.append(s.t(bar, beat + off))
            if beat in (1, 3):
                s.bus("kit").add(snare(rng, decay=0.22), s.t(bar, beat), 1.0)
            s.bus("kit").add(hat(rng, open_=(beat == 3)), s.t(bar, beat + 0.5), 0.3, 0.3)
        if bar % 2 == 0:
            s.bus("kit").add(crash(rng, 3.0), s.t(bar), 0.55, -0.4 if bar % 4 else 0.4)
        # Synth bass 8ths + stabs
        for k in range(8):
            s.bus("bass").add(synth_bass(mtof(r - 12), 0.45 * b, rng, cutoff=1200, decay=0.08),
                              s.t(bar, k * 0.5))
        st = synth_pluck(mtof(r + 12), 0.6 * b, rng, peak=7000, decay=0.12, voices=7) + \
            synth_pluck(mtof(r + 19), 0.6 * b, rng, peak=7000, decay=0.12, voices=7)
        s.bus("synth").add(st, s.t(bar), 0.7)
        s.bus("synth").add(st, s.t(bar, 2.5), 0.5)

    # Lead melody over bars 4-7
    melody = [(4, 0, 64, 1.5), (4, 1.5, 67, 0.5), (4, 2, 66, 1), (4, 3, 64, 1),
              (5, 0, 71, 2), (5, 2, 70, 2),
              (6, 0, 70, 1.5), (6, 1.5, 69, 0.5), (6, 2, 67, 1), (6, 3, 66, 1),
              (7, 0, 64, 3), (7, 3, 63, 1)]
    for bar, beat, midi, ln in melody:
        l = synth_lead(mtof(midi + 12), ln * b, rng, cutoff=5000)
        l *= env_adsr(ln * b, 0.01, 0.1, 0.8, 0.08)
        s.bus("lead").add(haas(l, 8), s.t(bar, beat))

    s.bus("fx").add(reverse_cymbal(rng, 4 * b), s.t(7), 0.7, tail=False)

    s.buses["synth"].buf = duck(s.buses["synth"].buf, kick_times[::2], depth=0.45, release=0.1)
    for name, mix, rt in (("synth", 0.3, 1.8), ("kit", 0.15, 1.5), ("lead", 0.3, 2.0)):
        s.buses[name].buf = reverb(s.buses[name].buf, mix, rt60=rt)
    s.buses["lead"].buf = delay(s.buses["lead"].buf, 0.75 * b, 0.3, 0.2, 4000)
    levels = dict(gtr=-16.5, kit=-15.5, bass=-21, synth=-22, lead=-21, fx=-25)
    return s.mixdown(levels, loop=True)


def vs_screen_sting(rng):
    """Versus slam: swell -> huge hit with chord, choir, horns."""
    dur = 10.0
    hit = 1.4
    m_gtr, m_fx, m_choir, m_drums = Mix(dur), Mix(dur), Mix(dur), Mix(dur)
    m_fx.add(reverse_cymbal(rng, hit), 0, 0.9, tail=False)
    m_fx.add(riser(rng, hit, 400, 9000), 0, 0.7, tail=False)
    ev = [(hit, "chord", dict(root=40, len=6.0, t60=6.0))]
    m_gtr.add(double_tracked_guitar(ev, dur, rng, gain=32))
    m_drums.add(impact(rng, 1.3, 7.0), hit)
    m_drums.add(crash(rng, 5.0, decay=2.0), hit, 0.6)
    m_choir.add(widen(stereo(choir([40, 47, 52, 55, 59], 4.0, rng, "a", attack=0.03,
                                   release=2.5)), 1.6), hit)
    h = brass(mtof(40), 3.5, rng, bright=2500, voices=4, attack=0.03, release=2.0) + \
        brass(mtof(28), 3.5, rng, bright=700, voices=4, growl=0.7, attack=0.05, release=2.0)
    m_fx.add(haas(h, 12), hit, 0.6)
    out = (level(reverb(m_drums.out, 0.25, rt60=3.0)[:, :n_samples(dur)], -14)
           + level(m_gtr.out[:, :n_samples(dur)], -16)
           + level(reverb(m_choir.out, 0.5, rt60=4.0)[:, :n_samples(dur)], -20)
           + level(reverb(m_fx.out, 0.35, rt60=3.0)[:, :n_samples(dur)], -20))
    return out


MUSIC = [
    ("Soul Altar", "Music_SoulAltar_Loop", soul_altar, True),
    ("Devils Deal", "Music_DevilsDeal_IntroRiff", devils_deal, False),
    ("Pre Battle", "Music_PreBattle_Loop", pre_battle, True),
    ("VS Screen", "Music_VSScreen_Loop", vs_screen_loop, True),
    ("VS Screen", "Music_VSScreen_Sting", vs_screen_sting, False),
]
