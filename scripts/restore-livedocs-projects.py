#!/usr/bin/env python3
"""Restore documentation inputs after Fable's single-framework restore rewrites shared assets."""
import json
import subprocess
from pathlib import Path

root = Path(__file__).resolve().parent.parent
config = json.loads((root / ".livedocs/config.json").read_text())
projects = dict.fromkeys(
    config.get("projects", [])
    + [project for docs_set in config.get("docsSets", []) for project in docs_set.get("projects", [])]
)
for project in projects:
    subprocess.run(["dotnet", "restore", project, "--verbosity", "quiet"], cwd=root, check=True)
