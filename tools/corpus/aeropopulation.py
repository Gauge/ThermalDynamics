"""Resumable, content-addressed P1/P2 population walk; unresolved files never silently disappear."""
import argparse
from collections import Counter
import datetime
import fcntl
import hashlib
import json
import math
import os
import pathlib
import subprocess
import sys
import tempfile

import numpy as np
import scipy
import aeroenvelope
import aerothrust
import scoring


def digest(path):
    h = hashlib.sha256()
    with pathlib.Path(path).open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b''):
            h.update(chunk)
    return h.hexdigest()


def canonical(value):
    return json.dumps(value, sort_keys=True, separators=(',', ':'), allow_nan=False).encode()


def atomic(path, value):
    path = pathlib.Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    fd, name = tempfile.mkstemp(prefix=path.name + '.', suffix='.tmp', dir=path.parent)
    try:
        with os.fdopen(fd, 'wb') as stream:
            stream.write(canonical(value))
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(name, path)
    finally:
        if os.path.exists(name):
            os.unlink(name)


def invoke(binary, *args):
    result = subprocess.run(['dotnet', str(binary), *map(str, args)], text=True, capture_output=True)
    if result.returncode:
        raise RuntimeError('Harness exit %s: %s' % (result.returncode, (result.stderr + result.stdout)[-12000:]))
    return json.loads(result.stdout)


def manifest(corpus, binary):
    corpus, binary = pathlib.Path(corpus).resolve(), pathlib.Path(binary).resolve()
    paths = [p for p in corpus.rglob('bp.sbc') if p.is_file()]
    total_bytes = sum(p.stat().st_size for p in paths)
    files, hashed_bytes = [], 0
    for p in paths:
        before = p.stat()
        checksum = digest(p)
        after = p.stat()
        if (before.st_size, before.st_mtime_ns, before.st_ctime_ns) != (after.st_size, after.st_mtime_ns, after.st_ctime_ns):
            raise ValueError('Blueprint changed during inventory: ' + str(p))
        files.append({'path': str(p.relative_to(corpus)), 'bytes': before.st_size, 'sha256': checksum})
        hashed_bytes += before.st_size
        if len(files) % 100 == 0 or len(files) == len(paths):
            print(json.dumps({'phase': 'manifest', 'files': len(files), 'total_files': len(paths),
                              'bytes': hashed_bytes, 'total_bytes': total_bytes}), flush=True)
    files.sort(key=lambda r: (-r['bytes'], r['path']))
    if not files:
        raise ValueError('No bp.sbc files in the requested population')
    content = pathlib.Path(invoke(binary, 'aero-environment')['game_content'])
    sources = [pathlib.Path(__file__), pathlib.Path(aeroenvelope.__file__), pathlib.Path(aerothrust.__file__), pathlib.Path(scoring.__file__)]
    dependencies = list(binary.parent.glob('*.dll')) + list(binary.parent.glob('*.json'))
    dependencies += [p for p in (pathlib.Path(__file__).resolve().parents[2] / 'Thermodynamics' / 'Content' / 'Data').rglob('*') if p.is_file()]
    game = [content / 'Components.sbc'] + sorted((content / 'CubeBlocks').glob('*.sbc'))
    fingerprint = {str(p.resolve()): digest(p) for p in sources + dependencies + game}
    identity = {'schema': 1, 'corpus': str(corpus), 'binary': str(binary), 'files': files,
                'fingerprint': fingerprint, 'python': sys.version, 'numpy': np.__version__, 'scipy': scipy.__version__,
                'vertex_limit': aeroenvelope.VERTEX_LIMIT}
    return dict(identity, identity_sha256=hashlib.sha256(canonical(identity)).hexdigest())


def verify_fingerprint(plan, stamps):
    """Rehash changed artifacts before and after each blueprint; changed content stops the run."""
    for name, expected in plan['fingerprint'].items():
        path = pathlib.Path(name)
        stat = path.stat()
        stamp = (stat.st_size, stat.st_mtime_ns, stat.st_ctime_ns, stat.st_ino)
        if stamps.get(name) != stamp:
            if digest(path) != expected:
                raise ValueError('Runtime/input artifact changed during the walk: ' + name)
            stamps[name] = stamp


def start(output, current):
    path = pathlib.Path(output) / 'manifest.json'
    if path.exists():
        saved = json.loads(path.read_text())
        if saved != current:
            raise ValueError('Population or provenance changed; use a new output directory')
    else:
        atomic(path, current)
    return current


def receipt_name(entry):
    return hashlib.sha256(entry['path'].encode()).hexdigest() + '.json'


def seal(record):
    value = {k: v for k, v in record.items() if k != 'record_sha256'}
    return dict(value, record_sha256=hashlib.sha256(canonical(value)).hexdigest())


def read_receipts(output, plan):
    folder = pathlib.Path(output) / 'receipts'
    expected = {receipt_name(e): e for e in plan['files']}
    records = []
    for path in sorted(folder.glob('*.json')):
        if path.name not in expected:
            raise ValueError('Unexpected or duplicate receipt: ' + path.name)
        record = json.loads(path.read_text())
        if record != seal(record):
            raise ValueError('Receipt checksum mismatch: ' + path.name)
        if record.get('entry') != expected[path.name] or record.get('identity_sha256') != plan['identity_sha256']:
            raise ValueError('Receipt provenance mismatch: ' + path.name)
        if record.get('status') not in ['measured', 'excluded_modded', 'excluded_small', 'not_self_lifting', 'unresolved']:
            raise ValueError('Unknown receipt status: ' + path.name)
        records.append(record)
    return records


def score(plan, records):
    names = [r['entry']['path'] for r in records]
    if len(names) != len(set(names)):
        raise ValueError('Duplicate blueprint records')
    expected = {e['path']: e for e in plan['files']}
    for record in records:
        if record['entry'] != expected.get(record['entry']['path']) or record['identity_sha256'] != plan['identity_sha256']:
            raise ValueError('Scored receipt does not belong to the manifest')
        if record['status'] not in ['measured', 'excluded_modded', 'excluded_small', 'not_self_lifting', 'unresolved']:
            raise ValueError('Unknown receipt status')
        if record['status'] == 'measured':
            row = record['drag']
            lo, hi = row['ceiling_lower_mps'], row['ceiling_upper_mps']
            if not math.isfinite(lo) or not 0 <= lo <= 128 or (hi is not None and (not math.isfinite(hi) or not lo <= hi <= 128)):
                raise ValueError('Invalid terminal-speed bracket')
            if row['right_censored'] != (hi is None) or row['p1'] not in ['holds', 'overpowered', 'unresolved']:
                raise ValueError('Invalid drag verdict or censoring')
    statuses = Counter(r['status'] for r in records)
    rows = [r['drag'] for r in records if r['status'] == 'measured']
    total = len(plan['files'])
    attempted = sum(r['entry']['bytes'] for r in records)
    complete = len(records) == total and not statuses['unresolved']
    n = len(rows)
    bad = sum(r['p1'] == 'overpowered' for r in rows)
    ambiguous = sum(r['p1'] == 'unresolved' for r in rows)
    low = 100 * bad / n if n else None
    high = 100 * (bad + ambiguous) / n if n else None
    p1 = 'unresolved'
    if complete and n >= 20:
        p1 = 'holds' if high <= 5 else 'fails' if low > 5 else 'unresolved'
    lower = scoring.percentile([r['ceiling_lower_mps'] for r in rows], 0.01) if n else None
    finite_upper = sorted(r['ceiling_upper_mps'] for r in rows if r['ceiling_upper_mps'] is not None)
    upper = None
    if n and math.ceil((n - 1) * 0.01) < len(finite_upper):
        upper = scoring.percentile(finite_upper + [128] * (n - len(finite_upper)), 0.01)
    p2 = 'unresolved'
    if complete and n >= 100:
        p2 = 'holds' if lower >= 60 else 'fails' if upper is not None and upper < 60 else 'unresolved'
    return {'schema': 1, 'complete_resolved': complete, 'files_total': total, 'files_recorded': len(records),
            'bytes_total': sum(e['bytes'] for e in plan['files']), 'bytes_recorded': attempted,
            'statuses': dict(statuses), 'self_lifting_measured': n,
            'p1_share_lower_percent': low, 'p1_share_upper_percent': high, 'p1': p1,
            'p1_speed_lower_mps': lower, 'p1_speed_upper_mps': upper, 'p2': p2,
            'right_censored': sum(r['right_censored'] for r in rows),
            'identity_sha256': plan['identity_sha256']}


def measure(plan, entry, output):
    binary = pathlib.Path(plan['binary'])
    path = pathlib.Path(plan['corpus']) / entry['path']
    if digest(path) != entry['sha256']:
        raise ValueError('Blueprint changed after manifest capture: ' + entry['path'])
    record = {'entry': entry, 'identity_sha256': plan['identity_sha256'],
              'utc': datetime.datetime.now(datetime.timezone.utc).isoformat()}
    try:
        selected = invoke(binary, 'aero-corpus-inputs', path)
        if selected['status'] != 'candidate':
            if selected['status'] not in ['excluded_modded', 'excluded_small']:
                raise ValueError('Unknown selection status')
            record.update(status=selected['status'], selection=selected)
        else:
            data = selected['input']
            if data['blueprint_sha256'] != entry['sha256']:
                raise ValueError('Harness blueprint hash mismatch')
            aerothrust.validate(data)
            strongest = aeroenvelope.maximum(data)
            strongest.update({k: data[k] for k in ['blueprint_sha256', 'harness_mvid', 'core_mvid']})
            strongest['schema'] = 1
            record.update(input=data, strongest=strongest)
            mass = float(data['mass_kg'])
            if not math.isfinite(mass) or mass <= 0:
                raise ValueError('Invalid model mass')
            rating, tolerance = strongest['capacity_n'], strongest['tolerance_n']
            weight = mass * 9.81
            if rating + tolerance < weight:
                record['status'] = 'not_self_lifting'
            elif rating - tolerance <= weight:
                raise RuntimeError('Self-lifting boundary unresolved within thrust tolerance')
            else:
                with tempfile.TemporaryDirectory(prefix='aero-thrust-', dir=output) as temp:
                    thrust = pathlib.Path(temp) / 'thrust.json'
                    atomic(thrust, strongest)
                    result = invoke(binary, 'aero-population-drag', path, thrust)
                for key in ['blueprint_sha256', 'harness_mvid', 'core_mvid']:
                    if result[key] != data[key]:
                        raise ValueError('Drag-result provenance mismatch: ' + key)
                record.update(status='measured', drag=result)
    except Exception as error:
        record.update(status='unresolved', error=type(error).__name__ + ': ' + str(error))
    if digest(path) != entry['sha256']:
        raise ValueError('Blueprint changed during measurement: ' + entry['path'])
    return record


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--corpus', type=pathlib.Path)
    parser.add_argument('--binary', type=pathlib.Path, default=pathlib.Path('tests/Thermodynamics.Sim/bin/Debug/net9.0/Thermodynamics.Sim.dll'))
    parser.add_argument('--out', type=pathlib.Path, required=True)
    parser.add_argument('--score', action='store_true')
    parser.add_argument('--limit', type=int, help='maximum new records this invocation; the manifest still covers every file')
    parser.add_argument('--retry-errors', action='store_true')
    args = parser.parse_args()
    try:
        args.out.mkdir(parents=True, exist_ok=True)
        lock = (args.out / 'run.lock').open('a')
        fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        if args.score:
            plan = json.loads((args.out / 'manifest.json').read_text())
        else:
            if args.corpus is None or (args.limit is not None and args.limit <= 0):
                raise ValueError('A corpus and a positive optional limit are required')
            plan = start(args.out, manifest(args.corpus, args.binary))
        records = read_receipts(args.out, plan)
        if not args.score:
            stamps = {}
            verify_fingerprint(plan, stamps)
            completed = {r['entry']['path']: r for r in records}
            count = 0
            for entry in plan['files']:
                old = completed.get(entry['path'])
                if old and not (args.retry_errors and old['status'] == 'unresolved'):
                    continue
                verify_fingerprint(plan, stamps)
                record = seal(measure(plan, entry, args.out))
                verify_fingerprint(plan, stamps)
                atomic(args.out / 'receipts' / receipt_name(entry), record)
                completed[entry['path']] = record
                count += 1
                summary = score(plan, list(completed.values()))
                atomic(args.out / 'summary.json', summary)
                print(json.dumps({'path': entry['path'], 'status': record['status'],
                                  'files': summary['files_recorded'], 'total_files': summary['files_total'],
                                  'bytes': summary['bytes_recorded'], 'total_bytes': summary['bytes_total']}, sort_keys=True), flush=True)
                if args.limit is not None and count >= args.limit:
                    break
            records = list(completed.values())
        result = score(plan, records)
        atomic(args.out / 'summary.json', result)
        print(json.dumps(result, indent=2, allow_nan=False))
        return 0
    except (ValueError, KeyError, TypeError, OSError, RuntimeError) as error:
        print('aeropopulation: ' + str(error), file=sys.stderr)
        return 1


if __name__ == '__main__':
    raise SystemExit(main())
