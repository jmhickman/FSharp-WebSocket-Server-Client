module Types
open System
open System.Net.WebSockets

type ServiceContext = {
    ws             : WebSocket
    guid           : Guid
    }

type ContextTrackerMessage = 
    | AddCtx of ServiceContext
    | RemoveCtx of ServiceContext
    | GetCtx of AsyncReplyChannel<ServiceContext list>
    | KillAllCtx of AsyncReplyChannel<int option>

type CWebSocketMessage = 
    | TextMsg   of string
    | BinaryMsg of byte array
    | NullMsg   of unit

type ServerMessageIncoming = {
    receivedMsg : WebSocketReceiveResult
    buffer      : ArraySegment<byte>
    }

type IncomingMessageLoop = MailboxProcessor<ContextTrackerMessage> -> ServiceContext -> Async<unit>

type ServerMessageOutgoing = {
    ctx : ServiceContext
    msg : CWebSocketMessage
    }
