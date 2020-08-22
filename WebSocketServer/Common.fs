module Common

open System
open System.Net.WebSockets
open System.Text
open System.Threading
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Builder

//open Microsoft.AspNetCore.WebSockets

open Types

// convenience functions for dealing with WebSocket messages
let unpackStringBytes bytearr count = Encoding.UTF8.GetString (bytearr, 0, count)

let packStringBytes (s: string) = s |> Encoding.UTF8.GetBytes |> ArraySegment<byte>

let extractIncomingMsg (msg: CWebSocketMessage) = 
    match msg with 
    | TextMsg s -> s
    | BinaryMsg b -> BitConverter.ToString b
    | NullMsg () -> ""

let createNewEvts () = 
    let e = new Event<ConnectionContext>()
    e, e.Publish

let createEndEvts () = 
    let e = new Event<ConnectionContext>()
    e, e.Publish

let createIncomingMsgEvts () =
    let e = new Event<CWebSocketMessage>()
    e, e.Publish

// Barebones state tracking for ServerContexts. Eventually will allow actual
// sending of messages!
module WebSocketContextTracker = 

    open System.Collections.Concurrent

    let initCtxTracker () = new ConcurrentDictionary<Guid, ConnectionContext>()
    
    let insertCtx (cdict: ConcurrentDictionary<Guid, ConnectionContext>) (ctx: ConnectionContext) = 
        cdict.GetOrAdd (ctx.guid, ctx) |> ignore
        ctx.guid.ToString() |> printfn "Added context with designator %s to the tracker..."
     
     
    let pollCtxTracker (cdict: ConcurrentDictionary<Guid, ConnectionContext>) =
        printfn "Tracker contains the following GUIDs:"
        cdict.Keys
        |> Seq.iter (fun g -> g.ToString() |> printfn "%s")
        

    let removeCtx (cdict: ConcurrentDictionary<Guid, ConnectionContext>) (ctx: ConnectionContext) =
        let ret = ctx.guid |> fun g ->  cdict.TryRemove g
        printfn "removed: %b" (fst ret)


// Home of all of the listener and async message logic
module Server = 
    open WebSocketContextTracker

    // Beginning of the receive pipeline. Mints a record containing what the
    // next step needs to work. Sends along a dummy record if we hit the 
    // exception. I don't know if I need to do something with the buffer in
    // that case or not.
    let tryReceiveMsg (ws: WebSocket) : ServerMessageIncoming =
        let buf = Array.init 65536 byte |> ArraySegment<byte>
        try
            let res = 
                ws.ReceiveAsync(buf, CancellationToken.None)
                |> Async.AwaitTask
                |> Async.RunSynchronously
            {receivedMsg = res; buffer = buf}        
        with _ -> 
            ws.CloseAsync(WebSocketCloseStatus.Empty, "", CancellationToken.None) 
            |> Async.AwaitTask
            |> Async.Start
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
            ws.SendAsync(arr, WebSocketMessageType.Binary, true, CancellationToken.None) |> Async.AwaitTask |> ignore
         | TextMsg m -> 
            let arr = packStringBytes m
            ws.SendAsync(arr, WebSocketMessageType.Text, true, CancellationToken.None) |> Async.AwaitTask |> ignore
        | NullMsg _ -> 
            ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "CLOSED!", CancellationToken.None) 
            |> Async.AwaitTask 
            |> Async.Start
        
    
    // Extracts WebSocket, procs event for context creation, and starts
    // the message spinner pieline. Procs another event when the context
    // closes
    let messageLoop (e: EventBundle) (ws: WebSocket) = async {
        
        let sctx = {websocket = ws; guid = Guid.NewGuid()}
        e.newContextEvt.Trigger(sctx) |> ignore
        
        Seq.initInfinite (messagePipe e.incomingMsgEvt ws) 
        |> Seq.takeWhile (fun _ -> ws.State = WebSocketState.Open )
        |> Seq.iter (fun _ -> ())
        
        e.endContextEvt.Trigger(sctx)|> ignore
        }


    let newCtxEvt, newCtxEvtStream = createNewEvts ()
    let endCtxEvt, endCtxEvtStream = createEndEvts ()
    let incomingMsgEvt, incomingMsgEvtStream = createIncomingMsgEvts()
    let wsctxtracker = initCtxTracker ()
    
    newCtxEvtStream |> Observable.subscribe (insertCtx wsctxtracker) |> ignore
    endCtxEvtStream |> Observable.subscribe (removeCtx wsctxtracker)  |> ignore
    incomingMsgEvtStream |> Observable.subscribe (fun m -> m |> extractIncomingMsg |> printfn "%s" ) |> ignore
    
    let evtbundle = {newContextEvt = newCtxEvt; endContextEvt = endCtxEvt; incomingMsgEvt = incomingMsgEvt}
    
    
    type Startup() = 
        member this.Configure (app : IApplicationBuilder) = 
            let wso = new WebSocketOptions()
            wso.ReceiveBufferSize <- 65536
            app.UseWebSockets(wso) |> ignore
            app.Run (fun ctx -> 
                let ws = ctx.WebSockets.AcceptWebSocketAsync().Result
                messageLoop evtbundle ws |> Async.StartAsTask :> Task)



    