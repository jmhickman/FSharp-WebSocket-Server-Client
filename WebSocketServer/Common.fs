module Common
open System
open System.Net.WebSockets

open Types


// Small function to clean up ReadKey characters on the console
let crlf (s: string) = 
    Console.SetCursorPosition((Console.CursorLeft - 1), Console.CursorTop)
    Console.Write(s)

// This pair gets new incoming contexts into the MailboxProcessor from kestrel
let createServiceCtx (ws: WebSocket) : ServiceContext =
    let g = Guid.NewGuid()
    {ws = ws; guid = g}


let postServiceCtxMsg 
    (mbox: MailboxProcessor<ContextTrackerMessage>) 
    (ctx: ServiceContext) 
    = async {ctx |> AddCtx |> mbox.Post}

