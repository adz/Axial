#!/usr/bin/env python3
"""Print '<passed> <failed> <projects>' for VSTest and Microsoft.Testing.Platform logs."""
import re
import sys

text = open(sys.argv[1], encoding="utf-8", errors="replace").read()
classic = re.findall(
    r"^(?:Passed|Failed)!\s+-\s+Failed:\s*(\d+),\s+Passed:\s*(\d+)", text, re.M
)
modern = re.findall(
    r"^Test summary:.*?total:\s*(\d+).*?failed:\s*(\d+).*?succeeded:\s*(\d+)",
    text,
    re.M | re.I,
)
failed = sum(int(failed) for failed, _ in classic) + sum(
    int(failed) for _, failed, _ in modern
)
passed = sum(int(passed) for _, passed in classic) + sum(
    int(passed) for _, _, passed in modern
)
print(passed, failed, len(classic) + len(modern))
