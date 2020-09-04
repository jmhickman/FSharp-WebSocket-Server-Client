module MailboxContextTracker
open System
open System.Net.WebSockets
open System.Threading

open Types
open Common

///
/// MailboxProcessor
///

let connectClientWebSocket ip port =    
    let connectString = sprintf "ws://%s:%s/" ip port |> Uri
             
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

let cycleConnectionAttempts ip port d = connectClientWebSocket ip port |> connectWithDelay d 


let tryWebSocketConnection (mbox: MailboxProcessor<ContextTrackerMessage>) ip port =    
    
    let rec conn delay attempt =    
        match cycleConnectionAttempts ip port delay with
        | [] -> if attempt < 4 then 
                    conn ((delay + 1) * 2) (attempt + 1)
                else Failed
        | x -> 
            createServiceCtx ip port (x.Head)
            |> postServiceCtxMsg mbox
            |> Async.Start
            Ok
    conn 0 1


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
        | ReconnectCtx (ip, port) -> 
            let res = tryWebSocketConnection mbx ip port
            if res = Failed then Environment.Exit(1)
            else return! postLoop sCTL 
        | GetCtx r ->
            r.Reply sCTL
            return! postLoop sCTL
        | KillAllCtx r -> 
            r.Reply (0 |> Some)
            return! postLoop []
        } 
        
    postLoop serviceContextList
