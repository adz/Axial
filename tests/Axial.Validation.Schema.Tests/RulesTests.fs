namespace Axial.Tests

open System
open Axial.Validation
open Axial.Validation.Schema
open Swensen.Unquote
open Xunit

module RulesTests =
    type private SupportTicket =
        {
            Priority: int
            HasAssignee: bool
        }

    type private TicketRuleError =
        | HighPriorityNeedsAssignee
        | ManualReviewRequired

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
    let ``explicit Rules API creates an empty contextual rule set`` () =
        let ruleSet = Rules.empty<SupportTicket, TicketRuleError>

        test <@ ruleSet.GetType().Name.StartsWith("RuleSet") @>

    [<Fact>]
    let ``explicit Rules API creates and composes contextual rule sets`` () =
        let ruleSet =
            Rules.concat
                [ Rules.create needsAssignee
                  Rules.ofList [ needsReview ] ]

        test <@ ruleSet.GetType().Name.StartsWith("RuleSet") @>

    [<Fact>]
    let ``explicit Rules API creates field-attached failures`` () =
        let result =
            Rules.failAt [ PathSegment.Name "assignee" ] HighPriorityNeedsAssignee

        test
            <@
                flattenedErrors result =
                    [ { Path = [ PathSegment.Name "assignee" ]
                        Error = HighPriorityNeedsAssignee } ]
            @>

    [<Fact>]
    let ``explicit Rules API creates custom code and message failures`` () =
        let result =
            Rules.failCustomAt
                [ PathSegment.Name "assignee" ]
                "ticket.assignee.required"
                "High-priority tickets need an assignee."

        test
            <@
                flattenedErrors result =
                    [ { Path = [ PathSegment.Name "assignee" ]
                        Error =
                            SchemaError.Custom(
                                "ticket.assignee.required",
                                Some "High-priority tickets need an assignee."
                            ) } ]
            @>

    [<Fact>]
    let ``explicit Rules API creates custom SchemaError values for domain rule adapters`` () =
        let error =
            Rules.custom "ticket.review.required" "Priority 5 tickets require manual review."

        test
            <@
                error =
                    SchemaError.Custom(
                        "ticket.review.required",
                        Some "Priority 5 tickets require manual review."
                    )
            @>

    [<Fact>]
    let ``explicit Rules API scopes rule failures under a path`` () =
        let scopedRule =
            Rules.at [ PathSegment.Name "approval"; PathSegment.Name "reviewer" ] needsReview

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
    let ``explicit Rules API scopes rule failures under field key and index segments`` () =
        let scopedRule =
            needsReview
            |> Rules.index 0
            |> Rules.key "regional"
            |> Rules.name "approvals"

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
    let ``explicit Rules API rejects null rule functions`` () =
        let ex =
            Assert.Throws<ArgumentNullException>(fun () ->
                Rules.create (Unchecked.defaultof<SupportTicket -> Result<unit, Diagnostics<TicketRuleError>>>)
                |> ignore)

        test <@ ex.ParamName = "rule" @>
