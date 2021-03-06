﻿module Console
open System

open Types
open Common

//
// Basic control loop to illustrate consuming the domain mailbox resources.
//

// Move all of these little functions into their own definitions, and then call them in the match, rather than performing them in the match

let rec console 
    (cmbx: CtxMailboxProcessor) 
    (dombx: ActionMsgAgent) 
    = async {
    printf "$>"

    match Console.ReadKey().Key with
    | ConsoleKey.Q -> 
        //crlf ()
        newline ()
        cmbx.PostAndReply GetCtx
        |> List.iter(fun ctx -> 
            dombx.Post {ctx = ctx; msgType = CloseMsg})
        cmbx.PostAndReply KillAllCtx |> ignore //Why am I posting with a reply and then ignoring the result?
        do! Async.Sleep 500 // a hedge against terminating before all the clients are cleanly disconnected
        Environment.Exit(0)
    | ConsoleKey.S -> 
        newline ()
        let currCtxs = cmbx.PostAndReply GetCtx
        if currCtxs.Length = 0 then printfn "No clients!"
        else 
            printfn "Select a client: "
            currCtxs |> List.iter (fun c -> printfn "%s" <| c.guid.ToString())
            let guid = Console.ReadLine() |> Guid
            let dctx = currCtxs |> List.filter( fun c -> c.guid = guid) |> List.head
            printf "Message $> "
            let msg = Console.ReadLine()
            //let multiply = msg |> String.replicate 5000
            dombx.Post {ctx = dctx; msgType = (msg |> Console)}
            //dombx.Post {ctx = dctx; msgType = (multiply |> Console)}
        do! console cmbx dombx
    | ConsoleKey.P ->
        //crlf ()
        newline ()
        cmbx.PostAndReply GetCtx
        |> List.iter (fun ctx -> ctx.guid.ToString() |> printfn "%s")
        do! console cmbx dombx
    | _            -> 
        crlf ()
        do! console cmbx dombx
    }

