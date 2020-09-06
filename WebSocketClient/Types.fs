module Types
open System
open System.Net.WebSockets

// Describes a host and port combination for a WebSocket connection attempt.
type ConnectionTarget = {
    host : string
    port : string
    }

// Describes an established connection to a WebSocket Server or Client
type ServiceContext = {
    ws   : WebSocket
    guid : Guid
    }

// Describes a semaphore for the AsyncReplyChannel
type ConnectionAttemptResult =
    | Ok
    | Failed

// Describes possible messages from other parts of the application into the
// Context Tracker mailbox. 
type ContextTrackerMessage = 
    | AddCtx        of ServiceContext
    | AddFailoverCt of ConnectionTarget
    | RemoveCtx     of ServiceContext
    | ReconnectCtx  of AsyncReplyChannel<ConnectionAttemptResult>
    | GetCt         of AsyncReplyChannel<ConnectionTarget list>
    | GetCtx        of AsyncReplyChannel<ServiceContext list>
    | KillAllCtx

// Describes the two types of WebSocket protocol messages at the functional 
// side of the domain boundary, the contents, and a fallthrough type for other
// messages.
type CWebSocketMessage = 
    | TextMsg   of string
    | BinaryMsg of byte array
    | NullMsg   of unit

// Describes a .Net type and the raw buffer corresponding to the WebSocket
// protocol message from the domain boundary.
type ServerMessageIncoming = {
    receivedMsg : WebSocketReceiveResult
    buffer      : ArraySegment<byte>
    }

// Describes a function interface capable of handling incoming messages. Mostly
// for convenience.
type IncomingMessageLoop = MailboxProcessor<ContextTrackerMessage> -> ServiceContext -> Async<unit>

// Describes the functional side of the domain boundary for sending a message
// down into the .Net WebSocket handler.
type ServerMessageOutgoing = {
    ctx : ServiceContext
    msg : CWebSocketMessage
    }

