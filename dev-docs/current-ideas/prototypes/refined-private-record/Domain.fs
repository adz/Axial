namespace Prototype

open System

type Booking =
    private {
        Start: DateOnly
        End: DateOnly
    }

    static member Create(draft: BookingDraft) =
        if draft.Start <= draft.End then
            Ok { Start = draft.Start; End = draft.End }
        else
            Error "Start must be on or before End"
