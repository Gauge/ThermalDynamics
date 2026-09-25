"""Whether a derived cruise speed lands where the authored one does, on real ships.

[RelativeTopSpeed](https://github.com/Gauge/RelativeTopSpeed) holds each grid under a *cruise
speed* interpolated through three authored mass points per grid size — eight numbers and two
multipliers, fitted by hand. It has no air in it at all: no density, no altitude, no area, no shape,
so a hull and a brick of the same mass decelerate identically.

**This model has every one of those terms already**, so the same quantity falls out instead of being
fitted: a ship cruises where its thrust balances its drag, `T = ½ C_d ρ A v²`, which is

    v_cruise = sqrt(2T / (C_d ρ A))

and needs no mass at all. That is the whole argument of thermal-model.md's change log, the grid-speed milestone, and the row says exactly
what decides it: **whether the derived speeds land where the authored ones do on real ships**. This
is that measurement.

**It needs thrust and exposed area and nothing else**, both of which the census carries, so it costs
a parse rather than a walk. Ships with neither — stations, unpowered hulks — are counted and
dropped rather than given a speed they do not have (`E8`).

Usage: cruise.py [census.csv] [--cd C] [--rho R] [--csv <path>]
"""
import csv
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import scoring

AUTHORED_BAND = (60.0, 110.0)

SEA_LEVEL_DENSITY = 1.225

ENGINE_CAP = 100.0


PROJECTED_SHARE = 0.25


# The coefficient the mod's force applied until the K25 strip removed the force (2026-09-24). No
# coefficient ships now; this stays the default so the figures taken with it reproduce.
RETIRED_DRAG_COEFFICIENT = 1.54


# cruise operation.
def cruise(thrust, area, coefficient, density, shape=1.0):
    """Where thrust balances drag, m/s, or None where the ship has neither (`E8`).

    `area` is a hull's **total exposed area**, as the census records it; the frontal projection the
    drag expression wants is `PROJECTED_SHARE` of it.

    `shape` is the hull's own shape factor from the census's `shape_factor` column — the
    projected-area-weighted mean of the Newtonian `sin^2(theta)` the flat-plate projection is
    missing, averaged over the six axis winds. **One is the model without the shape term**, which is
    what a census taken before 2026-08-31 or after 2026-09-24 gets — the column existed only while the
    shape term did — and what `--no-shape` forces, so the two readings are
    the same arithmetic with one factor switched. See aerodynamics.md, AR4.
    """
    if thrust is None or area is None or thrust <= 0 or area <= 0:
        return None
    if coefficient <= 0 or density <= 0:
        return None
    if shape is None or shape <= 0:
        shape = 1.0

    return math.sqrt(2.0 * thrust
                     / (coefficient * density * area * PROJECTED_SHARE * shape))


# shape of operation.
def shape_of(row, enabled):
    """A row's shape factor, or 1 where the census predates the column or it is switched off."""
    if not enabled:
        return 1.0
    value = scoring.number(row, "shape_factor")
    return 1.0 if value is None or value <= 0 else value


# main operation.
def main():
    args = scoring.positionals(("--cd", "--rho", "--csv"))

    path = args[0] if args else "out/census-2026-08-25/census.csv"
    coefficient = float(scoring.flag("--cd", RETIRED_DRAG_COEFFICIENT))
    density = float(scoring.flag("--rho", SEA_LEVEL_DENSITY))
    out = scoring.flag("--csv", None)

    shaped = "--no-shape" not in sys.argv

    if not os.path.exists(path):
        raise SystemExit(path + " does not exist, so there is no census to read")

    rows = list(csv.DictReader(open(path)))

    speeds = []
    without = 0
    for row in rows:
        speed = cruise(scoring.number(row, "thrust_n"), scoring.number(row, "exposed_area_m2"),
                       coefficient, density, shape_of(row, shaped))
        if speed is None:
            without += 1
            continue
        speeds.append((speed, row.get("ship", ""), scoring.number(row, "large")))

    if not speeds:
        raise SystemExit("no ship in " + path + " carries both thrust and exposed area")

    speeds.sort()
    values = [s for s, _, _ in speeds]

    print("derived cruise speed from %s" % path)
    print("  %d ships carry thrust and exposed area; %d carry neither and are dropped"
          % (len(values), without))
    print("  C_d %.2f%s, air density %.3f kg/m^3 — where a ship flies fastest and where the"
          " authored curve was tuned"
          % (coefficient,
             " (the retired force model's)" if coefficient == RETIRED_DRAG_COEFFICIENT else "",
             density))

    carried = sum(1 for row in rows if scoring.number(row, "shape_factor") is not None)
    if not shaped:
        print("  hull shape ignored (--no-shape): every hull read as a flat-plate projection")
    elif carried == 0:
        print("  this census carries no shape_factor column, so every hull reads as 1"
              " — the model without the shape term")
    else:
        factors = sorted(scoring.number(row, "shape_factor") for row in rows
                         if scoring.number(row, "shape_factor") is not None)
        middle = factors[len(factors) // 2]
        print("  hull shape applied to %d of %d ships, median factor %.4f (backlog.md K22)"
              % (carried, len(rows), middle))

    print()
    print("  %-10s %10s" % ("quantile", "m/s"))
    figures = []
    for label, q in (("p5", 0.05), ("p25", 0.25), ("p50", 0.5), ("p75", 0.75), ("p95", 0.95)):
        value = scoring.percentile(values, q)
        figures.append(("cruise " + label, round(value, 2), "m/s"))
        print("  %-10s %10.1f" % (label, value))
    print("  %-10s %10.1f" % ("min", values[0]))
    print("  %-10s %10.1f" % ("max", values[-1]))
    figures.append(("cruise min", round(values[0], 2), "m/s"))
    figures.append(("cruise max", round(values[-1], 2), "m/s"))

    low, high = AUTHORED_BAND
    inside = sum(1 for v in values if low <= v <= high)
    share = 100.0 * inside / len(values)

    print()
    print("  RTS's authored band is %g to %g m/s across 200 t to 8 kt." % (low, high))
    print("  %d of %d ships land inside it, %.1f %%" % (inside, len(values), share))
    figures.append(("ships inside the authored band", inside, "ships"))
    figures.append(("share inside the authored band", round(share, 2), "%"))
    figures.append(("ships with thrust and area", len(values), "ships"))
    figures.append(("ships with neither", without, "ships"))

    print()
    print("  **What the derived model separates that the authored one cannot.** RTS's curve is a"
          " function")
    print("  of mass alone, so a hollow hull and a dense one of equal mass cruise alike. This is a")
    print("  function of thrust over area, which is what actually decides a top speed — and the"
          " spread")
    print("  below is the size of what the mass curve throws away.")

    print()
    print("  %-12s %14s %10s %28s" % ("air density", "", "median", "drag-limited below 100 m/s"))
    for rho, label in ((1.225, "sea level"), (0.6, "half"), (0.3, "thin"), (0.1, "very thin")):
        thinner = sorted(v for v in (cruise(scoring.number(row, "thrust_n"),
                                            scoring.number(row, "exposed_area_m2"),
                                            coefficient, rho, shape_of(row, shaped))
                                     for row in rows) if v is not None)
        median = scoring.percentile(thinner, 0.5)
        below = sum(1 for v in thinner if v <= ENGINE_CAP)
        share_below = 100.0 * below / len(thinner)
        print("  %-12.3f %14s %8.1f m/s %26.1f %%" % (rho, label, median, share_below))
        figures.append(("median cruise at density %g" % rho, round(median, 2), "m/s"))
        figures.append(("share under the engine cap at density %g" % rho, round(share_below, 2), "%"))

    print()
    print("  **A top speed that falls with altitude, which is what a mass curve cannot produce.**")
    print("  Low down a ship is held by the air; high up the engine's own cap takes over, exactly as")
    print("  it does today. So the per-grid top speed the grid-speed milestone wanted a force for is an outcome of drag")
    print("  that is already applied, for the great majority of ships and all of the low-altitude"
          " ones.")

    if out:
        scoring.write_summary(out, figures)
        print("\ncsv -> %s  (%d figures)" % (out, len(figures)))


if __name__ == "__main__":
    main()
