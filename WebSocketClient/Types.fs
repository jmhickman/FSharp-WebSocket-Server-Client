module Types
open System
open System.Net.WebSockets

type ConnectionTarget = {
    host : string
    port : string
    }

type ServiceContext = {
    ws   : WebSocket
    host : string
    port : string
    guid : Guid
    }

type ConnectionAttemptResult =
    | Ok
    | Failed

type ContextTrackerMessage = 
    | AddCtx       of ServiceContext
    | AddFailoverCtx of ConnectionTarget
    | RemoveCtx    of ServiceContext
    | ReconnectCtx of AsyncReplyChannel<ConnectionAttemptResult>
    | GetCt        of AsyncReplyChannel<ConnectionTarget list>
    | GetCtx       of AsyncReplyChannel<ServiceContext list>
    | KillAllCtx   of AsyncReplyChannel<int option>

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

type ConnectionAttempt =
    | ConnectedSocket of ClientWebSocket
    | Dead of unit

type AsyncConnectionAttempt = int -> Async<ConnectionAttempt>
    
