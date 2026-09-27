"""Unfitted small-angle Helmbold compatibility diagnostic; not a flight-model acceptance scorer."""
import argparse
import csv
import hashlib
import json
import math
import pathlib


def slope(aspect):
    """Helmbold small-angle lift slope per radian, as proposed in aerodynamics.md."""
    if not math.isfinite(aspect) or aspect <= 0:
        raise ValueError('Expected a finite positive aspect ratio')
    return 2 * math.pi * aspect / (2 + math.hypot(aspect, 2))


def compare(row):
    fields = ['aspect_ratio', 'alpha_deg', 'value', 'measurement_lower_95', 'measurement_upper_95',
              'extraction_alpha_bound_deg', 'extraction_value_bound']
    values = {key: float(row[key]) for key in fields}
    if not all(math.isfinite(value) for value in values.values()):
        raise ValueError('Nonfinite primary observation')
    aspect, angle, observed = [values[k] for k in ['aspect_ratio', 'alpha_deg', 'value']]
    lower, upper = values['measurement_lower_95'], values['measurement_upper_95']
    angle_bound, value_bound = values['extraction_alpha_bound_deg'], values['extraction_value_bound']
    if not lower <= observed <= upper or min(angle_bound, value_bound) < 0:
        raise ValueError('Invalid observation uncertainty interval')
    lift_slope = slope(aspect)
    predicted = lift_slope * math.radians(angle)
    spread = lift_slope * math.radians(angle_bound)
    predicted_lower, predicted_upper = predicted - spread, predicted + spread
    observed_lower, observed_upper = lower - value_bound, upper + value_bound
    return dict(values, predicted_cl=predicted, residual_cl=predicted - observed,
                predicted_lower=predicted_lower, predicted_upper=predicted_upper,
                observed_lower=observed_lower, observed_upper=observed_upper,
                intervals_overlap=predicted_lower <= observed_upper and observed_lower <= predicted_upper)


def evaluate(rows):
    selected, strata, seen = [], set(), set()
    total_lift = 0
    for row in rows:
        if row['coefficient'] != 'CL':
            continue
        total_lift += 1
        compared = compare(row)
        aspect, angle = compared['aspect_ratio'], compared['alpha_deg']
        if aspect not in (0.5, 1.0):
            raise ValueError('Unexpected aspect-ratio stratum')
        identity = (aspect, angle, compared['value'])
        if identity in seen:
            raise ValueError('Duplicate primary observation')
        seen.add(identity)
        if 1 <= abs(angle) <= 10:
            selected.append(compared)
            strata.add((aspect, angle > 0))
    if strata != {(aspect, positive) for aspect in (0.5, 1.0) for positive in (False, True)}:
        raise ValueError('Incomplete aspect-ratio/incidence-sign coverage')
    summaries = []
    for aspect in (0.5, 1.0):
        group = [r for r in selected if r['aspect_ratio'] == aspect]
        summaries.append(dict(aspect_ratio=aspect, observations=len(group),
                              overlapping=sum(r['intervals_overlap'] for r in group),
                              nonoverlapping=sum(not r['intervals_overlap'] for r in group),
                              maximum_absolute_residual_cl=max(abs(r['residual_cl']) for r in group)))
    return dict(schema=1, scope='Unfitted pointwise compatibility; no model acceptance or population verdict',
                incidence_centre_abs_deg=[1, 10], total_lift_observations=total_lift,
                selected_observations=len(selected), summaries=summaries, observations=selected)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('reference', type=pathlib.Path)
    parser.add_argument('output', type=pathlib.Path)
    args = parser.parse_args()
    with args.reference.open() as handle:
        result = evaluate(list(csv.DictReader(handle)))
    result['reference_sha256'] = hashlib.sha256(args.reference.read_bytes()).hexdigest()
    result['source_sha256'] = hashlib.sha256(pathlib.Path(__file__).read_bytes()).hexdigest()
    args.output.write_text(json.dumps(result, indent=2, allow_nan=False) + '\n')
    print(json.dumps(result['summaries'], indent=2))


if __name__ == '__main__':
    main()
