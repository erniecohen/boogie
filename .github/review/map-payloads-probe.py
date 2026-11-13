"""Retain expected false controls, unknowns, and baseline/legacy solver bytes."""
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import sys

base, candidate, z3, output = map(Path, sys.argv[1:])
base, candidate, z3, output = [p.resolve() for p in (base, candidate, z3, output)]
output.mkdir(parents=True, exist_ok=True)
fixtures = Path('Test/test21/issue-284-evidence')
report = {'solver_sha256': hashlib.sha256(z3.read_bytes()).hexdigest(),
          'baseline_driver_sha256': hashlib.sha256(base.read_bytes()).hexdigest(),
          'candidate_driver_sha256': hashlib.sha256(candidate.read_bytes()).hexdigest(), 'runs': {}}
sources = sorted((fixtures / 'examples').glob('*.bpl')) + [Path('Test/test21/' + name) for name in ['issue-284-canonical-payloads.bpl', 'InterestingExamples1.bpl', 'MapOutputTypeParams.bpl']]
anchored = output / 'anchored'
anchored.mkdir(exist_ok=True)
for source in list(sources):
    target = anchored / (source.stem + '-erased.bpl')
    target.write_text(source.read_text() + '\n// Prevent the monomorphic fast path without constraining the theory.\nfunction EncodingAnchor<T>(x: T): T;\n')
    sources.append(target)
for source in sources:
    identity = []
    for mode, driver, extra in [('baseline', base, []), ('legacy', candidate, ['/legacyNoncanonicalMapPayloads:1']), ('default', candidate, [])]:
        log = output / 'query.smt2'
        for old in output.glob('query*'): old.unlink()
        cmd = ['dotnet', str(driver), str(source), '/typeEncoding:a', '/prune:0', '/normalizeDeclarationOrder:0',
               '/normalizeNames:1', '/emitDebugInformation:0', '/proverOpt:PROVER_PATH=' + str(z3),
               '/proverOpt:O:smt.mbqi=true', '/proverOpt:O:smt.mbqi.max_iterations=10', '/rlimit:16000000', '/timeLimit:15', '/proverLog:' + str(log)] + extra
        key = source.stem + '-' + mode
        source_sha256 = hashlib.sha256(source.read_bytes()).hexdigest()
        try:
            p = subprocess.run(cmd, capture_output=True, timeout=120)
            entry = {'exit': p.returncode, 'command': cmd, 'source_sha256': source_sha256, 'erased_smt': log.exists() and 'T@U' in log.read_text()}
            data = (p.returncode, p.stdout, p.stderr, {f.name: f.read_bytes() for f in output.glob('query*')})
            (output / (key + '.stdout')).write_bytes(p.stdout)
            (output / (key + '.stderr')).write_bytes(p.stderr)
            identity.append(data)
        except subprocess.TimeoutExpired:
            entry = {'timeout': True, 'command': cmd}
            identity.append(None)
        if log.exists() and '(check-sat)' in log.read_text():
            replay = output / (key + '-first-query.smt2')
            replay.write_text(log.read_text().split('(check-sat)', 1)[0] + '(check-sat)\n')
            try:
                direct = subprocess.run([str(z3), str(replay)], capture_output=True, timeout=30)
                (output / (key + '-direct.stdout')).write_bytes(direct.stdout)
                (output / (key + '-direct.stderr')).write_bytes(direct.stderr)
                entry['direct_result'] = direct.stdout.decode().strip()
            except subprocess.TimeoutExpired:
                entry['direct_timeout'] = True
        for generated in output.glob('query*'):
            shutil.copyfile(generated, output / (key + '-' + generated.name))
        report['runs'][key] = entry
    report[source.stem + '-legacy-identical'] = identity[0] is not None and identity[0] == identity[1]
for source in sorted((fixtures / 'proofs').glob('*.smt2')):
    p = subprocess.run([str(z3), str(source)], capture_output=True, timeout=90)
    (output / (source.stem + '.stdout')).write_bytes(p.stdout)
    (output / (source.stem + '.stderr')).write_bytes(p.stderr)
    report[source.name] = {'exit': p.returncode, 'stdout': p.stdout.decode()}
(output / 'report.json').write_text(json.dumps(report, indent=2))
print(json.dumps(report, indent=2))

# Expected false assertions are successful controls only when they remain unproved.
# A green job requires actual observations; recording a script error is not acceptance.
identities = [value for key, value in report.items() if key.endswith('-legacy-identical')]
controls = ['char-repair', 'char-source', 'iset-source', 'map-repair']
failures = []
if len(identities) != len(sources) or not all(identities):
    failures.append('baseline/legacy byte identity')
for name in controls:
    key = name + '-erased-default'
    run = report['runs'][key]
    if not run.get('erased_smt') or run.get('direct_result') not in ['sat', 'unknown']:
        failures.append(key + ': negative erased context')
for mode in ['baseline', 'legacy', 'default']:
    text = (output / ('issue-284-canonical-payloads-' + mode + '.stdout')).read_text()
    if 'Boogie program verifier finished with 6 verified, 1 error' not in text:
        failures.append(mode + ': canonical payload controls')
if report['iset-erased-repaired-core.smt2']['stdout'].strip() not in ['sat', 'unknown']:
    failures.append('repaired map kernel')
(output / 'gate.json').write_text(json.dumps({'failures': failures}, indent=2))
if failures:
    print('Unexpected comparison failures: ' + ', '.join(failures), file=sys.stderr)
    sys.exit(1)
