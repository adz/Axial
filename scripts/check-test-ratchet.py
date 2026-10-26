#!/usr/bin/env python3
import re, sys
from pathlib import Path

if len(sys.argv) != 3:
    raise SystemExit("usage: check-test-ratchet.py <Axial.Guardrails.lock> <test.log>")
lock, log = map(Path, sys.argv[1:])
baseline = {}
for line in lock.read_text(errors="replace").splitlines():
    if line.startswith("TEST  "):
        project, tests, _assertions = line[6:].split("\t")
        baseline[Path(project).stem] = (project, int(tests))

actual = {}
patterns = [
    re.compile(r"Passed!\s+- Failed:\s*\d+, Passed:\s*(\d+), Skipped:\s*\d+, Total:\s*\d+.*- ([^ /]+)\.dll"),
    re.compile(r"Test summary: total: (\d+), failed: \d+, succeeded: (\d+), skipped: \d+.*?([^ /]+)\.dll"),
]
for line in log.read_text(errors="replace").splitlines():
    m = patterns[0].search(line)
    if m:
        actual[m.group(2)] = int(m.group(1))

if not actual:
    print("test ratchet: no parsed test projects", file=sys.stderr)
    raise SystemExit(1)
failed = False
for name, (project, expected) in baseline.items():
    if expected == 0:
        continue
    ran = actual.get(name)
    if ran is None:
        print(f"test ratchet: no results for {project}", file=sys.stderr); failed = True
    elif ran < expected:
        print(f"test ratchet: {project}: tests {expected} -> {ran}", file=sys.stderr); failed = True
raise SystemExit(1 if failed else 0)
