namespace Prototype

[<RequireQualifiedAccess>]
module BookingGenerated =
    let private project (booking: Prototype.Booking) : BookingDraft =
        { Start = booking.Start; End = booking.End }

    let create (draft: BookingDraft) =
        Prototype.Booking.Create(draft)

    let update (edit: BookingDraft -> BookingDraft) (booking: Prototype.Booking) =
        booking |> project |> edit |> create
