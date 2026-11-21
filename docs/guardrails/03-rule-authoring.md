---
title: Guardrail rule authoring
linkTitle: Guardrail rule authoring
description: Declare capabilities with AxialCapability items, and package-specific rules with AxialGuardrailRule.
---

# Guardrail rule authoring

## Declaring a capability

Every raw effect call is an error until it goes through a service. Where no service can do the job, declare a
capability: the service that owns the raw API, and the files that implement it. Those files are the only place that
may call the API.

```xml
<ItemGroup>
  <AxialCapability Include="payments"
      Service="IPayments"
      Example="Payments.charge order"
      Live="Payments.live"
      Fake="TestPayments()"
      Replaces="any:Stripe.ChargeService|ctor:Stripe.RefundService"
      Implementation="Payments/Live.fs" />
</ItemGroup>
```

A call to `Stripe.ChargeService` anywhere else is an `AXG001` error that says what to change:

```text
error AXG001: This call uses Stripe.ChargeService directly, which the payments capability wraps. Replace it with
IPayments: add a `Payments: IPayments` field to the environment record, implement IHasPayments, and call the service
(for example `Payments.charge order`). Tests supply TestPayments().
```

Inside `Payments/Live.fs`, calls to the Stripe types are allowed and counted. Any other raw effect there, such as
`DateTimeOffset.UtcNow`, is still reported: the file owns the payments API, not every effect. A capability cannot
claim an API that another capability or rule already covers; a declaration of `any:System.IO.File` fails and names
Axial.FileSystem's `IFileSystem` instead.

| Item | Meaning |
| --- | --- |
| `Include` | The capability's name, unique across Axial's capabilities and yours. |
| `Service` | The type or module callers use instead. |
| `Replaces` | The APIs the service owns, in the [match grammar](#match-grammar), separated by `\|`. Required. |
| `Implementation` | The file, or a glob relative to the project, that implements the service. Required. |
| `Env` | The environment interface. Default: `IHas` plus the service name without its leading `I`. |
| `Field` | The environment record field. Default: `Payments: IPayments` for `IPayments`. |
| `Example` | One call through the service, shown in the finding. |
| `Live` | The production implementation, shown by `axial capabilities`. |
| `Fake` | The test implementation, shown in the finding. |
| `Level` | `enforce` (default), or `guide` to report outside uses as information while callers migrate. |
| `Guardrail` | The guardrail its rules belong to. Default: the capability's name, enabled by the declaration. |

A malformed declaration fails the build with its reason.

`axial capabilities` lists Axial's capabilities and every one a built project declares, with its implementation
files (`--json` for tools). `axial check` counts the raw effect calls each project was allowed: inside capability
implementations, by boundary comments, and in host files. A capability that several projects use is a candidate for
its own effect package.

## Declaring rules

A package can contribute rules to a guardrail of its own, without writing a separate analyzer. Ship the rules in the
package's `buildTransitive` props, next to the item that turns the guardrail on. This page is the reference for the
`AxialGuardrailRule` metadata syntax and matching grammar; the [adoption guide](/guardrails/guardrails.html) shows the install
path. Prefer an `<AxialCapability>` when a service owns the API: its findings name the service, the environment field
and the fake, and its implementation files may call the API.

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

Findings are reported as `AXG001`. A rule declared this way has no implementation that may call its API; declare an
`<AxialCapability>` with an `Implementation` instead when a service owns it. In an exceptional exact file listed under
`[boundaries].files`, `// axial-allow-AXG001: payments live implementation` allows an intentional use unless the rule
level is `forbid`; every such use is counted by `axial check`.

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
