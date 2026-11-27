---
title: Package rules
linkTitle: Package rules
description: Ship simple rules with a package using AxialGuardrailRule items, without writing an analyzer.
---

# Package rules

Your package has a type that callers should not construct themselves, such as a gateway client that the package's
service already manages. You can report direct use without writing an analyzer, by shipping an
`<AxialGuardrailRule>` item in the package's `buildTransitive` props.

Prefer a [capability](capabilities.html) when a service owns the API: its finding names the service, the environment
field and the fake, and files that implement the service can claim it. A package rule only reports.

```xml
<ItemGroup>
  <AxialGuardrail Include="Acme.Payments" />
  <AxialGuardrailRule Include="acme.gateway-client"
      Guardrail="Acme.Payments"
      Match="ctor:Acme.Gateway.GatewayClient"
      Scope="anywhere"
      Message="constructs the gateway client directly, which bypasses the payments service"
      Replacement="Acme.Payments' IPayments service" />
</ItemGroup>
```

Findings are reported as `raw-effect`. No file can claim a package rule. An
[allowance](allowances-and-notes.html) permits one call, unless the level is `forbid`.

## Metadata

| Item | Meaning |
| --- | --- |
| `Include` | A stable rule id, by convention `{guardrail}.{name}`. |
| `Guardrail` | The guardrail it belongs to, matching an `<AxialGuardrail>` item. |
| `Match` | What to report; see below. |
| `Scope` | `anywhere`, or `insideFlow` to report only inside `flow { }`. |
| `Message` | What the call does wrong. |
| `Replacement` | What to use instead. |

## Match grammar

- `ctor:Type` reports construction of `Type`.
- `member:Type::Member1,Member2` reports the named members.
- `any:Type` reports any use of `Type`.

MSBuild metadata cannot contain a semicolon; write `%3B`.
