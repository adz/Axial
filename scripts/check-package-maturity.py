#!/usr/bin/env python3
"""Check that the authored maturity table matches every packable source project."""
from pathlib import Path
import re
import sys

root = Path(__file__).resolve().parent.parent
page = (root / 'docs/15-notes/02-package-maturity.md').read_text()
rows = re.findall(r'^\| `(Axial(?:\.[\w]+)*)` \| (Experimental|Incubating|Stable) \|', page, re.M)
documented = dict(rows)
projects = {}
for project in (root / 'src').glob('Axial*/*.fsproj'):
    text = project.read_text()
    if '<IsPackable>false</IsPackable>' in text:
        continue
    match = re.search(r'<PackageMaturity>(.*?)</PackageMaturity>', text)
    projects[project.stem] = match.group(1) if match else None

if len(rows) != len(documented) or documented != projects:
    print(f'Maturity mismatch: docs={documented}, projects={projects}', file=sys.stderr)
    sys.exit(1)
print(f'Package maturity: {len(projects)} projects match documentation')
