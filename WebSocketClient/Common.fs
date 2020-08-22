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

module ClientAsync =
    
    // Straight forward connection code. Hopefully doesn't have an hidden gotchas like HttpListener did
    let connectClientWebSocket () =    
        let cws = new ClientWebSocket()
        let uriAddress = "ws://localhost:5000/" |> Uri
        cws.ConnectAsync(uriAddress, CancellationToken.None) |> Async.AwaitTask |> Async.Start
        printfn "Connecting..."
        cws


    let tryReceiveMsg (ws: ClientWebSocket) : ServerMessageIncoming =
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
    let messagePipe (evt: Event<CWebSocketMessage>) (ws: ClientWebSocket) _ = 
        tryReceiveMsg ws 
        |> sortAndPackMsg 
        |> procMessageEvent evt



    let sendWebSocketMsg outMsg (ws: ClientWebSocket) =
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


    let messageLoop (e: Event<CWebSocketMessage>) (ws: ClientWebSocket) = async {
        
        //let sctx = {websocket = ws; guid = Guid.NewGuid()}
        Seq.initInfinite (messagePipe e ws) 
        |> Seq.takeWhile (fun _ -> ws.State = WebSocketState.Open )
        |> Seq.iter (fun _ -> ())
        }
