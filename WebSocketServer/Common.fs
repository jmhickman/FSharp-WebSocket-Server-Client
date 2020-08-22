module Common

open System
open System.Net.WebSockets
open System.Text
open System.Threading
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder


open Types

//
// Common functions for WebSocket server process
//

// Convenience functions for dealing with WebSocket messages
let unpackStringBytes bytearr count = Encoding.UTF8.GetString (bytearr, 0, count)

let packStringBytes (s: string) = s |> Encoding.UTF8.GetBytes |> ArraySegment<byte>

let extractIncomingMsg (msg: CWebSocketMessage) = 
    match msg with 
    | TextMsg s -> s
    | BinaryMsg b -> BitConverter.ToString b
    | NullMsg () -> ""


let closeWebSocket (ws: WebSocket) = 
    ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None) 
    |> Async.AwaitTask
    |> Async.Start


// Create events and event streams for new Http/WebSocket connections, ending
// Http/WebSocket connections, and when a message has been processed and 
// needs to travel up to a higher level of the program.
let createNewEvts () = 
    let e = new Event<ConnectionContext>()
    e, e.Publish

let createEndEvts () = 
    let e = new Event<ConnectionContext>()
    e, e.Publish

let createIncomingMsgEvts () =
    let e = new Event<CWebSocketMessage>()
    e, e.Publish


// Barebones state tracking for ConnectionContexts. Implemented with a thread
// safe collection.
module WebSocketContextTracker = 

    open System.Collections.Concurrent

    let initCtxTracker () = new ConcurrentDictionary<Guid, ConnectionContext>()
    
    // GetOrAdd returns the added obj, or the existing one if there's a bounce
    // and I don't want the thing in either case, hence the ignore
    let insertCtx (cdict: ConcurrentDictionary<Guid, ConnectionContext>) (ctx: ConnectionContext) = 
        (ctx.guid, ctx) 
        |> cdict.GetOrAdd 
        |> ignore
    
    
    // Dumps the current set of connection client GUIDs to the console
    let pollCtxTracker (cdict: ConcurrentDictionary<Guid, ConnectionContext>) =
        printfn "Tracker contains the following GUIDs:"
        cdict.Keys |> Seq.iter (fun g -> g.ToString() |> printfn "%s")
        
    
    // Evict a context when the message logic detects it has closed the connection
    let removeCtx (cdict: ConcurrentDictionary<Guid, ConnectionContext>) (ctx: ConnectionContext) =
        ctx.guid 
        |> fun g -> cdict.TryRemove g
        |> ignore

    
    // The server should close the connections if it knows it's closing
    let killAllCtx (cdict: ConcurrentDictionary<Guid, ConnectionContext>) =
        let arr = cdict.ToArray()
        [for i in (cdict.ToArray()) do yield i]
        |> List.iter(fun x -> 
            let ws = x.Value.websocket
            ws.CloseAsync(WebSocketCloseStatus.EndpointUnavailable, "", CancellationToken.None)
            |> Async.AwaitTask
            |> fun x -> Async.RunSynchronously(x, 500)) //kinda gross


// Home of all of the asp.net core server and message logic
module Server = 
    
    // Beginning of the receive pipeline. Sends along a dummy record if we 
    // hit the exception. I don't know if I need to do something with the 
    // buffer in that case or not. When a logger gets plugged in I won't just
    // drop the exception into the void anymore.
    let tryReceiveMsg (ws: WebSocket) : ServerMessageIncoming =
        let buf = Array.init 65536 byte |> ArraySegment<byte>
        try
            let res = 
                ws.ReceiveAsync(buf, CancellationToken.None)
                |> Async.AwaitTask
                |> Async.RunSynchronously
            {receivedMsg = res; buffer = buf}        
        with _ -> 
            closeWebSocket ws
            {receivedMsg = WebSocketReceiveResult(0, WebSocketMessageType.Close, true); buffer = buf}
        
    
    // Simple matching based on the message type, and packing of the message
    let sortAndPackMsg smsg : CWebSocketMessage =
        match smsg.receivedMsg.MessageType with
        | WebSocketMessageType.Close -> () |> NullMsg
        | WebSocketMessageType.Text -> unpackStringBytes smsg.buffer.Array smsg.receivedMsg.Count |> TextMsg
        | WebSocketMessageType.Binary -> Array.truncate smsg.receivedMsg.Count smsg.buffer.Array |> BinaryMsg
        | _ -> () |> NullMsg

    
    // Proc the event that will eventually get hooked into something useful
    let procMessageEvent (incomingMsgEvt: Event<CWebSocketMessage> ) msg =
        match msg with
        | TextMsg m -> incomingMsgEvt.Trigger(m |> TextMsg)
        | BinaryMsg m -> incomingMsgEvt.Trigger(m |> BinaryMsg)
        | NullMsg u -> incomingMsgEvt.Trigger(u |> NullMsg)
        
    
    // Assemble the pipe via a binding so I can hook the spinner to it
    let messagePipe (evt: Event<CWebSocketMessage>) (ws: WebSocket) _ = 
        tryReceiveMsg ws 
        |> sortAndPackMsg 
        |> procMessageEvent evt
    
    
    // Sending Messages is simpler than receiving them! I'm not sure if 
    // overloading NullMsg for Closed is smart, but it's at least symmetric at
    // the time of writing.
    let sendWebSocketMsg outMsg (ws: WebSocket) =
        match outMsg with
        | BinaryMsg m ->
            let arr = m |> ArraySegment<byte>
            ws.SendAsync (arr, WebSocketMessageType.Binary, true, CancellationToken.None) |> Async.AwaitTask |> ignore
         | TextMsg m -> 
            let arr = packStringBytes m
            ws.SendAsync (arr, WebSocketMessageType.Text, true, CancellationToken.None) |> Async.AwaitTask |> ignore
        | NullMsg _ -> closeWebSocket ws
        
    
    // Extracts WebSocket and starts the message spinner pieline.
    // Procs another event when the context closes. Proc at the beginning and 
    // end for inclusion and eviction from the context tracker.
    // There is a spinner running for each active context so long as the 
    // context is alive.
    let messageLoop (e: EventBundle) (ws: WebSocket) = async {
        let cctx = {websocket = ws; guid = Guid.NewGuid ()}
        e.newContextEvt.Trigger cctx |> ignore
        
        // Primary messaging loop spinner
        // Weird-looking empty `iter` to 'pull' new websockets through the
        // pipeline
        Seq.initInfinite (messagePipe e.incomingMsgEvt ws) 
        |> Seq.takeWhile (fun _ -> ws.State = WebSocketState.Open )
        |> Seq.iter (fun _ -> ())
        
        closeWebSocket ws 
        e.endContextEvt.Trigger cctx |> ignore
        }


// Container for the worst of the object programming.
// Event handlers are initialized here as well as the context tracker
module ServerStartup =
    
    open WebSocketContextTracker
    open Server

    let newCtxEvt, newCtxEvtStream = createNewEvts ()
    let endCtxEvt, endCtxEvtStream = createEndEvts ()
    let incomingMsgEvt, incomingMsgEvtStream = createIncomingMsgEvts()
    let wsctxtracker = initCtxTracker ()
    
    newCtxEvtStream |> Observable.subscribe (insertCtx wsctxtracker) |> ignore
    endCtxEvtStream |> Observable.subscribe (removeCtx wsctxtracker)  |> ignore
    incomingMsgEvtStream |> Observable.subscribe (fun m -> m |> extractIncomingMsg |> printfn "-%s" ) |> ignore
    
    let evtbundle = {
        newContextEvt = newCtxEvt
        endContextEvt = endCtxEvt
        incomingMsgEvt = incomingMsgEvt
        }
    
    // Ugh
    type Startup() = 
        member this.Configure (app : IApplicationBuilder) = 
            let wso = new WebSocketOptions()
            wso.ReceiveBufferSize <- 65536
            app.UseWebSockets(wso) |> ignore
            app.Run (fun ctx -> 
                let ws = ctx.WebSockets.AcceptWebSocketAsync().Result
                messageLoop evtbundle ws |> Async.StartAsTask :> Task)



    