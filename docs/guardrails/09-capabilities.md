---
title: Capabilities
linkTitle: Capabilities
description: Define a service that owns a raw API, so calls outside its implementation are reported with what to use instead.
---

# Capabilities

Your app talks to Stripe. You wrapped it in an `IPayments` service so tests can use a fake. Then someone calls
`Stripe.ChargeService` directly from a handler, and the tests charge a real card. Guardrails does not know about
Stripe, so nothing stops it.

A capability teaches it. It says which raw API a service owns, what callers use instead, and which files may call the
raw API because they implement or test the service. Axial's own `clock`, `filesystem`, `process`, `http` and the
rest are capabilities; `axial capabilities` lists them.

## Define a capability

Write `payments.capability.toml`:

```toml
name = "payments"
service = "IPayments"
example = "Payments.charge order"
live = "Payments.live"
fake = "TestPayments()"
replaces = [
    "any:Stripe.ChargeService",
    "ctor:Stripe.RefundService",
]
```

Add it in `Directory.Build.props`, so every project enforces it:

```xml
<ItemGroup>
  <AxialCapabilityDefinition Include="$(MSBuildThisFileDirectory)payments.capability.toml" />
</ItemGroup>
```

Now a call to `Stripe.ChargeService` outside a claimed file is a `raw-effect` error that says what to do:

```text
error raw-effect: This call uses Stripe.ChargeService directly, which the payments capability wraps. Replace it with
IPayments: add a `Payments: IPayments` field to the environment record, implement IHasPayments, and call the service
(for example `Payments.charge order`). Tests supply TestPayments().
```

| Key | Meaning |
| --- | --- |
| `name` | Unique across Axial's capabilities and yours. |
| `service` | The type or module callers use instead. |
| `replaces` | The APIs the service owns, in the [match grammar](package-rules.html#match-grammar). Required. |
| `env` | The environment interface. Default: `IHas` plus the service name without its `I`. |
| `field` | The environment record field. Default: `Payments: IPayments` for `IPayments`. |
| `example` | One call through the service, shown in the finding. |
| `live` | The production implementation, shown by `axial capabilities`. |
| `fake` | The test implementation, shown in the finding. |
| `level` | `enforce` (default), or `guide` to report uses as Info while callers migrate. |
| `guardrail` | The guardrail its rules belong to. Default: the capability's name. |

A malformed definition or unknown key fails the build. A capability cannot take an API another one owns:
`replaces = [ "any:System.IO.File" ]` fails and names `Axial.FileSystem`'s `IFileSystem`.

A package ships the definition and the item in its `buildTransitive` folder, so every project that references it
enforces the capability.

## Claim a capability

Mark each file that implements the service, or tests it against the real thing:

```xml
<!-- src/Shop.Payments/Shop.Payments.fsproj -->
<Compile Include="Payments.fs" />
<Compile Include="Live/Stripe.fs" AxialCapability="payments" />

<!-- tests/Shop.Payments.Tests/Shop.Payments.Tests.fsproj -->
<Compile Include="StripeSandboxTests.fs" AxialCapability="payments" />
```

`Live/Stripe.fs` may call Stripe. It may not read `DateTimeOffset.UtcNow`: it owns payments, not every effect.

Claims work the same for Axial's capabilities, and a file can claim several:

```xml
<Compile Include="ProcessLive.fs;ProcessRun.fs" AxialCapability="process" />
<Compile Include="Platform/ClockLive.fs" AxialCapability="clock;timezone" />
```

Claiming a capability nobody defined fails the build. A claim on a file that makes no raw call for it is a
[`stale-allow`](diagnostics/stale-allow.html) warning.

## Review

Claims and definitions are policy, so [policy review](policy-review.html) checks them. Only changes that allow more
need a person:

| Needs review | Passes on its own |
| --- | --- |
| A definition removed | A definition added |
| A `replaces` entry removed | A `replaces` entry added |
| `enforce` lowered to `guide` | `guide` raised to `enforce` |
| A project claims the capability for more files | A claim removed, or a claimed file renamed or moved |

Claims are counted per project, so moving a claim to another file in the same project passes.

`axial capabilities` lists every capability and the files that claim it (`--json` for tools). A capability that
several projects use is a candidate for its own package.
