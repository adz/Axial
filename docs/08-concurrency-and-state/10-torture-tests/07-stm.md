---
title: STM
description: "Hundreds of transfers between accounts run as STM transactions while an auditor reads every balance and patient withdrawals wait for funds."
---

# STM

Hundreds of [STM](../stm.html) transfers move money between six accounts while an auditor reads every balance in one
transaction, and a fifth of the transfers are interrupted.

Run it with `dotnet run --project examples/Axial.TortureTest -- stm 100`.

## What it does

- A transfer checks that the source can cover the amount and uses `STM.retry` if it cannot. `STM.orElse` turns that
  into a skipped transfer instead of a wait.
- Ten patient withdrawals wait with `STM.retry` until a reserve account can cover them. The reserve is funded only
  after the transfers finish.
- An auditor sums every balance in one transaction, many times, while the transfers run.

{{< snippet id="torture-stm" mode="no-check" reason="Compiled and run as part of examples/Axial.TortureTest, which also defines the Round and Check helpers it uses" >}}

## What each check proves

| Check | Guarantee |
| --- | --- |
| Every audit saw the whole amount, never a transfer half done | A transaction sees a consistent snapshot of every `TRef` it reads. |
| The total is unchanged after every transfer | An interrupted transaction commits all of its writes or none of them. |
| No account ever went negative | A transaction's check and its write commit together. |
| Every patient withdrawal completed once the reserve was funded | `STM.retry` waits until a `TRef` it read changes, then runs the transaction again. |
| Some transfers went through | The contention did not starve every transaction. |
