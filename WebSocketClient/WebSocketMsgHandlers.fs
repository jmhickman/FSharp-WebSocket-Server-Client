module WebSocketMsgHandlers
open System
open System.Net.WebSockets
open System.Text
open System.Threading

open Types


let unpackStringBytes bytearr count = Encoding.UTF8.GetString (bytearr, 0, count)

let packStringBytes (s: string) = s |> Encoding.UTF8.GetBytes |> ArraySegment<byte>

let extractIncomingMsg (msg: CWebSocketMessage) = 
    match msg with 
    | TextMsg s   -> s
    | BinaryMsg b -> BitConverter.ToString b
    | NullMsg ()  -> ""


let closeWebSocket (ws: WebSocket) = async {
    do! ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None) 
        |> Async.AwaitTask
    }


let incomingWsMsgMailboxAgent (mbox: MailboxProcessor<CWebSocketMessage>) = 
    let rec messageLoop () = async {
        let! msg = mbox.Receive()
        extractIncomingMsg msg |> printfn "%s"
        do! messageLoop ()
        }
    messageLoop ()


// Beginning of the receive pipeline. Sends along a dummy record if we 
// hit the exception. I don't know if I need to do something with the 
// buffer in that case or not. When a logger gets plugged in I won't just
// drop the exception into the void anymore.
// While an exception causing a closure of the pipe might feel dramatic, 
// I want this to fail quickly and have the client reconnect rather than
// try to figure out why the receive failed.
let tryReceiveMsg (ws: WebSocket) : ServerMessageIncoming =
    let buf = Array.init 65536 byte |> ArraySegment<byte>
    try
        let res = 
            ws.ReceiveAsync(buf, CancellationToken.None)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        {receivedMsg = res; buffer = buf}        
    with _ -> 
        closeWebSocket ws |> Async.Start
        {receivedMsg = WebSocketReceiveResult(0, WebSocketMessageType.Close, true); buffer = buf}
    

// Simple matching based on the message type, and packing of the message
let sortAndPackMsg smsg : CWebSocketMessage =
    match smsg.receivedMsg.MessageType with
    | WebSocketMessageType.Close  -> () |> NullMsg
    | WebSocketMessageType.Text   -> 
        unpackStringBytes smsg.buffer.Array smsg.receivedMsg.Count 
        |> TextMsg
    | WebSocketMessageType.Binary -> 
        Array.truncate smsg.receivedMsg.Count smsg.buffer.Array 
        |> BinaryMsg
    | _ -> () |> NullMsg


// Message receive pipeline
let messagePipe (imbx: MailboxProcessor<CWebSocketMessage>) (ws: WebSocket) _ = 
    tryReceiveMsg ws 
    |> sortAndPackMsg 
    |> imbx.Post

// the MailboxProcessor has to be passed in here rather than calling the 
// convenient return method that some other code gets to use because this 
// file is lower in the stack. Spins receiving messages until the socket
// connection closes.
let messageLoop 
    (mbx: MailboxProcessor<ContextTrackerMessage>) 
    (sctx: ServiceContext) 
    = async {
    use imbx = MailboxProcessor.Start incomingWsMsgMailboxAgent
    
    Seq.initInfinite (messagePipe imbx sctx.ws) 
    |> Seq.find (fun _ -> sctx.ws.State <> WebSocketState.Open)
    
    match sctx.ws.State with
    | WebSocketState.CloseReceived ->
        printfn "close received"
        sctx.ws |> closeWebSocket |> Async.Start
        sctx |> RemoveCtx |> mbx.Post
    | WebSocketState.Aborted -> 
        mbx.Post (sctx |> RemoveCtx)
        mbx.Post ((sctx.host, sctx.port) |> ReconnectCtx)
    | _ -> printfn "Boom!"
    }
