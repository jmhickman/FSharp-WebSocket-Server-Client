module MailboxContextTracker
open Types

// A MailboxProcessor that contains and controls the shared state of the 
// WebSocket connections, called ServiceContexts. It tracks active 
// ServiceContexts ServiceContexts may be added, removed, or dropped. A list of
// active ServiceContexts will be returned on request. The incoming message 
// handler is asynchronously started when a new WebSocket connection is 
// successfully established. More simple than its Client counterpart, as it 
// doesn't track previous sessions nor does it contain the notion of future
// connections.
let serviceContextTrackerAgent msgLoop (mbx: CtxMailboxProcessor) =
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
