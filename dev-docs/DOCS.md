# Documentation guide

Axial has one user-facing documentation area: `docs/flow/`. The root landing page is `docs/index.md`; `llms.txt` and `docs/flow/llms.txt` are hand-written machine entry points.

Write guides for library users under `docs/flow/`. Keep contributor instructions under `dev-docs/` or `AGENTS.md`, never in user documentation.

## Sources and generated output

- Public API facts originate in source XML comments.
- `scripts/docgen` generates `docs/flow/reference/**`.
- `scripts/generate-example-docs.sh flow` regenerates `docs/flow/examples.md` from runnable projects.
- `scripts/populate-hugo-content.sh` stages guides into ignored `site/content/**`.
- `site/public/**`, `output/**`, `.fsdocs/**`, and build artifacts are generated and untracked.

Do not hand-edit generated reference pages as the primary fix. Update source comments or generator inputs and regenerate.

## Style

Name the API and behavior directly. Prefer short executable examples to restating signatures. Use fenced `fsharp` blocks for F# and verify every local link.

## Commands

```bash
bash scripts/generate-example-docs.sh flow
bash scripts/generate-api-docs.sh flow
bash scripts/validate-docs.sh
npm run build --prefix site
```

Run the full validation and site build at phase or release boundaries.
