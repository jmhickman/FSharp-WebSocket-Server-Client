module MailboxContextTracker
open Types
open Common

///
/// MailboxProcessor
///

// Mostly a bunch of partial applications that set up this scope, and then a 
// relatively simple set of handlers for each type of Msg the processor cares
// about. 
let serviceContextTrackerAgent 
    (msgLoop: IncomingMessageLoop) 
    (mbx: MailboxProcessor<ContextTrackerMessage>) 
    =
    let serviceContextList = []
    
    let rec postLoop (sCTL: ServiceContext list) = async {
        let! msg = mbx.Receive()
        
        match msg with
        | AddCtx ctx ->
            msgLoop mbx ctx |> Async.Start
            return! ctx::sCTL |> postLoop
        | RemoveCtx ctx ->
            return! sCTL |> List.filter (fun sctx -> ctx.guid <> sctx.guid) |> postLoop
        | GetCtx r ->
            r.Reply sCTL
            return! postLoop sCTL
        | KillAllCtx r -> 
            r.Reply (0 |> Some)
            return! postLoop []
        } 
        
    postLoop serviceContextList
