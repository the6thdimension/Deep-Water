"""Oscillation summary for a play-mode recording (CSV from templates/play-recorder.cs).

usage: python -I periods.py <recording.csv> [--after T] [--cols name1,name2]

For each numeric column (or the named ones): the late mean, the period from upward crossings of
that mean (linear interpolation between samples), the first few peaks above the mean (for the
decay), and the max |value|. Lines starting with '#' are printed as notes, not parsed.

The period is the average spacing of upward mean-crossings, so it is the DAMPED period. A
lightly damped oscillator's damped period is longer than the undamped one by 1/sqrt(1 - zeta^2).
"""
import argparse, csv, io, sys


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('csv')
    ap.add_argument('--after', type=float, default=0.0, help='ignore samples before this time')
    ap.add_argument('--cols', default='', help='comma-separated column names (default: all but t)')
    a = ap.parse_args()

    lines = io.open(a.csv, encoding='utf-8').read().splitlines()
    for l in lines:
        if l.startswith('#'):
            print(l)
    rows = [l for l in lines if l and not l.startswith('#')]
    header = next(csv.reader([rows[0]]))
    data = [list(map(float, r)) for r in csv.reader(rows[1:]) if r]
    data = [r for r in data if r[0] >= a.after]
    cols = a.cols.split(',') if a.cols else header[1:]
    t = [r[0] for r in data]
    for name in cols:
        k = header.index(name)
        v = [r[k] for r in data]
        half = v[len(v) // 2:]
        mean = sum(half) / len(half)
        crossings = []
        for i in range(1, len(v)):
            x0, x1 = v[i - 1] - mean, v[i] - mean
            if x0 < 0 <= x1:
                crossings.append(t[i - 1] + (t[i] - t[i - 1]) * (-x0) / (x1 - x0))
        period = (crossings[-1] - crossings[0]) / (len(crossings) - 1) if len(crossings) >= 2 else None
        peaks = [v[i] - mean for i in range(1, len(v) - 1) if v[i] > v[i - 1] and v[i] >= v[i + 1] and v[i] - mean > 1e-3]
        print(f"{name}: period {'n/a' if period is None else f'{period:.3f}'} s over {max(len(crossings) - 1, 0)} cycles; "
              f"late mean {mean:.3f}; first peaks {[round(p, 3) for p in peaks[:5]]}; max |x| {max(abs(x) for x in v):.3f}")


if __name__ == '__main__':
    sys.exit(main())
