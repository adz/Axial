namespace Axial.Tests

open System
open Axial.Validation
open Axial.Schema
open Swensen.Unquote
open Xunit

module ContextRulesTests =
    type private SupportTicket =
        {
            Priority: int
            HasAssignee: bool
        }

    type private TicketRuleError =
        | HighPriorityNeedsAssignee
        | ManualReviewRequired

    type private TrustedTicket(priority: int, hasAssignee: bool) =
        member _.Priority = priority
        member _.HasAssignee = hasAssignee

    let private needsAssignee ticket =
        if ticket.Priority >= 4 && not ticket.HasAssignee then
            Error(Diagnostics.singleton HighPriorityNeedsAssignee)
        else
            Ok ()

    let private needsReview ticket =
        if ticket.Priority >= 5 then
            Error(Diagnostics.singleton ManualReviewRequired)
        else
            Ok ()

    let private flattenedErrors result =
        match result with
        | Ok () -> []
        | Error diagnostics -> Diagnostics.flatten diagnostics

    [<Fact>]
    let ``apply accepts an already-trusted model when contextual rules pass`` () =
        let rules = [ needsAssignee; needsReview ]

        let ticket =
            {
                Priority = 3
                HasAssignee = false
            }

        test <@ ContextRules.apply rules ticket = Ok ticket @>

    [<Fact>]
    let ``apply accepts every model when the rule list is empty`` () =
        let ticket =
            {
                Priority = 5
                HasAssignee = false
            }

        test <@ ContextRules.apply ([]: (SupportTicket -> Result<unit, Diagnostics<TicketRuleError>>) list) ticket = Ok ticket @>

    [<Fact>]
    let ``apply accumulates diagnostics over an already-trusted model`` () =
        let rules =
            [ ContextRules.name "assignee" needsAssignee
              ContextRules.name "reviewer" needsReview ]

        let ticket =
            {
                Priority = 5
                HasAssignee = false
            }

        match ContextRules.apply rules ticket with
        | Ok _ -> failwith "Expected support-ticket rules to reject the ticket."
        | Error diagnostics ->
            test
                <@
                    Diagnostics.flatten diagnostics =
                        [ { Path = [ PathSegment.Name "assignee" ]
                            Error = HighPriorityNeedsAssignee }
                          { Path = [ PathSegment.Name "reviewer" ]
                            Error = ManualReviewRequired } ]
                @>

    [<Fact>]
    let ``apply returns the supplied trusted model instance without constructing a new one`` () =
        let rules =
            [ fun (ticket: TrustedTicket) ->
                  if ticket.Priority >= 4 && not ticket.HasAssignee then
                      ContextRules.failAt [ PathSegment.Name "assignee" ] HighPriorityNeedsAssignee
                  else
                      Ok () ]

        let ticket = TrustedTicket(3, false)

        match ContextRules.apply rules ticket with
        | Ok trusted -> test <@ Object.ReferenceEquals(trusted, ticket) @>
        | Error _ -> failwith "Expected rules to accept the trusted ticket."

    [<Fact>]
    let ``context selection is plain F# over plain rule lists`` () =
        // The same trusted ticket is acceptable to triage but not to auto-approval.
        let rulesFor context =
            match context with
            | "triage" -> [ needsAssignee ]
            | _ -> [ needsAssignee; ContextRules.name "review" needsReview ]

        let ticket =
            {
                Priority = 5
                HasAssignee = true
            }

        test <@ ContextRules.apply (rulesFor "triage") ticket = Ok ticket @>

        match ContextRules.apply (rulesFor "approval") ticket with
        | Ok _ -> failwith "Expected approval rules to require manual review."
        | Error diagnostics ->
            test
                <@
                    Diagnostics.flatten diagnostics =
                        [ { Path = [ PathSegment.Name "review" ]
                            Error = ManualReviewRequired } ]
                @>

    [<Fact>]
    let ``failAt creates field-attached failures`` () =
        let result =
            ContextRules.failAt [ PathSegment.Name "assignee" ] HighPriorityNeedsAssignee

        test
            <@
                flattenedErrors result =
                    [ { Path = [ PathSegment.Name "assignee" ]
                        Error = HighPriorityNeedsAssignee } ]
            @>

    [<Fact>]
    let ``custom creates SchemaError values for domain rule adapters`` () =
        let error =
            ContextRules.custom "ticket.review.required" "Priority 5 tickets require manual review."

        test
            <@
                error =
                    SchemaError.Custom(
                        "ticket.review.required",
                        Some "Priority 5 tickets require manual review."
                    )
            @>

    [<Fact>]
    let ``failCustom attaches a custom failure to the current node`` () =
        let result =
            ContextRules.failCustom "ticket.assignee.required" "High-priority tickets need an assignee."

        test
            <@
                flattenedErrors result =
                    [ { Path = []
                        Error =
                            SchemaError.Custom(
                                "ticket.assignee.required",
                                Some "High-priority tickets need an assignee."
                            ) } ]
            @>

    [<Fact>]
    let ``at scopes rule failures under a path`` () =
        let scopedRule =
            ContextRules.at [ PathSegment.Name "approval"; PathSegment.Name "reviewer" ] needsReview

        let ticket =
            {
                Priority = 5
                HasAssignee = true
            }

        test
            <@
                flattenedErrors (scopedRule ticket) =
                    [ { Path = [ PathSegment.Name "approval"; PathSegment.Name "reviewer" ]
                        Error = ManualReviewRequired } ]
            @>

    [<Fact>]
    let ``name key and index scope rule failures under nested segments`` () =
        let scopedRule =
            needsReview
            |> ContextRules.index 0
            |> ContextRules.key "regional"
            |> ContextRules.name "approvals"

        let ticket =
            {
                Priority = 5
                HasAssignee = true
            }

        test
            <@
                flattenedErrors (scopedRule ticket) =
                    [ { Path =
                            [ PathSegment.Name "approvals"
                              PathSegment.Key "regional"
                              PathSegment.Index 0 ]
                        Error = ManualReviewRequired } ]
            @>

    [<Fact>]
    let ``apply rejects null rule functions`` () =
        let ex =
            Assert.Throws<ArgumentNullException>(fun () ->
                ContextRules.apply
                    [ Unchecked.defaultof<SupportTicket -> Result<unit, Diagnostics<TicketRuleError>>> ]
                    { Priority = 1; HasAssignee = true }
                |> ignore)

        test <@ ex.ParamName = "rule" @>
