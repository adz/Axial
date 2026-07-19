namespace Axial.Tests.Generated

open Axial.Schema.Derive

/// A named pickup location.
[<DeriveSchema>]
type PickupPoint = { Code: string }

/// A courier delivery with tracking.
[<DeriveSchema>]
type CourierDelivery = { TrackingUrl: string }

/// How a shipment reaches the customer.
[<DeriveUnion "kind">]
type DeliveryMethod =
    | Pickup of PickupPoint
    | Courier of CourierDelivery

/// Delivery urgency.
type ShipmentPriority =
    | Standard
    | Express
    | [<SchemaName "same-day">] SameDay

/// A shipment as first stored.
[<DeriveSchema>]
type ShipmentV1 =
    { /// Public shipment reference.
      [<Pattern "^SH-[0-9]+$">]
      Reference: string
      [<Email>]
      NotifyEmail: string
      Items: Map<string, int> }

/// A shipment with delivery method, priority, and weight.
[<DeriveSchema>]
type Shipment =
    { /// Public shipment reference.
      [<Pattern "^SH-[0-9]+$">]
      Reference: string
      [<Email; SchemaName "notify_email">]
      NotifyEmail: string
      Items: Map<string, int>
      [<Min 1; Distinct>]
      Tags: string list
      [<AtLeast 0.5>]
      WeightKg: decimal
      [<Default "express">]
      Priority: ShipmentPriority
      Delivery: DeliveryMethod
      Origin: PickupPoint option
      [<Default 1; AtLeast 1>]
      Boxes: int }
