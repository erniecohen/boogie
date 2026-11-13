#!/usr/bin/env python3
"""Small pinned-solver comparison; the unit tests control delayed/dead/hung pings."""
import json
import os
from pathlib import Path
import subprocess
import sys
import time
import xml.etree.ElementTree as ET

driver, solver, destination = sys.argv[1:]
out = Path(destination)
out.mkdir(parents=True, exist_ok=True)
version = subprocess.check_output([solver, "--version"], text=True).strip()
assert version.startswith("Z3 version 5.1.0 "), version
source = "Test/commandline/IdleHandshake.bpl"
expected = Path(source + ".expect").read_bytes()
reports = {}
for mode in ("default", "legacy"):
    prefix = out / mode
    command = ["dotnet", driver, "/vcsCores:1", "/timeLimit:0", "/useBaseNameForFileName",
               f"/proverOpt:PROVER_PATH={solver}", f"/xml:{prefix}.xml",
               f"/proverLog:{prefix}.smt2", source]
    if mode == "legacy":
        command.append("/legacySolverIdleTimeout:1")
    start = time.monotonic()
    result = subprocess.run(command, capture_output=True, timeout=60, check=True)
    wall = time.monotonic() - start
    prefix.with_suffix(".stdout").write_bytes(result.stdout)
    prefix.with_suffix(".stderr").write_bytes(result.stderr)
    assert result.stdout == expected, (mode, result.stdout)
    assert not result.stderr, (mode, result.stderr)
    rows = [(method.attrib["name"], method.find("conclusion").attrib["outcome"],
             int(method.find("conclusion").attrib["resourceCount"]))
            for method in ET.parse(str(prefix) + ".xml").findall("method")]
    assert [(r[0], r[1]) for r in rows] == [("Healthy", "correct"), ("NegativeControl", "errors")], rows
    reports[mode] = {"rows": rows, "total_ru": sum(r[2] for r in rows), "wall_seconds": wall,
                     "verified": 1, "errors": 1, "exit": result.returncode}
assert reports["default"]["rows"] == reports["legacy"]["rows"], reports
# These tiny tasks have healthy handshakes. Controlled unit tests establish retention
# under a slow handshake, where the physical command histories intentionally differ.
assert (out / "default.smt2").read_bytes() == (out / "legacy.smt2").read_bytes()
report = {"solver": version, "modes": reports, "stdout_stderr_smt_equal": True}
text = json.dumps(report, indent=2)
(out / "report.json").write_text(text + "\n")
print(text)
if "GITHUB_STEP_SUMMARY" in os.environ:
    with open(os.environ["GITHUB_STEP_SUMMARY"], "a") as summary:
        summary.write("### Idle-handshake pinned-solver comparison\n\n```json\n" + text + "\n```\n")
