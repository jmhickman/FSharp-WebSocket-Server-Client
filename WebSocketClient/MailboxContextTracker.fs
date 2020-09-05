module MailboxContextTracker
open System
open System.Net.WebSockets
open System.Threading

open Types
open Common

///
/// MailboxProcessor
///

let connectClientWebSocket (c: ConnectionTarget) =    
    let connectString = sprintf "ws://%s:%s/" c.host c.port |> Uri
             
    let connectAttempt delay = async {
        let cws = new ClientWebSocket()
        do! Async.Sleep (delay * 2500)
        printfn "Connecting..."
        try
            do! cws.ConnectAsync(connectString, CancellationToken.None) |> Async.AwaitTask
            return cws |> ConnectedSocket
        with _ -> 
            return cws.Dispose() |> Dead
        }
    connectAttempt

let connectWithDelay d (aca: AsyncConnectionAttempt) =  
    [
     match (aca d |> Async.RunSynchronously) with
     | ConnectedSocket s -> yield s
     | Dead _ -> ()
    ]

let cycleConnectionAttempts (c: ConnectionTarget) d = connectClientWebSocket c |> connectWithDelay d 


let tryWebSocketConnection (mbox: MailboxProcessor<ContextTrackerMessage>) (c: ConnectionTarget) =    
    
    let rec conn delay attempt =    
        match cycleConnectionAttempts c delay with
        | [] -> 
            if attempt < 4 then 
                conn ((delay + 1) * 2) (attempt + 1)
            else Failed
        | x -> 
            createServiceCtx c.host c.port (x.Head)
            |> postServiceCtxMsg mbox
            |> Async.Start
            Ok
    conn 0 1

let matchWebSocketConnection mbx c =
    match tryWebSocketConnection mbx c with
    | Ok -> true
    | Failed -> false

let serviceContextTrackerAgent 
    (msgLoop: IncomingMessageLoop) 
    (mbx: MailboxProcessor<ContextTrackerMessage>) 
    =
    let serviceContextList = []
    let targethosts = []
    
    let rec postLoop (ts: ConnectionTarget list, sctxs: ServiceContext list)  = async {
        let! msg = mbx.Receive()
        
        match msg with
        | AddCtx ctx ->
            msgLoop mbx ctx |> Async.Start
            return! (ts, ctx::sctxs) |> postLoop
        | AddFailoverCtx ctx -> return! (ctx :: ts, sctxs) |> postLoop
        | RemoveCtx ctx ->
            let f = sctxs |> List.filter (fun sctx -> ctx.guid <> sctx.guid) 
            return! (ts, f) |> postLoop
        | ReconnectCtx r -> 
            match (ts |> List.tryFind (matchWebSocketConnection mbx)) with
            | Some c -> r.Reply Ok
            | None -> r.Reply Failed
            return! postLoop (ts, sctxs)
        | GetCt r -> 
            r.Reply ts
            return! postLoop (ts, sctxs)
        | GetCtx r ->
            r.Reply sctxs
            return! postLoop (ts, sctxs)
        | KillAllCtx r -> 
            r.Reply (0 |> Some)
            return! postLoop (ts, [])
        } 
        
    postLoop (targethosts, serviceContextList)
