---
title: Guardrail rule authoring
linkTitle: Guardrail rule authoring
description: Declare capabilities with AxialCapability items, and package-specific rules with AxialGuardrailRule.
---

# Guardrail rule authoring

## Declaring a capability

A capability is a service that replaces a raw API: `IPayments` over a payment gateway's client, `IDns` over
`System.Net.Dns`. Declare it once and Guardrails points every direct use of the wrapped API at the service:

```xml
<ItemGroup>
  <AxialCapability Include="payments"
      Service="IPayments"
      Example="Payments.charge order"
      Live="Payments.live"
      Fake="TestPayments()"
      Replaces="any:Stripe.ChargeService|ctor:Stripe.RefundService" />
</ItemGroup>
```

A call to `Stripe.ChargeService` then reports:

```text
Info AXG001: This call uses Stripe.ChargeService directly, which the payments capability wraps. Use IPayments: give
the environment record a `Payments: IPayments` field and implement IHasPayments, then call it, for example
`Payments.charge order`. Production: Payments.live. Tests: TestPayments().
```

The finding is information. It never fails a build, never enters the baseline and needs no `axial-note`. Files that
declare or implement the service (`type IPayments`, `interface IPayments with`, `{ new IPayments with ... }`) get no
finding, because that is where the wrapped API belongs. Set `Level="enforce"` to report the capability like
Axial's own rules instead, at the file role's `effects.ambient` level.

| Item | Meaning |
| --- | --- |
| `Include` | The capability's name, unique across Axial's capabilities and yours. |
| `Service` | The type or module callers use instead. |
| `Replaces` | The APIs the service wraps, in the [match grammar](#match-grammar), separated by `\|`. Required. |
| `Env` | The environment interface. Default: `IHas` plus the service name without its leading `I`. |
| `Field` | The environment record field. Default: `Payments: IPayments` for `IPayments`. |
| `Example` | One call through the service, shown in the finding. |
| `Live` | The production implementation, shown in the finding. |
| `Fake` | The test implementation, shown in the finding. |
| `Level` | `guide` (default) or `enforce`. |
| `Guardrail` | The guardrail its rules belong to. Default: the capability's name, enabled by the declaration. |

A malformed declaration, such as one with no `Replaces`, fails the build with its reason.

`axial add service Payments --replaces "any:Stripe.ChargeService"` writes `Payments.fs` with the service interface,
the environment interface, an accessor, a live implementation stub and a test fake, and prints the item to add.
`axial capabilities` lists Axial's capabilities and every one a built project declares (`--json` for tools).

## Declaring rules

A package can contribute rules to a guardrail of its own, without writing a separate analyzer. Ship the rules in the
package's `buildTransitive` props, next to the item that turns the guardrail on. This page is the reference for the
`AxialGuardrailRule` metadata syntax and matching grammar; the [adoption guide](/guardrails/guardrails.html) shows the install
path. Prefer an `<AxialCapability>` when a service exists: its findings name the service, the environment field and
the fake.

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

Findings are reported as `AXG001`. Put normal live implementations in a TOML `capabilities` role glob. In an
exceptional exact file listed under `[boundaries].files`, `// axial-allow-AXG001: payments live implementation`
allows an intentional use unless the rule level is `forbid`.

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
