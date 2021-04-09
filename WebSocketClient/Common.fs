module Common
open System
open System.Text
open System.Threading
open System.Threading.Tasks
open System.Net.WebSockets

open Types

// Small function to clean up ReadKey characters on the console

let crlf () = 
    Console.SetCursorPosition(2, Console.CursorTop)
    Console.Write(" ")
    Console.SetCursorPosition(2, Console.CursorTop)
    Console.Write("")

    
// small function to alias a common printfn.

let newline () = printfn ""

// This function is just a convenience symbol for the repetitive task of 
// extracting a string from an incoming byte array. Might be removed eventually
// when TextMsg is removed from the WebSocket comms layer.

let unpackStringBytes bytearr count = Encoding.UTF8.GetString (bytearr, 0, count)

// The reverse of the above, also may be removed.

let packStringBytes (s: string) = s |> Encoding.UTF8.GetBytes

// This function is a convenience symbol for creating a ServiceContext to send
// to a MailboxProcessor Context Tracker.

let createServiceCtx (ws: WebSocket) : ServiceContext =
    let g = Guid.NewGuid()
    {ws = ws; guid = g}


// This function is a convenience symbol for closing WebSockets asynchronously.
// Moved to Common because other pieces in the stack need to be able 
// to reference it.

let closeWebSocket (ws: WebSocket) = async {
    printfn "inside closeWebSocket"
    do! ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None) |> Async.AwaitTask
    }

// Unused, but if more sophisticated steps are needed for outgoing messages, 
// they could happen here.

let toDTO (ctx: ServiceContext) (dmsg: DomainMsg) = 
    // serialize dmsg into bytes
    // pack byte array as BinaryMsg
    // create ServerMessageOutgoing with ctx
    ()

// Unused, but if more sophisticated steps are needed for incoming messages,
// they could happen here.

let fromDTO (ctx: ServiceContext) (smsg: ServerMessageIncoming) = 
    // deserialize record from byte array
    // perform bounding checks
    // create domain msg record
    ()