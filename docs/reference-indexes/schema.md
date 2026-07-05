---
title: Reference
type: docs
notoc: true
weight: 100
---

API reference for the Schema area, grouped by package.

## Axial.Schema

- [`Schema`](./schema/) — the portable model declaration: builders, fields, constraints, and metadata inspection.

## Axial.Validation.Schema

- [`Schema Interpreters`](./schema/interpreters/) — input parsing, model validation, and rules over a schema.

## Axial.Codec

- [`Codec`](./codec/) — compiled JSON codecs and JSON Schema generation from the same declaration.

## Axial.Refined

- [`Refined`](./refined/) — parse and refine single values into types that carry their own proof.

## Axial.Validation

- [`Validation`](./validation/) — accumulating validation and the `validate {}` builder.
- [`Diagnostics`](./diagnostics/) — path-aware error trees: inspection, merging, and rendering.
