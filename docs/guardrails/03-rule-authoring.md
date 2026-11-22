---
title: Guardrail rule authoring
linkTitle: Guardrail rule authoring
description: Define capabilities in .capability.toml files, claim them on Compile items, and add package-specific rules with AxialGuardrailRule.
---

# Guardrail rule authoring

## Capabilities

Every raw effect call is an error until it goes through a service. A capability is a service that owns a raw API: it
says what callers use instead, and which files may call the raw API because they implement or test the service.

There are two parts:

- **A definition** says what the capability is and which APIs it replaces. Axial's own capabilities (`clock`,
  `filesystem`, `process`, `http` and the rest) are built in; `axial capabilities` lists them.
- **A claim** marks a file as working directly with the capability. It is `AxialCapability` metadata on the file's
  `Compile` item. Only claimed files may call the API, and a claimed file may call only the APIs it claims.

### Define a capability

Write a `<name>.capability.toml` file:

```toml
# payments.capability.toml
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

Name it with an item, in `Directory.Build.props` so that every project enforces it:

```xml
<ItemGroup>
  <AxialCapabilityDefinition Include="$(MSBuildThisFileDirectory)payments.capability.toml" />
</ItemGroup>
```

A package ships the same two pieces in its `buildTransitive` folder, so every project that references it enforces the
capability. Its own implementation is already compiled, so a package's consumers claim nothing.

| Key | Meaning |
| --- | --- |
| `name` | The capability's name, unique across Axial's capabilities and yours. |
| `service` | The type or module callers use instead. |
| `replaces` | The APIs the service owns, in the [match grammar](#match-grammar). Required. |
| `env` | The environment interface. Default: `IHas` plus the service name without its leading `I`. |
| `field` | The environment record field. Default: `Payments: IPayments` for `IPayments`. |
| `example` | One call through the service, shown in the finding. |
| `live` | The production implementation, shown by `axial capabilities`. |
| `fake` | The test implementation, shown in the finding. |
| `level` | `enforce` (default), or `guide` to report outside uses as information while callers migrate. |
| `guardrail` | The guardrail its rules belong to. Default: the capability's name, enabled by the definition. |

A malformed definition fails the build with its reason, and so does an unknown key. A capability cannot replace an API
that another capability or rule already covers: `replaces = [ "any:System.IO.File" ]` fails and names
Axial.FileSystem's `IFileSystem` instead.

A call to `Stripe.ChargeService` outside a claimed file is an `AXG001` error that says what to change:

```text
error AXG001: This call uses Stripe.ChargeService directly, which the payments capability wraps. Replace it with
IPayments: add a `Payments: IPayments` field to the environment record, implement IHasPayments, and call the service
(for example `Payments.charge order`). Tests supply TestPayments().
```

### Claim a capability

Mark each file that implements the service, or tests it against the real thing:

```xml
<!-- src/Shop.Payments/Shop.Payments.fsproj -->
<Compile Include="Payments.fs" />
<Compile Include="Live/Stripe.fs" AxialCapability="payments" />

<!-- tests/Shop.Payments.Tests/Shop.Payments.Tests.fsproj -->
<Compile Include="StripeSandboxTests.fs" AxialCapability="payments" />
```

Claims work the same way for Axial's own capabilities, and one file can claim several:

```xml
<Compile Include="ProcessLive.fs;ProcessRun.fs" AxialCapability="process" />
<Compile Include="Platform/ClockLive.fs" AxialCapability="clock;timezone" />
```

Inside a claimed file, calls to the claimed API are allowed and counted. Any other raw effect there, such as
`DateTimeOffset.UtcNow` in the payments file, is still reported: the file owns the payments API, not every effect. A
claim of a capability nobody defined fails the build.

### Allowances for gaps

When Axial or a capability cannot do something yet, one call may be allowed with a comment that names the gap:

```fsharp
use pipe = new NamedPipeServerStream(name) // axial-allow-AXG001: Axial.Process has no named pipes yet (gap: process-named-pipes)
```

An allowance covers its own line or the next one, never a whole file, and it must end with `(gap: name)`. It works in
any file unless the rule level is `forbid`. Allowances should be rare: each is a missing feature, and `axial check`
lists them by gap name.

### Review

Every claim, definition and allowance is recorded in the policy lock, and `axial policy review` judges changes by
direction. A change that only blocks more passes without review: a new definition, a new `replaces` entry, `guide` to
`enforce`, a claim removed, or a claimed file renamed or moved (the project claims as many files as before). A change
that allows more needs review: a definition or `replaces` entry removed, `enforce` to `guide`, a project claiming more
files, or a new allowance.

`axial capabilities` lists every capability with the files that claim it (`--json` for tools). `axial check` counts
the raw effect calls each project was allowed: in claimed files, by gap allowances, and in host files. A capability
that several projects use is a candidate for its own effect package.

## Declaring rules

A package can contribute rules to a guardrail of its own, without writing a separate analyzer. Ship the rules in the
package's `buildTransitive` props, next to the item that turns the guardrail on. This page is the reference for the
`AxialGuardrailRule` metadata syntax and matching grammar; the [adoption guide](/guardrails/guardrails.html) shows the install
path. Prefer a [capability](#capabilities) when a service owns the API: its findings name the service, the environment
field and the fake, and the files that claim it may call the API.

Each rule is one `<AxialGuardrailRule>` item:

```xml
<ItemGroup>
  <AxialGuardrail Include="Acme.Payments" />
  <AxialGuardrailRule Include="acme.gateway-client"
      Guardrail="Acme.Payments"
      Category="payments"
      Match="ctor:Acme.Gateway.GatewayClient"
      Scope="anywhere"
      Message="constructs the gateway client directly, which bypasses the payments service"
      Replacement="Acme.Payments' IPayments service" />
</ItemGroup>
```

Findings are reported as `AXG001`. No file can claim a rule declared this way; define a capability instead when a
service owns the API. A rare [gap allowance](#allowances-for-gaps) allows one call unless the rule level is `forbid`.

## Metadata

| Item | Meaning |
| --- | --- |
| `Include` | A stable rule id, conventionally `{guardrail}.{name}`. |
| `Guardrail` | The guardrail the rule belongs to, matching an `<AxialGuardrail>` item. |
| `Category` | The effect category used to group and document the contributed rule. |
| `Match` | The symbol to flag; see the grammar below. |
| `Scope` | `anywhere`, or `insideFlow` to report the call only inside `flow { }`. |
| `Message` | The diagnostic text naming the direct call and what it bypasses. |
| `Replacement` | The service to use instead, shown in the finding. |

## Match grammar

`Match` is one of three forms:

- `ctor:Type` — flag construction of `Type`.
- `member:Type::Member1,Member2` — flag the named members of `Type`.
- `any:Type` — flag any reference to `Type`.

Item metadata cannot contain a semicolon; write it as `%3B`.
