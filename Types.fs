module Types

open System
open System.Net
open System.Net.WebSockets


type CWebSocketMessage = 
    |TextMsg of string
    |BinaryMsg of byte array


type ServerContext = {
    httpCtx : HttpListenerContext 
    websocketCtx : HttpListenerWebSocketContext
    guid : Guid
    }

type ServerMessageIncoming = {
    incomingMsgType : WebSocketMessageType
    incomingMsg : CWebSocketMessage
   }

type ServerMessageOutgoing = {
    outgoingMsgType : WebSocketMessageType
    outgoingMsg : CWebSocketMessage
    }

type WebsocketRequest = 
    |NotWebSocketMessage
    |ServerCtx of ServerContext
