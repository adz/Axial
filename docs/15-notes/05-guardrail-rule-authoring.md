---
title: Guardrail rule authoring
linkTitle: Guardrail rule authoring
description: Declare package-specific guardrail rules with AxialGuardrailRule item metadata.
---

# Guardrail rule authoring

A package can contribute rules to a guardrail of its own, without writing a separate analyzer. Ship the rules in the
package's `buildTransitive` props, next to the item that turns the guardrail on. This page is the reference for the
`AxialGuardrailRule` metadata syntax and matching grammar; the [adoption guide](guardrails.html) shows the install
path.

## Declaring rules

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

Findings are reported as `AXG001`. In a file declared with `AxialGuardrailsBoundary`,
`// axial-allow-AXG001: payments live implementation` allows an intentional use.

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
