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
    

// This pair gets new incoming contexts into the MailboxProcessor from kestrel
let createServiceCtx host port (ws: WebSocket) : ServiceContext =
    let g = Guid.NewGuid()
    {ws = ws; host = host; port = port; guid = g}


let postServiceCtxMsg 
    (mbox: MailboxProcessor<ContextTrackerMessage>) 
    (ctx: ServiceContext) 
    = async {ctx |> AddCtx |> mbox.Post}

