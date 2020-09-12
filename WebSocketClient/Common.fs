module Common
open System
open System.Net.WebSockets

open Types

// Small function to clean up ReadKey characters on the console
let crlf () = 
    Console.SetCursorPosition(0, Console.CursorTop)
    Console.Write(" ")
    Console.SetCursorPosition(0, Console.CursorTop)
    Console.Write("")
    

// This function is a convenience symbol for creating a ServiceContext to send
// to a MailboxProcessor Context Tracker.
let createServiceCtx (ws: WebSocket) : ServiceContext =
    let g = Guid.NewGuid()
    {ws = ws; guid = g}


// This function is a concenience symbol for packing and sending the AddCtx
// message to a MailboxProcessor Context Tracker.
let postServiceCtxMsg (mbox: CtxMailboxProcessor) (ctx: ServiceContext) = async {
    ctx |> AddCtx |> mbox.Post
    }

