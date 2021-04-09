module ProtocolContextTracker
open Types

// A MailboxProcessor that contains and controls the shared state of the 
// WebSocket connections, called ServiceContexts. 
// 
// ServiceContexts may be added, removed, or a list of them returned.
// All active contexts may also be terminated as a group during shutdown.


let serviceContextTracker (mbx: CtxMailboxProcessor) =
    let serviceContextList = []
    
    let rec postLoop (sCTL: ServiceContext list) = async {
        let! msg = mbx.Receive()
        
        match msg with
        | AddCtx ctx ->
            ctx.guid.ToString() |> printfn "Adding %s to context tracker"
            return! ctx::sCTL |> postLoop
        | RemoveCtx ctx ->
            ctx.guid.ToString() |> printfn "Removing %s from context tracker" 
            return! sCTL |> List.filter (fun sctx -> ctx.guid <> sctx.guid) |> postLoop
        | GetCtx r ->
            r.Reply sCTL
            return! postLoop sCTL
        | KillAllCtx r -> 
            r.Reply (0 |> Some)
            return! postLoop []
        } 
        
    postLoop serviceContextList

// Creates the MailboxProcessor and passes it back. Used in Program.fs in order
// to pass to various consumers and/or complications.

let startProtocolContextTracker () = MailboxProcessor.Start serviceContextTracker
    