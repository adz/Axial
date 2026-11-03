#!/usr/bin/env python3
"""Compare two exported Full matrices, including exact findings and expected project/TFM coverage.

Usage: scripts/check-guardrails-full-equivalence.py BEFORE AFTER
The only accepted message change is AXG010's catch-narrowing guidance; acknowledgement reasons stay exact.
"""
import collections
import json
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

OLD = "Consider propagating, recording, or reporting this exception; this catch-all handler ignores it."
NEW = ("This catch-all handler ignores the exception. Consider narrowing the catch to the specific exception type(s), "
       "for example | :? FileNotFoundException ->. A typed catch need not use the exception value.")


def matrix(directory):
    directory = Path(directory)
    expected = {p.read_text().strip() for p in directory.glob("*.expected")}
    result = {}
    counts = collections.Counter()
    for path in directory.rglob("*.xml"):
        if path.name.endswith(".timings.xml"):
            continue
        root = ET.parse(path).getroot()
        assert root.tag == "result" and root.get("complete") == "true" and root.get("mode") == "Full", path
        identity = root.attrib["project"] + "|" + root.attrib["targetFramework"]
        assert identity not in result, f"duplicate identity: {identity}"
        coverage, findings = [], []
        for entry in root.findall("coverage"):
            assert entry.get("status") == "ran" and entry.get("mode") == "Full", (path, entry.attrib)
            pair = entry.attrib["rule"], entry.attrib["file"]
            coverage.append(pair)
            for finding in entry.findall("finding"):
                text = finding.text or ""
                if finding.get("code") == "AXG010":
                    text = text.replace(OLD, "AXG010 guidance", 1).replace(NEW, "AXG010 guidance", 1)
                findings.append((pair, sorted(finding.attrib.items()), text))
        assert len(coverage) == len(set(coverage)), f"duplicate rule/file coverage: {identity}"
        result[identity] = sorted(coverage), sorted(findings)
        counts.update(identities=1, coverage=len(coverage), findings=len(findings))
    assert expected and expected == set(result), f"missing/unexpected project/TFM exports in {directory}"
    return result, counts


def compare(before, after):
    original, counts = matrix(before)
    updated, new_counts = matrix(after)
    assert original.keys() == updated.keys(), "project/TFM matrix changed"
    for identity in original:
        assert original[identity] == updated[identity], f"coverage/findings differ: {identity}"
    assert counts == new_counts
    print(json.dumps(dict(equivalent=True, **counts), sort_keys=True))


if __name__ == "__main__":
    if len(sys.argv) != 3:
        sys.exit(__doc__)
    compare(*sys.argv[1:])
