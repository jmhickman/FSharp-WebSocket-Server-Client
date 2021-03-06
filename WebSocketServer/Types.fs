﻿namespace Types
open System
open System.Net.WebSockets

//
// Protocol/Transport Types
//

// Describes an established connection to a WebSocket Server or Client

type ServiceContext = {
    ws   : WebSocket
    guid : Guid
    }

// Describes possible messages from other parts of the application into the
// Context Tracker mailbox. 

type ContextTrackerMessage = 
    | AddCtx     of ServiceContext
    | RemoveCtx  of ServiceContext
    | GetCtx     of AsyncReplyChannel<ServiceContext list>
    | KillAllCtx of AsyncReplyChannel<int option>

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
    ctx         : ServiceContext
    receivedMsg : WebSocketReceiveResult
    buffer      : ArraySegment<byte>
    }

// Describes the functional side of the domain boundary for sending a message
// down into the .Net WebSocket handler.

type ServerMessageOutgoing = {
    ctx : ServiceContext
    msg : CWebSocketMessage
    }

// Describes all Domain messages, so any and all function can be mapped to a 
// type. Various consumers can be addressed in the domain inbox based on the 
// message type.

type ActionMsgPayload = 
    | AllMsg
    | CloseMsg
    | Console of string
    | DeadMsg

// Describes a basic concept of a domain record.

type ActionMsg = {
    ctx     : ServiceContext
    msgType : ActionMsgPayload
    }

// Set of types to make for less ugly typing later in other modules.

type CtxMailboxProcessor = MailboxProcessor<ContextTrackerMessage>

type ProtocolOutbox = MailboxProcessor<ServerMessageOutgoing>

type ActionMsgAgent = MailboxProcessor<ActionMsg>

// Describes a function interface capable of handling incoming messages. Helps
// with dependency loops.

type IncomingMessageLoop = MailboxProcessor<ContextTrackerMessage> -> ServiceContext -> Async<unit>

// Internal feature list

type Complication = {
    name        : string
    messageType : ActionMsg list
    }



