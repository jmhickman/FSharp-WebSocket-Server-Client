module MailboxContextTracker
open System
open System.Net.WebSockets
open System.Threading

open Types

// A MailboxProcessor that contains and controls the shared state of the 
// WebSocket connections, called ServiceContexts. It tracks both active 
// ServiceContexts and the list of Servers that are contacted when a 
// connection dies or when manually prompted. ServiceContexts may be added,
// removed, or dropped. A list of active ServiceContexts will be returned on
// request. The incoming message handler is asynchronously started when a new
// WebSocket connection is successfully established.
let serviceContextTrackerAgent 
    (mbx: CtxMailboxProcessor) 
    =
    let serviceContextList = []
    
    let rec postLoop (sctxs: ServiceContext list) = async {
        let! msg = mbx.Receive()
        
        match msg with
        | AddCtx ctx ->
            return! ctx::sctxs |> postLoop
        | GetCtx r ->
            r.Reply sctxs
            return! postLoop sctxs
        | KillAllCtx -> 
            return! postLoop []
        | RemoveCtx ctx ->
            return! sctxs |> List.filter (fun sctx -> ctx.guid <> sctx.guid) |> postLoop
        } 
        
    postLoop serviceContextList

// Creates the MailboxProcessor and passes it back. Used in Program.fs in order
// to pass to various consumers and/or complications.
let getCtxbox () = MailboxProcessor.Start serviceContextTrackerAgent