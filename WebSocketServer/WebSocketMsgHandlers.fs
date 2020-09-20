module WebSocketMsgHandlers
open System
open System.Net.WebSockets
open System.Threading

open Types
open Common


// This function is a convenience symbol for sending a WebSocket message.
// It is hardcoded to use the Binary message type because the application layer
// protocol is entirely binary in nature. Deliberately not completely async.
let CSendAsync (ws: WebSocket) (arr: ArraySegment<byte>) = 
    ws.SendAsync (arr, WebSocketMessageType.Binary, true, CancellationToken.None) 
    |> Async.AwaitTask 
    |> Async.RunSynchronously

//
// WebSocket send and receive functions
//


// This function has cases for handling the various outgoing message types.
// For all practical purposes though, TextMsg is a dead branch and may
// eventually be removed. The application will never intentially send a plain
// text message in normal operation.
let sendWebSocketMsg outMsg (ws: WebSocket) =
    match outMsg with
    | BinaryMsg m ->
        let arr = m |> ArraySegment<byte>
        CSendAsync ws arr
    | TextMsg m  -> 
        let arr = packStringBytes m
        CSendAsync ws (arr|> ArraySegment<byte>)
    | NullMsg _ -> 
        closeWebSocket ws |> Async.Start 


// This function is the IO bondary between the underlaying OS WebSocket impl
// and the application. Server has to have exception catching here, because 
// there's some difference between how Kestrel does the Sockets and how Client-
// WebSocket does it. 
let receiveMsg (sctx: ServiceContext) : ServerMessageIncoming =
    let buf = Array.init 65536 byte |> ArraySegment<byte>
    try
        let res = 
            sctx.ws.ReceiveAsync(buf, CancellationToken.None)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        {ctx =  sctx; receivedMsg = res; buffer = buf}        
    with exn -> 
        (sctx.ws.GetHashCode(), exn.Message) ||> printfn "Connection %i closed unexpectedly: %s"
        {ctx = sctx; receivedMsg = WebSocketReceiveResult(0, WebSocketMessageType.Close, true); buffer = buf}
    

// Funtion to take raw websocket message and deserialize and pack it into a 
// DomainMsg. Text messages are now a dead codepath and will be ignored.
let deserializeToDomainMsg smsg =
    match smsg.receivedMsg.MessageType with
    | WebSocketMessageType.Close  -> {ctx = smsg.ctx; msgType = CloseMsg} 
    | WebSocketMessageType.Text   -> {ctx = smsg.ctx; msgType = DeadMsg} 
    | WebSocketMessageType.Binary -> 
        let str = Array.truncate smsg.receivedMsg.Count smsg.buffer.Array |> BitConverter.ToString |> Console
        {ctx = smsg.ctx; msgType = str }
    | _ -> {ctx = smsg.ctx; msgType = DeadMsg} //|> NullMsg


// This MailboxProcessor handles incoming WebSocket protocol messages. 
// Eventually will handle other functions.
let incomingWsMsgMailbox 
    (dimbx: DomainMailboxProcessor)
    (mbox: MailboxProcessor<DomainMsg>) = 
    
    let rec messageLoop () = async {
        let! msg = mbox.Receive()
        msg |> dimbx.Post
        do! messageLoop ()
        }
    messageLoop ()

// Creates the MailboxProcessor and passes it back. Used in Program.fs in order
// to pass to various consumers and/or complications.
let getInbox dimbx = 
    MailboxProcessor.Start (incomingWsMsgMailbox dimbx )
    
    
//
// WebSocket incoming loop and outgoing mailbox
//

// This function is the asynchronous core of the message receive logic. It 
// sets up a spinner that monitors for incoming WebSocket protocol messages and
// controls what occurs when the WebSocket is closed or collapses. Is called
// from and communicates with the MailboxProcessor ServiceContext tracker, 
// while also starting an incoming MailboxProcessor. Each of these is unique 
// to a ServiceContext, making each WebSocket protocol connection its own 
// thread. Less complex than the Client equivalent, since the server doesn't
// attempt to re-establish connections with clients.
let messageLoop 
    (dimbx: DomainMailboxProcessor)
    (sctx: ServiceContext) 
    = async {
    use imbx = getInbox dimbx
    Seq.initInfinite (fun _ -> receiveMsg sctx |> deserializeToDomainMsg |> imbx.Post) 
    |> Seq.find (fun _ -> sctx.ws.State <> WebSocketState.Open)
    closeWebSocket sctx.ws |> Async.Start
    }


// This MailboxProcessor handles the outgoing message queue, using the 
// information contained in a ServerMessageOutgoing to select the correct
// ServiceContext to send the message to.
let outgoingWsMsgMailbox (mbox: OutgoingMailboxProcessor) = 
    let rec messageLoop () = async {
        let! smesgout =  mbox.Receive()
        newline ()
        sendWebSocketMsg smesgout.msg smesgout.ctx.ws
        do! messageLoop ()
        }
    messageLoop ()

// Creates the MailboxProcessor and passes it back. Used in Program.fs in order
// to pass to various consumers and/or complications.
let getOutbox () = 
    MailboxProcessor.Start outgoingWsMsgMailbox
    