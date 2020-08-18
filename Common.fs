module Common

open System
open System.Net
open System.Net.WebSockets
open System.Text
open System.Threading

open Types

// convenience functions for dealing with WebSocket messages
let unpackStringBytes bytearr count = Encoding.UTF8.GetString(bytearr, 0, count)

let packStringBytes (s: string) = s |> Encoding.UTF8.GetBytes |> ArraySegment<byte>

let extractIncomingMsg (msg: CWebSocketMessage) = 
    match msg with 
    | TextMsg s -> s
    | BinaryMsg b -> BitConverter.ToString b 


    
// Home of all of the listener and async message logic
module ServerAsync = 
    
    let initServer () : HttpListener = 
        let server = new HttpListener()
        server.Prefixes.Add("http://127.0.0.1:8090/")
        try server.Start() 
        with e -> 
            printfn "Failed to start server -> %s" e.Message
            Environment.Exit(1)
        server

    // Wait for an incoming HttpContext. When once occurs, create 
    // a ServerContext and fire an event so an async workflow for handling
    // messages starts. Drop the request if not a WebSocket request.
    let triggerNextContext (evt: Event<ServerContext>) (server: HttpListener) _ : unit = 
        let ctx = server.GetContext()
        
        match ctx.Request.IsWebSocketRequest with
        | true -> 
            let websocketCtx = ctx.AcceptWebSocketAsync(null) |> Async.AwaitTask |> Async.RunSynchronously
            evt.Trigger({httpCtx = ctx; websocketCtx = websocketCtx; guid = Guid.NewGuid() })
        | false -> 
            ctx.Response.StatusCode <- 400
            ctx.Response.Close()
            
    // Master router for handling various types of incoming WebSocket messages
    // Takes in event handlers for surfacing new messages to be processed 
    // upstream, or for tracking when a WebSocketContext has ended and the 
    // ServerContext needs to be retired from the tracker
    // Event handlers surface ServerMessageIncoming records
    let handleIncomingMsg 
        (incomingMsgEvt: Event<ServerMessageIncoming>) 
        (endServerCtxEvt: Event<ServerContext>)
        (serverCtx: ServerContext) 
        _  = 
        
        let websocket = serverCtx.websocketCtx.WebSocket
        let buf = Array.init 4096 byte |> ArraySegment<byte> // replace this logic with something that understands aggregated messages
            
        match websocket.State with    
        | WebSocketState.Open ->
            let recvmsg = 
                try
                    websocket.ReceiveAsync(buf, CancellationToken.None)
                    |> Async.AwaitTask
                    |> Async.RunSynchronously
                with _ -> new WebSocketReceiveResult(0, WebSocketMessageType.Close, true) // Make dummy message that will close the socket
            
            match recvmsg.MessageType with
            | WebSocketMessageType.Close ->
                websocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "CLOSED!", CancellationToken.None) 
                |> Async.AwaitTask 
                |> Async.Start
                endServerCtxEvt.Trigger(serverCtx)
            | WebSocketMessageType.Text -> 
                let msg = unpackStringBytes buf.Array recvmsg.Count
                incomingMsgEvt.Trigger({incomingMsgType = WebSocketMessageType.Text; incomingMsg = msg |> TextMsg})
            | WebSocketMessageType.Binary -> 
                let binmsg = Array.truncate recvmsg.Count buf.Array
                incomingMsgEvt.Trigger({incomingMsgType = WebSocketMessageType.Binary; incomingMsg = binmsg |> BinaryMsg})
            | _ -> 
                incomingMsgEvt.Trigger({incomingMsgType = WebSocketMessageType.Text; incomingMsg = "Dead path in message type match" |> TextMsg})
        
        | _ -> printfn "WebSocket in Closed state?... %A" websocket.State // someday I'll have a real logger
    
    
    // Like the incoming message counterpart. No particular need at this time
    // to generate events, so it's a little simpler.
    let handleOutgoingMsg outMsg sctx =
        let ws = sctx.websocketCtx.WebSocket
        
        match outMsg.outgoingMsgType with
        | WebSocketMessageType.Binary -> 
            match outMsg.outgoingMsg with
            | BinaryMsg b ->
                let arr = b |> ArraySegment<byte>
                ws.SendAsync(arr, WebSocketMessageType.Binary, true, CancellationToken.None) |> Async.AwaitTask |> ignore
            | _ -> () // Log this error later, Text message ended up in a Binary outgoingMsg
        | WebSocketMessageType.Text -> 
            match outMsg.outgoingMsg with
            | TextMsg t ->
                let arr = packStringBytes t
                ws.SendAsync(arr, WebSocketMessageType.Text, true, CancellationToken.None) |> Async.AwaitTask |> ignore
            |_ -> () // Log this error later, Binary message ended up in a Text outgoingMsg
        | WebSocketMessageType.Close -> 
            ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "CLOSED!", CancellationToken.None) 
            |> Async.AwaitTask 
            |> Async.Start
        | _ -> () // Shut up compiler. How can enums have out of bounds values and still be that enum anyway?

    // Spin infinitely until the WebSocket contained in the ServerContext 
    // expires/is closed. endServerCtxEvt fires when the connection is closed
    // so that the HttpContext/WebSocketContext can be removed from the Tracker
    let takeMessages incomingMsgEvt endServerCtxEvt (sctx: ServerContext) = async {
        Seq.initInfinite (handleIncomingMsg incomingMsgEvt endServerCtxEvt sctx) 
        |> Seq.takeWhile (fun _ -> sctx.websocketCtx.WebSocket.State = WebSocketState.Open)
        |> Seq.iter (fun _ -> ())
        }


  // this workflow consumes a subscription to the ctxEventStream, firing the 
  // takeMessages spinner once a ServerContext arrives.
    let beginTakingMessages incomingMsgEvt endServerCtxEvt (sctx: ServerContext) = 
        sctx 
        |> takeMessages incomingMsgEvt endServerCtxEvt
        |> Async.Start

 
    // Primary spinner: Queues up a new GetContext() sync process, handles the
    // connection, then resets the listener.
    let runloop newCtxEvt server = async {
        Seq.initInfinite (triggerNextContext newCtxEvt server) 
        |> Seq.iter (fun _ -> ())
        }

 


// Server event generators. We have the Event to pass into functions, and the 
// actual Observable stream to surface the Events to subscribers
module ServerEvents =
    
    let createServerCtxEvent () = 
        let serverCtxEvt = new Event<ServerContext>()
        serverCtxEvt, serverCtxEvt.Publish


    let endServerCtxEvent () = 
        let serverCtxEvt = new Event<ServerContext>()
        serverCtxEvt, serverCtxEvt.Publish
    

    let createIncomingMsgEvent () = 
        let incomingMsgEvt = new Event<ServerMessageIncoming>()
        incomingMsgEvt, incomingMsgEvt.Publish


// Barebones state tracking for ServerContexts. Eventually will allow actual
// sending of messages!
module HttpContextTracker = 

    open System.Collections.Concurrent

    let initCtxTracker () = new ConcurrentDictionary<Guid, ServerContext>()
    
    let insertCtx (cdict: ConcurrentDictionary<Guid, ServerContext>) (ctx: ServerContext) = 
        cdict.GetOrAdd (ctx.guid, ctx) |> ignore
        ctx.guid.ToString() |> printfn "Added context with designator %s to the tracker..."
     
     
    let pollCtxTracker (cdict: ConcurrentDictionary<Guid, ServerContext>) =
        printfn "Tracker contains the following GUIDs:"
        cdict.Keys
        |> Seq.iter (fun g -> g.ToString() |> printfn "%s")
        

    let removeCtx (cdict: ConcurrentDictionary<Guid, ServerContext>) (ctx: ServerContext) =
        let ret = ctx.guid |> fun g ->  cdict.TryRemove g
        printfn "removed: %b" (fst ret)
