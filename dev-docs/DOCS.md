# Axial Documentation Guide

Source of truth for Hugo + Docsy maintenance and documentation style.

## Audience and voice

Write for pragmatic F# devs solving dependency, async, and typed-failure problems.

- Skip category theory.
- Explain trade-offs vs `Async<Result<_,_>>` or FsToolkit.
- Prefer concise, code-first examples.
- Use direct, instructive language.
- Start with the user's problem, not the abstraction.
- Stay factual and avoid marketing, filler, or internal narrative.

## Hugo workflow

- Hand-written guides live in `/docs`.
- Source-lifted API reference pages live in `/docs/reference`.
- The Hugo site source lives in `/site`.
- Content is synced from `/docs` to `/site/content` via `scripts/populate-hugo-content.sh`.
- The site uses Hugo with the Docsy theme.

## Docs Source Of Truth

The docs system has two different kinds of pages:

- hand-written guides and landing pages live in `docs/`
- source-lifted API member pages live in `docs/reference/`

The API member pages are generated from the XML doc comments in `src/`. When you change public API wording, update the code comments first and then regenerate the reference pages.

The pipeline is:

1. Edit the public XML doc comments in `src/`.
2. Run `bash scripts/validate-docs.sh`.
3. Review regenerated reference pages with `bash scripts/preview-docs.sh` when browser inspection or screenshots are needed.
4. Update the hand-written guides in `docs/` as needed.

Do not hand-edit the generated API member pages unless you are fixing a generated-doc bug. If the source comments change, the generated markdown should change with them.

### Generated content

- The "Runnable Examples" page is generated from real code in `/examples/`.
- Use `scripts/generate-example-docs.sh` to refresh it.
- Do not edit `site/content/docs/examples/_index.md` directly; it is managed by the population script.
- The API reference member pages under `docs/reference/` are generated from the XML docs in `src/`.
- Update the generator in `scripts/generate-api-docs.mjs` when the reference structure changes, then rerun the script.
- The reference index pages and guide pages are hand-written markdown in `docs/`.

### Validate, preview, and deploy

Run `bash scripts/validate-docs.sh` for routine documentation validation. This regenerates examples, regenerates API reference pages, syncs Hugo content, and runs a static Hugo render into `.fsdocs/validate` without starting a long-lived server.

Run `bash scripts/preview-docs.sh` for a local live-reload server at `http://localhost:3000` when you need browser review or screenshots. Stop it with `SIGHUP`, `TERM`, `INT`, or by creating `$AXIAL_DOCS_PREVIEW_STOP_FILE` (default `/tmp/axial-docs-preview.stop`).

Use `bash scripts/build-docs-site.sh` only when preparing or checking deployment output. It builds all projects and writes the deployable site to `/output`, so it is heavier than the normal validation path.

## Documentation rules

- Structure API pages around the package and module hierarchy.
- Use F# code blocks with syntax highlighting (` ```fsharp `).
- Include "Source-Lifted Notes" for implementation-derived insights.
- Start every page with a one-sentence summary that begins with "This page shows".
- Use small, credible examples before semantic deep-dives.
- Add an XML doc comment with an example to every public function.
- State explicitly when not to use a feature.
- Avoid FAQ-style rhetorical questions.
- Avoid justifying why a section exists.
- Avoid promises about future features as an excuse for current gaps.

## LLM and Agent Optimization

We maintain specific files to optimize the experience for AI agents (Claude, Gemini, Codex) used by our library users.

- `llms.txt`: A machine-readable, high-density reference served at the site root. Optimized for "Agentic SEO" and crawlers.
- `docs/AGENT.md`: A user-facing guide titled "For AI Agents" that provides high-signal patterns and a Rosetta Stone for prompt injection.

When the public API changes, ensure both of these files are updated to reflect the current idiomatic "Golden Path."
