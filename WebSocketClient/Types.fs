module Types

open System
open System.Net.WebSockets


type CWebSocketMessage = 
    | TextMsg of string
    | BinaryMsg of byte array
    | NullMsg of unit


type ConnectionAttempt =
    | ConnectedSocket of ClientWebSocket
    | Dead of unit


type ConnectionAttemptResult = {
    cws : ClientWebSocket
    died : bool
    }

type ConnectionContext = {
    websocket : ClientWebSocket
    guid : Guid
    }


type ServerMessageIncoming = {
    receivedMsg : WebSocketReceiveResult
    buffer : ArraySegment<byte>
    }

type EventBundle = {
    newContextEvt : Event<ConnectionContext>
    endContextEvt : Event<ConnectionContext>
    incomingMsgEvt : Event<CWebSocketMessage>
    }