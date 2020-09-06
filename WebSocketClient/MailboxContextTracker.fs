module MailboxContextTracker
open System
open System.Net.WebSockets
open System.Threading

open Types
open Common


let connectClientWebSocket (c: ConnectionTarget, delay: int) =    
    let connectString = sprintf "ws://%s:%s/" c.host c.port |> Uri
             
    let connectAttempt delay = async {
        let cws = new ClientWebSocket()
        printfn "%i" delay
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


let generateTargetsAndDelays attempts delay (cts: ConnectionTarget list) =
    cts 
    |> List.map(fun c -> [for idx in [1..attempts] do yield (c, (idx * delay)) ])
    |> List.concat


let serviceContextTrackerAgent 
    (msgLoop: IncomingMessageLoop) 
    (mbx: MailboxProcessor<ContextTrackerMessage>) 
    =
    let serviceContextList = []
    let targethosts = []
    let pGenerate = generateTargetsAndDelays 4 500 
    
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
            let res = 
                ts 
                |> pGenerate
                |> List.tryPick(fun x -> (connectClientWebSocket x |> Async.RunSynchronously))
                
            match res with
            | Some c -> 
                createServiceCtx c |> (postServiceCtxMsg mbx) |> Async.Start
                r.Reply  Ok
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
