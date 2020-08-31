module MailboxOutgoingMessage
open System
open System.Net.WebSockets
open System.Threading

open Types
open WebSocketMsgHandlers


let CSendAsync (ws: WebSocket) (arr: ArraySegment<byte>) = 
    ws.SendAsync (arr, WebSocketMessageType.Text, true, CancellationToken.None) |> Async.AwaitTask |> ignore


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


let outgoingWsMsgMailboxAgent (mbox: MailboxProcessor<ServerMessageOutgoing>) = 
    let rec messageLoop () = async {
        let! smesgout =  mbox.Receive()
        sendWebSocketMsg smesgout.msg smesgout.ctx.ws
        do! messageLoop ()
        }
    messageLoop ()
