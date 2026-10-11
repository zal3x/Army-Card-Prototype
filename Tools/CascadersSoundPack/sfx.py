"""One-shot sound effect designs. Each function takes an rng and returns stereo audio."""
import numpy as np

from dsp import (SR, n_samples, t_axis, ramp, exp_ramp, env_perc, env_adsr, mtof, cents,
                 osc_sine, osc_saw, osc_pulse, supersaw, fm, white, pink, brown,
                 smooth_random, lp, hp, bp, eq, sweep, saturate, fold, bitcrush, pan,
                 stereo, widen, haas, reverb, delay, reverse, Mix, normalize, pad_to)
from instruments import (kick, snare, tom, crash, war_drum, boom, impact, reverse_cymbal,
                         riser, whoosh, power_chord, amp, lead_tone, synth_pluck, synth_pad, sub_bass,
                         synth_bass, bell, fm_bell, coin, sparkle, brass, choir, whisper,
                         ghost_wail, soul_tone, bubble, gurgle_bed, ks_string)


# ============================================================================
# WAR DRUMS
# ============================================================================

def _drum_hit(m, rng, t, f=None, vel=1.0, p=None):
    f = f or rng.uniform(52, 64)
    m.add(war_drum(rng, f=f, decay=rng.uniform(1.0, 1.4), slap=rng.uniform(0.35, 0.6)),
          t + rng.normal(0, 0.004), vel * rng.uniform(0.9, 1.0),
          rng.uniform(-0.25, 0.25) if p is None else p)


def war_drums_march(rng):
    """Steady 4-bar march: BOOM . BOOM . BOOM boom BOOM ."""
    m = Mix(9)
    beat = 60 / 92
    pattern = [0, 1, 2, 2.5, 3]
    for bar in range(3):
        for b in pattern:
            _drum_hit(m, rng, (bar * 4 + b) * beat, vel=1.0 if b in (0, 2) else 0.7)
        # rim/stick clicks on the off-beats
        for b in (0.5, 1.5, 3.5):
            t0 = (bar * 4 + b) * beat
            m.add(hp(white(0.05, rng), 2500) * env_perc(0.05, 0.0005, 0.008), t0, 0.25,
                  rng.uniform(-0.5, 0.5))
    _drum_hit(m, rng, 12 * beat, f=48, vel=1.2)
    m.add(boom(rng, dur=3.0), 12 * beat, 0.6)
    return reverb(m.out, 0.35, rt60=2.6, predelay=0.03)


def war_drums_charge(rng):
    """Accelerating charge roll ending on a huge unison hit."""
    m = Mix(8)
    t, gap = 0.0, 0.62
    while gap > 0.075:
        _drum_hit(m, rng, t, vel=0.55 + 0.45 * (0.62 - gap) / 0.55, p=rng.choice([-0.4, 0.4]))
        t += gap
        gap *= 0.88
    for p in (-0.5, 0, 0.5):
        _drum_hit(m, rng, t + 0.1, f=rng.uniform(46, 52), vel=1.0, p=p)
    m.add(boom(rng, dur=3.5), t + 0.1, 0.8)
    m.add(crash(rng, 3.0, decay=1.2), t + 0.1, 0.25)
    return reverb(m.out, 0.35, rt60=2.8)


def war_drums_ritual(rng):
    """Slow tribal call-and-response between low and high drums."""
    m = Mix(9)
    beat = 60 / 70
    lows = [0, 1.5, 4, 5.5, 8, 9.5]
    highs = [2.5, 3, 6.5, 7, 10.5, 11, 11.5]
    for b in lows:
        _drum_hit(m, rng, b * beat, f=rng.uniform(48, 55), vel=1.0, p=-0.3)
    for b in highs:
        m.add(tom(rng, f=rng.uniform(150, 175), decay=0.35), b * beat, 0.55, 0.35)
    _drum_hit(m, rng, 12 * beat, f=46, vel=1.2)
    m.add(boom(rng, dur=3.0), 12 * beat, 0.5)
    return reverb(m.out, 0.4, rt60=3.2, predelay=0.04)


def war_drums_ensemble_hits(rng):
    """Three massive ensemble hits with grit — punctuation for big moments."""
    m = Mix(7)
    for i, t in enumerate((0.0, 0.9, 1.8)):
        for p in (-0.6, -0.2, 0.2, 0.6):
            _drum_hit(m, rng, t, f=rng.uniform(50, 60) - i * 3, vel=0.8 + 0.15 * i, p=p)
        m.add(kick(rng, f0=120, f1=40, decay=0.6), t, 0.5)
    m.add(boom(rng, dur=4.0), 1.8, 0.9)
    m.add(crash(rng, 3.5, decay=1.6), 1.8, 0.3)
    m.add(reverse_cymbal(rng, 1.7), 0.1, 0.25, tail=False)
    return reverb(m.out, 0.3, rt60=3.0)


# ============================================================================
# BATTLE HORNS
# ============================================================================

def _troll(rng, notes, root=36, bright=900, growl=0.9, dur_total=None):
    m = Mix(dur_total or 6)
    for t0, semis, length in notes:
        f = mtof(root + semis)
        sag = ramp([(0, 0), (length * 0.75, 0), (length, -120)], length)
        s = brass(f * cents(sag), length, rng, bright=bright, voices=4, detune=18,
                  growl=growl, breath=0.3, attack=0.15, release=0.6, scoop=-200)
        s += 0.5 * sub_bass(f * cents(sag) * 0.5, length) * env_adsr(length, 0.2, 0.1, 0.8, 0.5)
        # grit: noisy throat rattle
        rattle = bp(white(length, rng), 200, 900) * (0.5 + 0.5 * smooth_random(length, rng, 28))
        s += rattle * env_adsr(length, 0.2, 0.1, 0.6, 0.5) * 0.25
        s = saturate(s, 2.5)
        s = eq(s, "peak", 220, 0.8, 4)
        m.add(haas(s, 14), t0)
    return reverb(m.out, 0.45, rt60=3.5, predelay=0.05, damping=0.35)


def horn_troll_1(rng):
    """One long, gravelly, bowel-shaking blast."""
    return _troll(rng, [(0.0, 0, 3.6)])


def horn_troll_2(rng):
    """Two-note troll call dropping a tritone."""
    return _troll(rng, [(0.0, 3, 1.4), (1.5, -3, 2.8)], root=38, bright=1100)


def _goblin(rng, notes, root=74, total=4.5):
    m = Mix(total)
    for t0, semis, length in notes:
        n = n_samples(length)
        t = t_axis(length)
        f = mtof(root + semis) * cents(60 * smooth_random(length, rng, 7)
                                       + 35 * np.sin(2 * np.pi * 9.5 * t))
        # random squeaky cracks up toward the octave
        crack = np.zeros(n)
        for _ in range(int(length * 2)):
            c0 = rng.uniform(0.1, max(0.11, length - 0.1))
            crack += 900 * np.exp(-((t - c0) / 0.03) ** 2)
        f = f * cents(crack) * cents(-150 * np.exp(-t / 0.04))
        src = osc_pulse(f, length, 0.22) * 0.6 + osc_saw(f * cents(14), length) * 0.4
        s = eq(src, "peak", 1400, 2.5, 10)
        s = eq(s, "peak", 2900, 3.0, 6)
        s = lp(hp(s, 350), 7000)
        s = bitcrush(saturate(s, 3.0), bits=10, hold=2)
        s *= env_adsr(length, 0.02, 0.05, 0.85, 0.08)
        m.add(s, t0, 1.0, rng.uniform(-0.35, 0.35))
        # an out-of-tune second goblin a beat behind
        s2 = osc_pulse(f * cents(rng.uniform(25, 45)), length, 0.3)
        s2 = lp(eq(s2, "peak", 1200, 2.0, 8), 5000) * env_adsr(length, 0.03, 0.05, 0.7, 0.1)
        m.add(saturate(s2, 2.5), t0 + 0.035, 0.45, rng.uniform(-0.7, 0.7))
    return reverb(m.out, 0.25, rt60=1.4, predelay=0.015)


def horn_goblin_1(rng):
    """Toot-toot-tooooot: cheeky, nasal, wobbly."""
    return _goblin(rng, [(0.0, 0, 0.22), (0.3, 0, 0.22), (0.6, 5, 1.6)])


def horn_goblin_2(rng):
    """Frantic ascending squeal call."""
    return _goblin(rng, [(0.0, -5, 0.15), (0.18, -2, 0.15), (0.36, 0, 0.15),
                         (0.54, 3, 0.15), (0.72, 7, 1.3)], root=76)


def _human(rng, notes, root=50, total=6.0):
    m = Mix(total)
    for t0, semis, length in notes:
        f = mtof(root + semis)
        s = brass(f, length, rng, bright=3200, voices=4, detune=10, breath=0.2,
                  attack=0.07, release=0.5)
        s_low = brass(f / 2, length, rng, bright=1500, voices=3, detune=8, breath=0.1,
                      attack=0.09, release=0.5)
        m.add(haas(s + 0.6 * s_low, 11), t0)
    return reverb(m.out, 0.5, rt60=3.8, predelay=0.06, damping=0.5)


def horn_human_1(rng):
    """Noble rallying call: root - fifth - octave."""
    return _human(rng, [(0.0, 0, 0.55), (0.6, 7, 0.55), (1.2, 12, 2.6)])


def horn_human_2(rng):
    """Long war-horn swell with a mournful minor fall."""
    return _human(rng, [(0.0, 0, 2.0), (2.05, 3, 0.7), (2.8, 0, 2.4)], root=45, total=7.0)


# ============================================================================
# MYSTERIOUS SOUL AMBIENCES
# ============================================================================

def soul_mystery_ethereal_choir(rng):
    """Floating 'oo' choir cluster with glass partials."""
    dur = 5.0
    c = choir([62, 63, 69, 74], dur, rng, vowel_seq=[(0, "u"), (2.5, "o"), (5, "u")],
              attack=1.4, release=1.8, breath=0.2, vib_depth=12)
    st = soul_tone(mtof(86), dur, rng) * env_adsr(dur, 1.5, 0.2, 1.0, 1.5)
    return reverb(widen(stereo(c) + st, 1.6), 0.6, rt60=4.5, predelay=0.05, damping=0.6)


def soul_mystery_whispers(rng):
    """A crowd of whispering souls drifting across the stereo field."""
    dur = 4.5
    m = Mix(dur)
    for _ in range(6):
        d = rng.uniform(1.2, 2.5)
        seq = [(0, rng.choice(list("aeiou"))), (d * 0.5, rng.choice(list("aeiou"))),
               (d, rng.choice(list("aeiou")))]
        w = whisper(d, rng, vowel_seq=seq, flutter=rng.uniform(6, 12))
        p0 = rng.uniform(-1, 1)
        m.add(pan(w, np.linspace(p0, -p0 * 0.5, len(w))), rng.uniform(0, dur - d), 0.7)
    bed = soul_tone(mtof(57), dur, rng, 0.6) * env_adsr(dur, 1.0, 0.2, 1, 1.5) * 0.6
    m.add(bed)
    return reverb(m.out, 0.55, rt60=3.5, predelay=0.04)


def soul_mystery_wail(rng):
    """A single lost soul wailing up and drifting down."""
    dur = 4.0
    f = exp_ramp([(0, 330), (0.8, 523), (1.6, 494), (2.6, 392), (4.0, 262)], dur)
    w = ghost_wail(f, dur, rng, vowel_seq=[(0, "u"), (1.0, "o"), (2.5, "a"), (4, "u")])
    w *= env_adsr(dur, 0.5, 0.2, 0.9, 1.4)
    echo = delay(w * 0.6, 0.37, 0.45, 0.4, lp_f=2500)
    return reverb(echo, 0.6, rt60=4.0, predelay=0.03, damping=0.5)


def soul_mystery_glass(rng):
    """Shimmering glass harmonica tones beating against each other."""
    dur = 5.0
    m = Mix(dur)
    for i, midi in enumerate((74, 77, 81, 80)):
        t0 = i * 0.8
        d = dur - t0
        m.add(soul_tone(mtof(midi), d, rng, 0.8) * env_adsr(d, 0.6, 0.2, 0.9, 1.6), t0, 0.8)
    m.add(sparkle(rng, 4.0, density=6, f_lo=3000, f_hi=8000), 0.5, 0.4)
    return reverb(m.out, 0.55, rt60=4.0)


def soul_mystery_moan(rng):
    """Low spectral moan: tritone choir through a slow filter sweep."""
    dur = 5.0
    c = choir([38, 44, 50], dur, rng, vowel_seq=[(0, "o"), (2.5, "a"), (5, "u")],
              attack=1.2, release=1.6, breath=0.25, vib_depth=22,
              pitch=ramp([(0, 0), (3.5, 0), (5, -150)], dur))
    c = sweep(stereo(c), "lowpass", exp_ramp([(0, 500), (2.5, 3500), (5, 600)], dur), q=2.5)
    rumble = lp(brown(dur, rng), 120) * env_adsr(dur, 1.0, 0.2, 1, 1.5)
    return reverb(widen(c, 1.4) + stereo(rumble) * 0.6, 0.5, rt60=4.0)


def soul_mystery_reverse_swell(rng):
    """Reverse-reverb ghost swell that sucks into silence."""
    dur = 1.5
    hit = bell(mtof(69), dur, rng, decay=0.8) + choir([57, 64], dur, rng, "a", attack=0.01,
                                                     release=0.8)
    wet = reverb(hit, 1.0, rt60=3.0)
    rev = reverse(wet)
    rev = rev * np.interp(np.linspace(0, 1, rev.shape[1]), [0, 0.8, 1], [0, 0.7, 1])
    m = Mix(rev.shape[1] / SR + 2.0)
    m.add(rev, tail=False)
    tail = soul_tone(mtof(81), 2.0, rng) * env_perc(2.0, 0.005, 0.6)
    m.add(tail, rev.shape[1] / SR - 0.02, 0.8)
    return reverb(m.out, 0.2, rt60=2.0)


def soul_mystery_hum(rng):
    """Seamless-ish dark drone of souls trapped in an altar (good for looping)."""
    dur = 6.0
    t = t_axis(dur)
    c = choir([45, 52, 57], dur, rng, "u", attack=1.5, release=1.5, breath=0.3, vib_depth=8)
    drone = supersaw(mtof(33), dur, rng, 5, 10)
    drone = sweep(drone, "lowpass", 250 + 200 * (1 + np.sin(2 * np.pi * 0.25 * t)), q=3)
    st = soul_tone(mtof(69), dur, rng, 0.9) * 0.6
    mix = stereo(c) + drone * 0.5 + st
    return reverb(mix * env_adsr(dur, 1.2, 0.2, 1, 1.5), 0.5, rt60=4.0)


# ============================================================================
# SOUL TRANSACTIONS
# ============================================================================

def _suction(rng, dur, f_hi=6000, f_lo=200):
    """Filtered noise sweeping downward with resonance: the 'pull'."""
    t = t_axis(dur)
    f = exp_ramp([(0, f_hi), (dur, f_lo)], dur)
    s = sweep(white(dur, rng), "bandpass", f, q=4.0) * 2.0
    env = ramp([(0, 0), (dur * 0.2, 0.7), (dur * 0.85, 1.0), (dur, 0)], dur)
    return s * env


def soul_taken_1(rng):
    """Soul ripped out: shriek pitching down, sucked into a dark thud."""
    m = Mix(4)
    d = 1.3
    m.add(reverse_cymbal(rng, 0.6), 0, 0.3, tail=False)
    wail = ghost_wail(exp_ramp([(0, 880), (d, 110)], d), d, rng,
                      vowel_seq=[(0, "a"), (d * 0.5, "o"), (d, "u")], vib=50)
    m.add(pan(wail * env_adsr(d, 0.05, 0.1, 0.9, 0.3), np.linspace(-0.6, 0.6, n_samples(d))), 0.3)
    m.add(stereo(_suction(rng, d)), 0.3, 0.5, tail=False)
    m.add(boom(rng, 80, 30, 1.0, 2.0), 0.3 + d - 0.05, 0.9)
    m.add(kick(rng, 120, 40, 0.4), 0.3 + d - 0.05, 0.5)
    return reverb(m.out, 0.4, rt60=2.6)


def soul_taken_2(rng):
    """Vacuum slurp: whispers stretched and pulled down a drain."""
    m = Mix(3.5)
    d = 1.5
    w = whisper(d, rng, flutter=14)
    w = sweep(w, "lowpass", exp_ramp([(0, 8000), (d, 300)], d), q=3.0)
    m.add(pan(w, np.linspace(0.8, 0, n_samples(d))), 0, 1.2)
    tone = osc_saw(exp_ramp([(0, 660), (d, 55)], d), d) * env_adsr(d, 0.1, 0.1, 0.8, 0.2)
    tone = sweep(tone, "lowpass", exp_ramp([(0, 4000), (d, 200)], d), q=6)
    m.add(stereo(saturate(tone, 2.0)), 0, 0.4)
    m.add(stereo(_suction(rng, d, 5000, 120)), 0, 0.6)
    m.add(boom(rng, 60, 28, 0.9, 1.8), d - 0.05, 0.8)
    return reverb(m.out, 0.35, rt60=2.2)


def soul_taken_3(rng):
    """Harsh distorted grab: guitar dive bomb + choir collapse."""
    m = Mix(3.5)
    d = 1.4
    dive = lead_tone(exp_ramp([(0, 659), (0.15, 659), (d, 41)], d), d, rng, vib_depth=0)
    g = amp(dive * env_adsr(d, 0.01, 0.1, 1.0, 0.2), gain=35)
    m.add(haas(g, 12), 0, 0.7)
    c = choir([62, 68], d, rng, "a", attack=0.02, release=0.3,
              pitch=ramp([(0, 0), (d, -1900)], d))
    m.add(stereo(c), 0, 0.6)
    m.add(impact(rng, 0.7, 3.5), d - 0.05, 0.6)
    return reverb(m.out, 0.3, rt60=2.4)


def soul_received_1(rng):
    """Ascending ghostly arpeggio blooming into a choir 'ah'."""
    m = Mix(4)
    notes = [57, 60, 64, 69, 72, 76]
    for i, n in enumerate(notes):
        m.add(bell(mtof(n + 12), 1.5, rng, decay=0.6), i * 0.07, 0.6, -0.6 + i * 0.24)
    c = choir([57, 64, 69, 72], 2.4, rng, "a", attack=0.25, release=1.4, breath=0.15)
    m.add(widen(stereo(c), 1.5), 0.35, 0.8)
    m.add(soul_tone(mtof(81), 2.4, rng) * env_adsr(2.4, 0.3, 0.1, 1.0, 1.4), 0.35, 0.8)
    m.add(sub_bass(mtof(33), 1.0) * env_perc(1.0, 0.005, 0.35), 0.35, 0.6)
    m.add(sparkle(rng, 1.4, density=20), 0.3, 0.6)
    return reverb(m.out, 0.45, rt60=3.2)


def soul_received_2(rng):
    """Soul slams into you: whoosh in, bright hit, glittering tail."""
    m = Mix(4)
    m.add(whoosh(rng, 0.7, 400, 5000, peak=0.85, pan_from=-0.9, pan_to=0), 0, 1.0)
    t0 = 0.6
    m.add(fm_bell(mtof(76), 2.0, 2.0, 3.0, 1.0), t0, 0.5, 0)
    m.add(fm_bell(mtof(83), 2.0, 3.5, 2.0, 0.8), t0 + 0.01, 0.35, 0.3)
    m.add(kick(rng, 160, 50, 0.35), t0, 0.6)
    m.add(soul_tone(mtof(88), 2.5, rng, 0.8) * env_perc(2.5, 0.005, 0.9), t0, 1.0)
    m.add(sparkle(rng, 1.6, density=25, f_lo=3500), t0, 0.7)
    return reverb(m.out, 0.4, rt60=3.0)


def soul_received_3(rng):
    """Dark absorb: deep inhale swell, power chord + choir resolve."""
    m = Mix(5)
    d = 1.0
    inhale = sweep(pink(d, rng), "bandpass", exp_ramp([(0, 300), (d, 2500)], d), q=2)
    m.add(stereo(inhale * ramp([(0, 0), (d, 1)], d) ** 2), 0, 0.8, tail=False)
    m.add(reverse_cymbal(rng, d), 0, 0.3, tail=False)
    pc = amp(power_chord(45, 2.5, rng, mute_at=2.3), gain=24)
    m.add(haas(pc, 13), d, 0.5)
    c = choir([57, 64, 69], 2.5, rng, "o", attack=0.05, release=1.5)
    m.add(stereo(c), d, 0.7)
    m.add(soul_tone(mtof(81), 2.5, rng) * env_perc(2.5, 0.005, 1.0), d, 0.9)
    m.add(boom(rng, 70, 35, 1.0, 2.0), d, 0.5)
    return reverb(m.out, 0.4, rt60=3.0)


def soul_flying_1(rng):
    """Wispy soul zips left-to-right with a whistling Doppler tail."""
    d = 1.4
    m = Mix(d + 0.5)
    m.add(whoosh(rng, d, 300, 4500, 0.5, 1.5, -1, 1), 0, 1.2)
    f = mtof(84) * cents(ramp([(0, 250), (d * 0.5, 0), (d, -500)], d))
    whistle = osc_sine(f, d) * np.exp(-((t_axis(d) - d * 0.5) / (d * 0.22)) ** 2)
    m.add(pan(whistle, np.linspace(-1, 1, n_samples(d))), 0, 0.25)
    m.add(pan(soul_tone(mtof(76), d, rng)[0] * np.exp(-((t_axis(d) - d * 0.5) / (d * 0.25)) ** 2),
              np.linspace(-1, 1, n_samples(d))), 0, 1.0)
    return reverb(m.out, 0.35, rt60=2.0)


def soul_flying_2(rng):
    """Spirit swirl: circling whoosh with ghost voice and flutter."""
    d = 2.0
    m = Mix(d + 0.5)
    t = t_axis(d)
    circ = np.sin(2 * np.pi * 1.2 * t)
    w = whoosh(rng, d, 500, 3500, 0.5, 2.0, 0, 0)
    m.add(pan((w[0] + w[1]) / 2, circ), 0, 1.0)
    v = ghost_wail(exp_ramp([(0, 400), (d, 700)], d), d, rng)
    v *= np.exp(-((t - d * 0.5) / (d * 0.3)) ** 2)
    m.add(pan(v, circ * 0.8), 0, 0.5)
    return reverb(m.out, 0.45, rt60=2.5)


def soul_flying_3(rng):
    """Fast streak overhead — short, good for many souls at once."""
    d = 0.7
    m = Mix(d + 0.4)
    m.add(whoosh(rng, d, 600, 6000, 0.45, 2.5, -0.7, 0.7), 0, 1.2)
    s = osc_sine(exp_ramp([(0, 1500), (d, 600)], d), d)
    s *= np.exp(-((t_axis(d) - d * 0.45) / (d * 0.2)) ** 2)
    m.add(s, 0, 0.15)
    m.add(sparkle(rng, 0.5, density=30, f_lo=4000), 0.15, 0.4)
    return reverb(m.out, 0.3, rt60=1.6)


def soul_gurgle_1(rng):
    """Bubbling, choking soul drowning in ichor."""
    d = 2.5
    m = Mix(d + 0.5)
    m.add(gurgle_bed(d, rng, rate=45), 0, 0.8)
    v = choir([40], d, rng, "o", voices=2, attack=0.2, release=0.5, breath=0.4, detune=30)
    gargle = 0.5 + 0.5 * np.tanh(4 * np.sin(2 * np.pi * 22 * t_axis(d))) * \
        (0.5 + 0.5 * smooth_random(d, rng, 6))
    m.add(lp(v * gargle, 1500), 0, 0.8)
    return reverb(m.out, 0.25, rt60=1.4)


def soul_gurgle_2(rng):
    """Thick swampy gurgle with a groan underneath."""
    d = 3.0
    m = Mix(d + 0.5)
    m.add(gurgle_bed(d, rng, rate=28, f_lo=90, f_hi=400), 0, 1.0)
    groan = ghost_wail(exp_ramp([(0, 90), (d * 0.6, 110), (d, 70)], d), d, rng,
                       vowel_seq=[(0, "o"), (d, "u")], vib=40)
    am = 0.6 + 0.4 * smooth_random(d, rng, 18)
    m.add(stereo(lp(groan * am * env_adsr(d, 0.3, 0.1, 0.9, 0.6), 1200)), 0, 0.8)
    m.add(stereo(lp(brown(d, rng), 200) * env_adsr(d, 0.3, 0.1, 1, 0.6)), 0, 0.5)
    return reverb(m.out, 0.3, rt60=1.8)


def soul_gurgle_3(rng):
    """Short wet gurgle burst — good for frequent feedback."""
    d = 1.0
    m = Mix(d + 0.4)
    m.add(gurgle_bed(d, rng, rate=70, f_lo=200, f_hi=900), 0, 0.9)
    s = sweep(white(d, rng), "bandpass", 300 + 250 * smooth_random(d, rng, 25), q=6)
    m.add(stereo(s * env_adsr(d, 0.02, 0.1, 0.7, 0.3)), 0, 0.6)
    return reverb(m.out, 0.2, rt60=1.2)


# ============================================================================
# SKILLS
# ============================================================================

def skill_damage_1(rng):
    """Power-chord smash + impact."""
    m = Mix(3)
    pc = amp(power_chord(40, 1.6, rng, mute_at=1.2), gain=32)
    m.add(haas(pc, 12), 0, 0.7)
    m.add(kick(rng, 170, 45, 0.45), 0, 0.9)
    m.add(snare(rng, decay=0.25), 0, 0.5)
    m.add(boom(rng, 70, 32, 0.8, 1.6), 0, 0.6)
    m.add(crash(rng, 2.0, decay=0.8), 0, 0.25)
    return reverb(m.out, 0.25, rt60=1.8)


def skill_damage_2(rng):
    """Blade slash + metallic ring + crunch."""
    m = Mix(2.5)
    d = 0.25
    sl = sweep(white(d, rng), "bandpass", exp_ramp([(0, 1500), (d, 9000)], d), q=3)
    m.add(pan(sl * env_adsr(d, 0.01, 0.05, 0.8, 0.08), np.linspace(-0.8, 0.8, n_samples(d))), 0, 1.2)
    ring = sum(osc_sine(f, 1.5, rng.random()) for f in (1840, 2710, 3950)) * env_perc(1.5, 0.001, 0.4)
    m.add(ring * 0.25, d * 0.8, 1.0, 0.3)
    crunch = amp(white(0.15, rng) * env_perc(0.15, 0.001, 0.04), gain=10)
    m.add(haas(crunch, 8), d * 0.8, 0.5)
    m.add(kick(rng, 200, 60, 0.2), d * 0.8, 0.6)
    return reverb(m.out, 0.2, rt60=1.5)


def skill_damage_3(rng):
    """Synth bass zap with pitch drop into a distorted crash."""
    m = Mix(3)
    d = 0.6
    f = exp_ramp([(0, 880), (d, 40)], d)
    z = saturate(osc_saw(f, d) + osc_pulse(f * 1.01, d, 0.3), 4.0)
    z = sweep(z, "lowpass", exp_ramp([(0, 9000), (d, 300)], d), q=4)
    m.add(haas(z * env_adsr(d, 0.002, 0.05, 0.9, 0.1), 9), 0, 0.6)
    m.add(impact(rng, 0.6, 3.5), d * 0.7, 0.6)
    return reverb(m.out, 0.2, rt60=1.6)


def skill_buff_1(rng):
    """Rising power-up riser into a bright major power chord."""
    m = Mix(3.5)
    d = 0.9
    m.add(riser(rng, d, 400, 8000), 0, 0.5, tail=False)
    f = exp_ramp([(0, 110), (d, 440)], d)
    up = synth_pluck(f, d, rng, peak=2000, base=600 * 1, decay=10)
    m.add(up * ramp([(0, 0), (d, 1)], d), 0, 0.35)
    pc = amp(power_chord(45, 1.8, rng, intervals=(0, 7, 12, 16), mute_at=1.6), gain=22)
    m.add(haas(pc, 12), d, 0.55)
    m.add(synth_pluck(mtof(81), 1.5, rng, decay=0.4), d, 0.25)
    m.add(sparkle(rng, 1.2, density=22), d, 0.5)
    m.add(kick(rng, 150, 50, 0.3), d, 0.5)
    return reverb(m.out, 0.3, rt60=2.2)


def skill_buff_2(rng):
    """Heroic brass + taiko 'rally' sting."""
    m = Mix(3.5)
    for i, s in enumerate((0, 7, 12)):
        b = brass(mtof(57 + s), 0.9 if i < 2 else 1.8, rng, bright=3500, voices=4)
        m.add(haas(b, 10), i * 0.14, 0.5)
    m.add(war_drum(rng, 60), 0.28, 0.8)
    m.add(sparkle(rng, 1.2, density=15), 0.28, 0.4)
    return reverb(m.out, 0.35, rt60=2.6)


def skill_buff_3(rng):
    """Guitar slide up + shimmering octave arpeggio."""
    m = Mix(3)
    d = 0.6
    slide = lead_tone(exp_ramp([(0, 110), (d, 330)], d), d, rng, vib_depth=0)
    m.add(haas(amp(slide * env_adsr(d, 0.01, 0.05, 1, 0.05), gain=30), 12), 0, 0.5)
    for i, n in enumerate((69, 76, 81, 88)):
        m.add(synth_pluck(mtof(n), 0.8, rng, decay=0.15), d + i * 0.07, 0.3)
    m.add(soul_tone(mtof(93), 1.5, rng) * env_perc(1.5, 0.005, 0.5), d, 0.8)
    return delay(reverb(m.out, 0.25, rt60=2.0), 0.18, 0.35, 0.25)


def skill_magic_1(rng):
    """Arcane swirl: FM shimmer with a resonant sweep and sparkles."""
    d = 1.8
    m = Mix(d + 1)
    t = t_axis(d)
    s = fm(exp_ramp([(0, 220), (d, 660)], d), 1.5, 3 + 2 * np.sin(2 * np.pi * 3 * t), d)
    s = sweep(s, "bandpass", exp_ramp([(0, 500), (d * 0.6, 5000), (d, 1500)], d), q=3)
    m.add(pan(s * env_adsr(d, 0.2, 0.1, 0.9, 0.6), np.sin(2 * np.pi * 1.5 * t) * 0.7), 0, 1.0)
    m.add(sparkle(rng, d, density=30), 0, 0.6)
    m.add(fm_bell(mtof(84), 1.5, 3.5, 3, 0.8), d * 0.6, 0.3)
    return reverb(m.out, 0.45, rt60=3.0)


def skill_magic_2(rng):
    """Spell cast: reverse swell + bright FM bell chord blast."""
    m = Mix(4)
    pre = reverse(reverb(fm_bell(mtof(72), 1.0, 3.5, 4, 0.5), 1.0, rt60=1.5))
    pre = pre[:, -n_samples(0.9):] * np.linspace(0, 1, n_samples(0.9)) ** 2
    m.add(pre, 0, 0.6, tail=False)
    t0 = pre.shape[1] / SR
    for n, p in ((72, -0.4), (75, 0.0), (79, 0.4), (84, 0.1)):
        m.add(fm_bell(mtof(n), 2.0, 2.0, 2.5, 1.0), t0, 0.25, p)
    m.add(synth_pad([60, 67, 72], 2.0, rng, attack=0.01, release=1.5, cutoff=4000), t0, 0.4)
    m.add(sparkle(rng, 1.4, density=25), t0, 0.5)
    return reverb(m.out, 0.4, rt60=3.0)


def skill_magic_3(rng):
    """Dark sorcery: ring-modulated choir and a sub drop."""
    d = 2.2
    m = Mix(d + 1)
    c = choir([50, 56, 62], d, rng, "a", attack=0.1, release=0.8)
    c = c * osc_sine(exp_ramp([(0, 30), (d, 300)], d), d)
    m.add(widen(stereo(c), 1.6), 0, 0.9)
    m.add(sweep(stereo(white(d, rng)), "bandpass", exp_ramp([(0, 8000), (d, 400)], d), q=5)
          * env_adsr(d, 0.05, 0.1, 0.5, 0.5), 0, 0.3)
    m.add(sub_bass(exp_ramp([(0, 80), (d, 30)], d), d) * env_perc(d, 0.01, 0.8), 0, 0.6)
    return reverb(m.out, 0.45, rt60=3.0)


def skill_debuff_1(rng):
    """Descending detuned dissonance — something is draining."""
    d = 1.6
    m = Mix(d + 1)
    for midi, p in ((64, -0.5), (65, 0.5), (70, 0.0)):
        f = mtof(midi) * cents(ramp([(0, 0), (d, -1200)], d))
        s = supersaw(f, d, rng, 5, 35)
        s = sweep(s, "lowpass", exp_ramp([(0, 5000), (d, 400)], d), q=2)
        m.add(s * env_adsr(d, 0.01, 0.1, 0.8, 0.4), 0, 0.35)
    m.add(bitcrush(stereo(lp(brown(d, rng), 300)), 6, 8) * env_adsr(d, 0.1, 0.1, 0.8, 0.3), 0, 0.4)
    return reverb(m.out, 0.3, rt60=2.2)


def skill_debuff_2(rng):
    """Guitar dive-bomb wobble + crushed curse."""
    d = 1.4
    m = Mix(d + 1)
    t = t_axis(d)
    f = exp_ramp([(0, 247), (d, 62)], d) * cents(60 * np.sin(2 * np.pi * 7 * t))
    g = amp(lead_tone(f, d, rng, vib_depth=0) * env_adsr(d, 0.01, 0.1, 1, 0.3), gain=30)
    m.add(haas(g, 11), 0, 0.6)
    crush = bitcrush(saturate(osc_pulse(exp_ramp([(0, 220), (d, 55)], d), d, 0.15), 2), 5, 12)
    m.add(stereo(lp(crush, 3000) * env_adsr(d, 0.01, 0.1, 0.7, 0.3)), 0, 0.25)
    return reverb(m.out, 0.3, rt60=2.0)


def skill_debuff_3(rng):
    """Hex: whispered curse + low tritone bell toll."""
    d = 2.5
    m = Mix(d + 1)
    w = whisper(1.4, rng, flutter=11)
    m.add(haas(w, 15), 0, 0.9)
    m.add(bell(mtof(38), d, rng, decay=1.2), 0.25, 0.8, -0.2)
    m.add(bell(mtof(44), d, rng, decay=1.0), 0.27, 0.6, 0.2)
    m.add(stereo(lp(brown(d, rng), 150)) * env_perc(d, 0.05, 1.0), 0.25, 0.5)
    return reverb(m.out, 0.45, rt60=3.0)


# ============================================================================
# CHARMS (shorter, punchier — UI-adjacent)
# ============================================================================

def charm_gold_1(rng):
    """Coin shower over a bass thump."""
    m = Mix(2)
    for i in range(9):
        m.add(coin(rng), i * rng.uniform(0.03, 0.06) + rng.uniform(0, 0.02), rng.uniform(0.5, 1),
              rng.uniform(-0.7, 0.7))
    m.add(kick(rng, 130, 50, 0.25), 0, 0.5)
    m.add(fm_bell(mtof(88), 1.0, 2.0, 2.0, 0.4), 0.05, 0.25)
    return reverb(m.out, 0.2, rt60=1.2)


def charm_gold_2(rng):
    """Cash-register style chime arpeggio with metal clinks."""
    m = Mix(2)
    for i, n in enumerate((76, 83, 88, 95)):
        m.add(fm_bell(mtof(n), 0.8, 2.0, 2.5, 0.35), i * 0.06, 0.35, -0.3 + i * 0.2)
    for i in range(4):
        m.add(coin(rng), 0.24 + i * 0.05, 0.6, rng.uniform(-0.6, 0.6))
    m.add(amp(power_chord(52, 0.5, rng, mute_at=0.4), gain=20), 0, 0.25)
    return reverb(m.out, 0.2, rt60=1.2)


def charm_damage_1(rng):
    """Crunchy spiked hit."""
    m = Mix(1.5)
    m.add(amp(power_chord(40, 0.6, rng, palm=True), gain=35), 0, 0.6)
    m.add(kick(rng, 180, 55, 0.25), 0, 0.8)
    m.add(stereo(hp(white(0.2, rng), 3000) * env_perc(0.2, 0.001, 0.03)), 0, 0.5)
    m.add(sum(osc_sine(f, 0.8, rng.random()) for f in (2100, 3300)) * env_perc(0.8, 0.001, 0.2),
          0.01, 0.15, 0.3)
    return reverb(m.out, 0.15, rt60=1.0)


def charm_damage_2(rng):
    """Sharpening shing + stab."""
    m = Mix(1.6)
    d = 0.35
    sh = sweep(white(d, rng), "bandpass", exp_ramp([(0, 3000), (d, 10000)], d), q=8)
    m.add(stereo(sh * env_adsr(d, 0.01, 0.05, 0.8, 0.1)), 0, 1.4)
    m.add(synth_bass(mtof(28), 0.4, rng, cutoff=2000, decay=0.08), d, 0.5)
    m.add(snare(rng, decay=0.12), d, 0.4)
    return reverb(m.out, 0.15, rt60=1.0)


def charm_buff_1(rng):
    """Quick rising chime + glow."""
    m = Mix(1.6)
    for i, n in enumerate((69, 73, 76, 81)):
        m.add(synth_pluck(mtof(n), 0.6, rng, decay=0.12), i * 0.05, 0.3, -0.3 + i * 0.2)
    m.add(soul_tone(mtof(88), 1.2, rng) * env_perc(1.2, 0.005, 0.4), 0.15, 0.6)
    return reverb(m.out, 0.25, rt60=1.5)


def charm_buff_2(rng):
    """Short brass stab + shimmer."""
    m = Mix(1.6)
    m.add(haas(brass(mtof(57), 0.5, rng, bright=3500, attack=0.03, release=0.2), 10), 0, 0.5)
    m.add(haas(brass(mtof(64), 0.5, rng, bright=3500, attack=0.03, release=0.2), 10), 0, 0.35)
    m.add(sparkle(rng, 0.6, density=25), 0.05, 0.4)
    return reverb(m.out, 0.25, rt60=1.6)


def charm_magic_1(rng):
    """Twinkling arcane flick."""
    m = Mix(1.6)
    m.add(sparkle(rng, 0.5, density=50, f_lo=3000, f_hi=10000), 0, 0.8)
    m.add(fm(exp_ramp([(0, 400), (0.4, 1600)], 0.4), 2.0, 2.5, 0.4) * env_perc(0.4, 0.005, 0.15),
          0, 0.3)
    return reverb(m.out, 0.35, rt60=1.8)


def charm_magic_2(rng):
    """Mystic rune pulse: FM bell triad with a warble."""
    m = Mix(1.8)
    for n, p in ((74, -0.4), (77, 0.4), (81, 0)):
        b = fm_bell(mtof(n), 1.0, 1.4, 3.0, 0.5)
        b *= 1 + 0.4 * np.sin(2 * np.pi * 9 * t_axis(1.0))
        m.add(b, 0, 0.3, p)
    return reverb(m.out, 0.35, rt60=1.8)


def charm_debuff_1(rng):
    """Dark bell drop."""
    m = Mix(1.8)
    m.add(bell(mtof(50), 1.3, rng, decay=0.5), 0, 0.8)
    m.add(stereo(osc_saw(exp_ramp([(0, 400), (0.5, 80)], 0.5), 0.5) * env_perc(0.5, 0.005, 0.2)),
          0, 0.15)
    return reverb(m.out, 0.25, rt60=1.5)


def charm_debuff_2(rng):
    """Hissing sour curse puff."""
    m = Mix(1.6)
    d = 0.6
    hiss = sweep(white(d, rng), "bandpass", exp_ramp([(0, 6000), (d, 800)], d), q=2)
    m.add(stereo(hiss * env_adsr(d, 0.01, 0.1, 0.6, 0.3)), 0, 0.8)
    for midi in (62, 63):
        s = osc_pulse(mtof(midi) * cents(ramp([(0, 0), (d, -300)], d)), d, 0.25)
        m.add(lp(s, 2000) * env_perc(d, 0.005, 0.25), 0, 0.15, -0.4 if midi == 62 else 0.4)
    return reverb(m.out, 0.2, rt60=1.3)


def charm_health_1(rng):
    """Warm healing bloom: major arpeggio + choir 'ah'."""
    m = Mix(2.4)
    for i, n in enumerate((64, 68, 71, 76)):
        m.add(bell(mtof(n + 12), 1.2, rng, decay=0.5), i * 0.06, 0.5, -0.4 + i * 0.27)
    m.add(stereo(choir([64, 68, 71], 1.5, rng, "a", attack=0.15, release=0.9, breath=0.1)), 0.1, 0.6)
    m.add(sparkle(rng, 0.8, density=18, f_lo=2500, f_hi=7000), 0.15, 0.35)
    return reverb(m.out, 0.35, rt60=2.0)


def charm_health_2(rng):
    """Heartbeat + heal shimmer."""
    m = Mix(2.2)
    for t0, g in ((0, 1.0), (0.22, 0.7)):
        m.add(kick(rng, 90, 40, 0.18, click=0.1, dur=0.5), t0, g)
    m.add(soul_tone(mtof(76), 1.5, rng, 0.5) * env_adsr(1.5, 0.1, 0.1, 0.8, 0.9), 0.35, 1.0)
    for i, n in enumerate((76, 80, 83)):
        m.add(fm_bell(mtof(n + 12), 0.9, 1.0, 1.5, 0.4), 0.35 + i * 0.08, 0.2, -0.3 + i * 0.3)
    return reverb(m.out, 0.3, rt60=1.8)


# ============================================================================
# REGISTRY:  (folder, file name, function)
# ============================================================================

SFX = [
    ("War Drums", "WarDrums_March", war_drums_march),
    ("War Drums", "WarDrums_Charge", war_drums_charge),
    ("War Drums", "WarDrums_Ritual", war_drums_ritual),
    ("War Drums", "WarDrums_EnsembleHits", war_drums_ensemble_hits),

    ("Battle Horns", "Horn_Troll_01", horn_troll_1),
    ("Battle Horns", "Horn_Troll_02", horn_troll_2),
    ("Battle Horns", "Horn_Goblin_01", horn_goblin_1),
    ("Battle Horns", "Horn_Goblin_02", horn_goblin_2),
    ("Battle Horns", "Horn_Human_01", horn_human_1),
    ("Battle Horns", "Horn_Human_02", horn_human_2),

    ("Souls/Mysterious", "Soul_Mystery_EtherealChoir", soul_mystery_ethereal_choir),
    ("Souls/Mysterious", "Soul_Mystery_Whispers", soul_mystery_whispers),
    ("Souls/Mysterious", "Soul_Mystery_Wail", soul_mystery_wail),
    ("Souls/Mysterious", "Soul_Mystery_Glass", soul_mystery_glass),
    ("Souls/Mysterious", "Soul_Mystery_Moan", soul_mystery_moan),
    ("Souls/Mysterious", "Soul_Mystery_ReverseSwell", soul_mystery_reverse_swell),
    ("Souls/Mysterious", "Soul_Mystery_Hum", soul_mystery_hum),

    ("Souls/Taken", "Soul_Taken_01", soul_taken_1),
    ("Souls/Taken", "Soul_Taken_02", soul_taken_2),
    ("Souls/Taken", "Soul_Taken_03", soul_taken_3),
    ("Souls/Received", "Soul_Received_01", soul_received_1),
    ("Souls/Received", "Soul_Received_02", soul_received_2),
    ("Souls/Received", "Soul_Received_03", soul_received_3),
    ("Souls/Flying", "Soul_Flying_01", soul_flying_1),
    ("Souls/Flying", "Soul_Flying_02", soul_flying_2),
    ("Souls/Flying", "Soul_Flying_03", soul_flying_3),
    ("Souls/Gurgling", "Soul_Gurgle_01", soul_gurgle_1),
    ("Souls/Gurgling", "Soul_Gurgle_02", soul_gurgle_2),
    ("Souls/Gurgling", "Soul_Gurgle_03", soul_gurgle_3),

    ("Skills/Damage", "Skill_Damage_01", skill_damage_1),
    ("Skills/Damage", "Skill_Damage_02", skill_damage_2),
    ("Skills/Damage", "Skill_Damage_03", skill_damage_3),
    ("Skills/Buff", "Skill_Buff_01", skill_buff_1),
    ("Skills/Buff", "Skill_Buff_02", skill_buff_2),
    ("Skills/Buff", "Skill_Buff_03", skill_buff_3),
    ("Skills/Magic", "Skill_Magic_01", skill_magic_1),
    ("Skills/Magic", "Skill_Magic_02", skill_magic_2),
    ("Skills/Magic", "Skill_Magic_03", skill_magic_3),
    ("Skills/Debuff", "Skill_Debuff_01", skill_debuff_1),
    ("Skills/Debuff", "Skill_Debuff_02", skill_debuff_2),
    ("Skills/Debuff", "Skill_Debuff_03", skill_debuff_3),

    ("Charms/Gold", "Charm_Gold_01", charm_gold_1),
    ("Charms/Gold", "Charm_Gold_02", charm_gold_2),
    ("Charms/Damage", "Charm_Damage_01", charm_damage_1),
    ("Charms/Damage", "Charm_Damage_02", charm_damage_2),
    ("Charms/Buff", "Charm_Buff_01", charm_buff_1),
    ("Charms/Buff", "Charm_Buff_02", charm_buff_2),
    ("Charms/Magic", "Charm_Magic_01", charm_magic_1),
    ("Charms/Magic", "Charm_Magic_02", charm_magic_2),
    ("Charms/Debuff", "Charm_Debuff_01", charm_debuff_1),
    ("Charms/Debuff", "Charm_Debuff_02", charm_debuff_2),
    ("Charms/Health", "Charm_Health_01", charm_health_1),
    ("Charms/Health", "Charm_Health_02", charm_health_2),
]
