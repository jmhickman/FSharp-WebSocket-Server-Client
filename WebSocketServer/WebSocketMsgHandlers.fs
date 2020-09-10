module WebSocketMsgHandlers
open System
open System.Net.WebSockets
open System.Text
open System.Threading

open Types

// This function is just a convenience symbol for the repetitive task of 
// extracting a string from an incoming byte array. Might be removed eventually
// when TextMsg is removed from the WebSocket comms layer.
let unpackStringBytes bytearr count = Encoding.UTF8.GetString (bytearr, 0, count)

// The reverse of the above, also may be removed.
let packStringBytes (s: string) = s |> Encoding.UTF8.GetBytes |> ArraySegment<byte>

// This function handles the incoming message types. Will eventually be sub-
// sumed into the higher Transceiver layer where deserialization will occur.
let extractIncomingMsg (msg: CWebSocketMessage) = 
    match msg with 
    | TextMsg s   -> s
    | BinaryMsg b -> BitConverter.ToString b
    | NullMsg ()  -> ""


// This function is a convenience symbol for closing WebSockets asynchronously.
let closeWebSocket (ws: WebSocket) = async {
    do! ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None) 
        |> Async.AwaitTask
    }


// This MailboxProcessor handles incoming WebSocket protocol messages. For
// now it just dumps things to the terminal. Eventually will expose messages
// to the Transceiver above it.
let incomingWsMsgMailboxAgent (mbox: MailboxProcessor<CWebSocketMessage>) = 
    let rec messageLoop () = async {
        let! msg = mbox.Receive()
        extractIncomingMsg msg |> printfn "%s"
        do! messageLoop ()
        }
    messageLoop ()


// This function is the IO bondary between the underlaying OS WebSocket impl
// and the application. At this stage, I'm not concerned about exceptions in
// the underlying task or anything, so there's no try/with here.
let receiveMsg (ws: WebSocket) : ServerMessageIncoming =
    let buf = Array.init 65536 byte |> ArraySegment<byte>
    let res = 
        ws.ReceiveAsync(buf, CancellationToken.None)
        |> Async.AwaitTask
        |> Async.RunSynchronously
    {receivedMsg = res; buffer = buf}
    

// This function handles the incoming messages based on type, creating a
// corresponding CWebSocketMessage with the original contents. The TextMsg
// path will eventually be culled. The fallthrough branch might be better 
// leveraged eventually.
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


// This function is the asynchronous core of the message receive logic. It 
// sets up a spinner that monitors for incoming WebSocket protocol messages and
// controls what occurs when the WebSocket is closed or collapses. Is called
// from and communicates with the MailboxProcessor ServiceContext tracker, 
// while also starting an incoming MailboxProcessor. Each of these is unique 
// to a ServiceContext, making each WebSocket protocol connection its own 
// thread. Less complex than the Client equivalent, since the server doesn't
// attempt to re-establish connections with clients.
let messageLoop 
    (mbx: MailboxProcessor<ContextTrackerMessage>) 
    (sctx: ServiceContext) 
    = async {
    use imbx = MailboxProcessor.Start incomingWsMsgMailboxAgent

    Seq.initInfinite (fun _ -> receiveMsg sctx.ws |> sortAndPackMsg |> imbx.Post) 
    |> Seq.find (fun _ -> sctx.ws.State <> WebSocketState.Open)
    printfn "closing socket..."
    sctx.ws |> closeWebSocket |> Async.Start
    sctx |> RemoveCtx |> mbx.Post
    }
