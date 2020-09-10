module MailboxContextTracker
open System
open System.Net.WebSockets
open System.Threading

open Types
open Common


// This function attempts to activate a new WebSocket with the indicated Server
// Cleans up the ClientWebSocket if the attempt creates an exception. Designed
// to require a delay to facilitate repeated attempts. Implemented in a try
// because this is actually more straightforward than setting up a Task timer.
let connectClientWebSocket (ct, delay) : Async<ClientWebSocket option> =    
    let connectString = sprintf "ws://%s:%s/" ct.host ct.port |> Uri
             
    let connectAttempt delay = async {
        let cws = new ClientWebSocket()
        do! Async.Sleep delay
        printfn "Connecting..."
        try
            do! cws.ConnectAsync(connectString, CancellationToken.None) |> Async.AwaitTask
            return cws |> Some
        with _ -> 
            cws.Dispose() 
            return None
        }
    connectAttempt delay


// This function creates a list of Server and delay pairs, which are fed 
// singlely to the connection attempt function. The notion of the number of 
// attempts for a given Server is encoded in the number of times the Server's
// entries appear in the list.
let generateTargetsWithDelays attempts delay connectionTargets =
    connectionTargets 
    |> List.map(fun c -> [for idx in [1..attempts] do yield (c, (idx * delay)) ])
    |> List.concat


// This function is a simple alias fully applying a default delay and number of
// connection attempts. Designed to stop the first time we get a Some so that
// repeated connections after the first successful one are prohibited.
let tryWebSocketConnection connectionTargets =
    connectionTargets 
    |> generateTargetsWithDelays 4 500
    |> List.tryPick(fun ct -> (connectClientWebSocket ct |> Async.RunSynchronously))


// A MailboxProcessor that contains and controls the shared state of the 
// WebSocket connections, called ServiceContexts. It tracks both active 
// ServiceContexts and the list of Servers that are contacted when a 
// connection dies or when manually prompted. ServiceContexts may be added,
// removed, or dropped. A list of active ServiceContexts will be returned on
// request. The incoming message handler is asynchronously started when a new
// WebSocket connection is successfully established.
let serviceContextTrackerAgent msgLoop (mbx: CtxMailboxProcessor) =
    let serviceContextList = []
    let targethosts = []
    
    let rec postLoop (ts: ConnectionTarget list, sctxs: ServiceContext list) = async {
        let! msg = mbx.Receive()
        
        match msg with
        | AddCtx ctx ->
            msgLoop mbx ctx |> Async.Start
            return! (ts, ctx::sctxs) |> postLoop
        | AddFailoverCt ctx -> return! (ctx :: ts, sctxs) |> postLoop
        | GetCt r -> 
            r.Reply ts
            return! postLoop (ts, sctxs)
        | GetCtx r ->
            r.Reply sctxs
            return! postLoop (ts, sctxs)
        | KillAllCtx -> 
            return! postLoop (ts, [])
        | RemoveCtx ctx ->
            let f = sctxs |> List.filter (fun sctx -> ctx.guid <> sctx.guid) 
            return! (ts, f) |> postLoop
        | ReconnectCtx r -> 
            match tryWebSocketConnection ts with
            | Some c -> 
                createServiceCtx c |> (postServiceCtxMsg mbx) |> Async.Start
                printfn "Connected"
                r.Reply  Ok
            | None -> 
                printfn "Connection failed"
                r.Reply Failed
            return! postLoop (ts, sctxs)
        } 
        
    postLoop (targethosts, serviceContextList)
