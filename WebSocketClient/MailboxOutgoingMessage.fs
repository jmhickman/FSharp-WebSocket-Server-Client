module MailboxOutgoingMessage
open System
open System.Net.WebSockets
open System.Threading

open Types
open WebSocketMsgHandlers

// This function is a convenience symbol for sending a WebSocket message.
// It is hardcoded to use the Binary message type because the application layer
// protocol is entirely binary in nature. Deliberately not completely 
let CSendAsync (ws: WebSocket) (arr: ArraySegment<byte>) = 
    ws.SendAsync (arr, WebSocketMessageType.Binary, true, CancellationToken.None) |> Async.AwaitTask |> ignore


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
        CSendAsync ws arr
    | NullMsg _ -> 
        closeWebSocket ws |> Async.Start
        ws.Dispose()


// This MailboxProcessor handles the outgoing message queue, using the 
// information contained in a ServerMessageOutgoing to select the correct
// ServiceContext to send the message to.
let outgoingWsMsgMailbox (mbox: MailboxProcessor<ServerMessageOutgoing>) = 
    let rec messageLoop () = async {
        let! smesgout =  mbox.Receive()
        sendWebSocketMsg smesgout.msg smesgout.ctx.ws
        do! messageLoop ()
        }
    messageLoop ()
