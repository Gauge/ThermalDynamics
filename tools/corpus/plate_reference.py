"""Extract primary marker/error-bar observations from the hash-pinned Mizoguchi 2016 PDF."""
import argparse
import csv
import hashlib
import pathlib

PDF_SHA256 = 'f90541a83d6fae1ddc62069ccd91855cfe048e303d228635a01248be97a6c78b'
PLOTS = [dict(figure=3, coefficient='CL', left=99.321, right=262.713, top=75.886, bottom=189.768,
              low=-0.4, high=1.4, legend=(167.873, 142.654, 257.689, 187.884)),
         dict(figure=4, coefficient='CD', left=99.321, right=262.713, top=259.458, bottom=373.339,
              low=-0.2, high=1.2, legend=(104.345, 261.343, 194.161, 306.572))]
EXTRACTION_BOUND_PT = 0.5


def coordinates(plot, x, y):
    """Convert PDF top-origin coordinates using the independently read axis endpoint labels."""
    angle = -10 + 60 * (x - plot['left']) / (plot['right'] - plot['left'])
    value = plot['high'] - (plot['high'] - plot['low']) * (y - plot['top']) / (plot['bottom'] - plot['top'])
    return angle, value


def marker_centre(marker, plot):
    """Select hollow primary symbols inside the plot and outside its legend."""
    if not (1.8 <= marker['width'] <= 2 and 1.8 <= marker['height'] <= 2):
        return None
    x, y = (marker['x0'] + marker['x1']) / 2, (marker['top'] + marker['bottom']) / 2
    if not (plot['left'] <= x <= plot['right'] and plot['top'] <= y <= plot['bottom']):
        return None
    left, top, right, bottom = plot['legend']
    if left <= x <= right and top <= y <= bottom:
        return None
    if not marker['stroke'] or marker['fill']:
        raise ValueError('Unexpected primary marker style')
    return x, y


def observation(plot, marker, lines, aspect_ratio):
    """Retain the unique centred vertical error bar, without assigning an incidence branch."""
    centre = marker_centre(marker, plot)
    if centre is None:
        return None
    x, y = centre
    bars = [line for line in lines if line['width'] < 0.01 and line['height'] > 0.5
            and abs(line['x0'] - x) <= 0.03
            and abs((line['top'] + line['bottom']) / 2 - y) <= 0.05]
    if len(bars) != 1:
        raise ValueError('Expected one primary error bar at %.6f, %.6f; found %d' % (x, y, len(bars)))
    bar = bars[0]
    angle, value = coordinates(plot, x, y)
    lower, upper = coordinates(plot, x, bar['bottom'])[1], coordinates(plot, x, bar['top'])[1]
    if not lower <= value <= upper:
        raise ValueError('Marker is outside its measurement interval')
    return dict(figure=plot['figure'], aspect_ratio=aspect_ratio, coefficient=plot['coefficient'],
                alpha_deg=angle, value=value, measurement_lower_95=lower, measurement_upper_95=upper,
                extraction_alpha_bound_deg=60 * EXTRACTION_BOUND_PT / (plot['right'] - plot['left']),
                extraction_value_bound=(plot['high'] - plot['low']) * EXTRACTION_BOUND_PT / (plot['bottom'] - plot['top']),
                marker_x_pt=x, marker_y_pt=y, error_top_pt=bar['top'], error_bottom_pt=bar['bottom'],
                sweep_branch='unspecified')


def extract(path):
    if hashlib.sha256(pathlib.Path(path).read_bytes()).hexdigest() != PDF_SHA256:
        raise ValueError('Source PDF hash differs from the inspected publisher version')
    import pdfplumber
    with pdfplumber.open(path) as pdf:
        page = pdf.pages[2]
        if abs(page.width - 595.276) > 0.001 or abs(page.height - 841.89) > 0.001:
            raise ValueError('Unexpected source page dimensions')
        rows = []
        for plot in PLOTS:
            frames = [r for r in page.rects if all(abs(r[k] - plot[v]) < 0.002
                      for k, v in [('x0', 'left'), ('x1', 'right'), ('top', 'top'), ('bottom', 'bottom')])]
            if len(frames) != 2:
                raise ValueError('Source plot frame does not match the inspected layout')
            for aspect, markers, expected in [(1.0, page.curves, 63), (0.5, page.rects, 60)]:
                measured = [row for marker in markers if (row := observation(plot, marker, page.lines, aspect)) is not None]
                if len(measured) != expected:
                    raise ValueError('Unexpected primary marker count: %d' % len(measured))
                rows.extend(measured)
        return sorted(rows, key=lambda r: (r['figure'], r['aspect_ratio'], r['alpha_deg'], r['value']))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('pdf', type=pathlib.Path)
    parser.add_argument('output', type=pathlib.Path)
    args = parser.parse_args()
    rows = extract(args.pdf)
    with args.output.open('w', newline='') as handle:
        writer = csv.DictWriter(handle, fieldnames=list(rows[0]))
        writer.writeheader()
        writer.writerows({key: format(value, '.10g') if isinstance(value, float) else value
                         for key, value in row.items()} for row in rows)
    print('Extracted %d primary marker/error-bar observations; no model fit' % len(rows))


if __name__ == '__main__':
    main()
