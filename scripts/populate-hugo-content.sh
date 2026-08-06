#!/usr/bin/env bash

set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
flow_dir="$root_dir/site/content/flow"

rm -rf "$root_dir/site/content"
mkdir -p "$flow_dir" "$root_dir/site/static/flow" "$root_dir/site/static/content"
cp -r "$root_dir/docs/flow/." "$flow_dir/"
rm -f "$flow_dir/llms.txt"
cp "$root_dir/docs/index.md" "$root_dir/site/content/_index.md"
cp "$root_dir/llms.txt" "$root_dir/site/static/llms.txt"
cp "$root_dir/docs/flow/llms.txt" "$root_dir/site/static/flow/llms.txt"
cp -r "$root_dir/docs/content/." "$root_dir/site/static/content/"

find "$flow_dir" -type f -name '*.md' -print0 |
  node -e '
    const fs = require("node:fs");
    for (const path of fs.readFileSync(0, "utf8").split("\0")) {
      if (!path) continue;
      const content = fs.readFileSync(path, "utf8")
        .split(/(?<=\n)/)
        .filter(line => !line.startsWith("# "))
        .join("");
      const end = content.indexOf("\n---", 4);
      if (end < 0) throw new Error(`missing frontmatter: ${path}`);
      let frontmatter = content.slice(0, end);
      frontmatter = /^type:/m.test(frontmatter)
        ? frontmatter.replace(/^type:.*$/m, "type: docs")
        : frontmatter + "\ntype: docs";
      fs.writeFileSync(path, frontmatter + content.slice(end));
    }
  '
