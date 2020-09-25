module MailboxConnectionTracker
open System
open System.Threading
open System.Net.WebSockets

open Types
open Common
open WebSocketMsgHandlers

// This function attempts to activate a new WebSocket with the indicated Server
// Cleans up the ClientWebSocket if the attempt creates an exception. Designed
// to require a delay to facilitate repeated attempts. Implemented in a try
// because this is actually more straightforward than setting up a Task timer.
let connectClientWebSocket (ct, delay) : Async<ClientWebSocket option> =    
    let connectString = sprintf "ws://%s:%s/" ct.host ct.port |> Uri
    
    let connectAttempt delay = async {
        let cws = new ClientWebSocket()
        //cws.Options.RemoteCertificateValidationCallback <- (fun _ _ _ _ -> true)
        do! Async.Sleep delay
        printfn "Connecting..."
        try
            do! cws.ConnectAsync(connectString, CancellationToken.None) |> Async.AwaitTask
            return cws |> Some
        with exn -> 
            printfn "%A" exn.Message
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


// The client needs additional functionality to manage connections, so that
// is done here. Broken out of the ContextTracker because of cyclic dep issues
// Works similarly; a recursive function that passes a list to itself.
let serviceConnectionTrackerAgent 
    (dimbx: DomainMailboxProcessor)
    (ctxmbx: CtxMailboxProcessor)
    (mbx: CtMailboxProcessor) 
    =
    let targethosts = []


    let rec postLoop (ts: ConnectionTarget list) = async {
        let! msg = mbx.Receive()
    
        match msg with
        | AddFailoverCt ctx -> return! (ctx :: ts) |> postLoop
        | GetCt r -> 
            r.Reply ts
            return! postLoop ts
        | ReconnectCt r -> 
            match tryWebSocketConnection ts with
            | Some c -> 
                let ctx = createServiceCtx c 
                ctx |> AddCtx |> ctxmbx.Post
                printfn "Connected"
                ctx |> messageLoop dimbx |> Async.Start
                r.Reply  Ok
            | None -> 
                printfn "Connection failed"
                r.Reply Failed
            return! postLoop ts
        } 
    
    postLoop targethosts

// Creates the MailboxProcessor and passes it back. Used in Program.fs in order
// to pass to various consumers and/or complications.
let getCtbox (dimbx: DomainMailboxProcessor) (ctxmbx: CtxMailboxProcessor) = 
    MailboxProcessor.Start (serviceConnectionTrackerAgent dimbx ctxmbx)