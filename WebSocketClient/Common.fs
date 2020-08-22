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
    | NullMsg () -> "" 

let createIncomingMsgEvent () = 
    let incomingMsgEvt = new Event<CWebSocketMessage>()
    incomingMsgEvt, incomingMsgEvt.Publish


let closeWebSocket (ws: WebSocket) = 
    ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None) 
    |> Async.AwaitTask
    |> Async.Start

module ClientAsync =
    
    // Straight forward connection code, hardcoded for now
    let connectClientWebSocket () =    
        let cws = new ClientWebSocket()
        let uriAddress = "ws://localhost:5000/" |> Uri
        printfn "Connecting..."
        cws.ConnectAsync(uriAddress, CancellationToken.None) 
        |> Async.AwaitTask 
        |> Async.RunSynchronously // wait for it, not waiting caused weird Abort issues
        printfn "Connected"
        cws

    // Beginning of the receive pipeline. Sends along a dummy record if we 
    // hit the exception. I don't know if I need to do something with the 
    // buffer in that case or not. When a logger gets plugged in I won't just
    // drop the exception into the void anymore.
    let tryReceiveMsg (ws: ClientWebSocket) : ServerMessageIncoming =
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
    let procMessageEvent (incomingMsgEvt: Event<CWebSocketMessage>) msg =
        match msg with
        | TextMsg m -> incomingMsgEvt.Trigger(m |> TextMsg)
        | BinaryMsg m -> incomingMsgEvt.Trigger(m |> BinaryMsg)
        | NullMsg u -> incomingMsgEvt.Trigger(u |> NullMsg)
        
    
    // Assemble the pipe via a binding so I can hook the spinner to it
    let messagePipe (evt: Event<CWebSocketMessage>) (ws: ClientWebSocket) _ = 
        tryReceiveMsg ws 
        |> sortAndPackMsg 
        |> procMessageEvent evt


    // Sending Messages is simpler than receiving them! I'm not sure if 
    // overloading NullMsg for Closed is smart, but it's at least symmetric at
    // the time of writing.
    let sendWebSocketMsg outMsg (ws: ClientWebSocket) =
        match outMsg with
        | BinaryMsg m ->
            let arr = m |> ArraySegment<byte>
            ws.SendAsync(arr, WebSocketMessageType.Binary, true, CancellationToken.None) |> Async.AwaitTask |> ignore
         | TextMsg m -> 
            let arr = packStringBytes m
            ws.SendAsync(arr, WebSocketMessageType.Text, true, CancellationToken.None) |> Async.AwaitTask |> ignore
        | NullMsg _ -> closeWebSocket ws

    // Simpler than the server-side code for sure.
    let messageLoop (e: Event<CWebSocketMessage>) (ws: ClientWebSocket) = async {
        Seq.initInfinite (messagePipe e ws) 
        |> Seq.takeWhile (fun _ -> ws.State = WebSocketState.Open )
        |> Seq.iter (fun _ -> ())
        
        closeWebSocket ws
        }
