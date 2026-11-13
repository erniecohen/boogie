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
report = {'solver_sha256': hashlib.sha256(z3.read_bytes()).hexdigest(), 'runs': {}}
for source in sorted((fixtures / 'examples').glob('*.bpl')) + [Path('Test/test21/issue-284-canonical-payloads.bpl')]:
    identity = []
    for mode, driver, extra in [('baseline', base, []), ('legacy', candidate, ['/legacyNoncanonicalMapPayloads:1']), ('default', candidate, [])]:
        log = output / 'query.smt2'
        for old in output.glob('query*'): old.unlink()
        cmd = ['dotnet', str(driver), str(source), '/typeEncoding:a', '/prune:0', '/normalizeDeclarationOrder:0',
               '/normalizeNames:1', '/emitDebugInformation:0', '/proverOpt:PROVER_PATH=' + str(z3),
               '/proverOpt:O:smt.mbqi=true', '/timeLimit:15', '/proverLog:' + str(log)] + extra
        key = source.stem + '-' + mode
        try:
            p = subprocess.run(cmd, capture_output=True, timeout=120)
            entry = {'exit': p.returncode, 'command': cmd}
            data = (p.returncode, p.stdout, p.stderr, {f.name: f.read_bytes() for f in output.glob('query*')})
            (output / (key + '.stdout')).write_bytes(p.stdout)
            (output / (key + '.stderr')).write_bytes(p.stderr)
            identity.append(data)
        except subprocess.TimeoutExpired:
            entry = {'timeout': True, 'command': cmd}
            identity.append(None)
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
