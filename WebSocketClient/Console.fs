module Console
open System

open Types
open Common

// Basic control loop to illustrate consuming the domain mailbox resources.

let rec controlLoop 
    (cmbx: CtxMailboxProcessor) 
    (ctmbx: CtMailboxProcessor) 
    (dombx: DomainMailboxProcessor) 
    = async {

    printf "$>"
    match Console.ReadKey().Key with
    | ConsoleKey.Q -> 
        crlf ()
        cmbx.PostAndReply GetCtx
        |> List.iter(fun ctx -> 
            dombx.Post {ctx = ctx; msgType = CloseMsg})
        cmbx.Post KillAllCtx
        do! Async.Sleep 500
        Environment.Exit(0)
    | ConsoleKey.A ->
        crlf ()
        printf "ip and port: "
        let raw = Console.ReadLine()
        let rr = raw.Split(' ')
        ctmbx.Post ({host = rr.[0]; port = rr.[1]} |> AddFailoverCt)
        do! controlLoop cmbx ctmbx dombx
    | ConsoleKey.L ->
        crlf ()
        ctmbx.PostAndReply GetCt
        |> List.iter (printfn "%A")
        do! controlLoop cmbx ctmbx dombx 
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
            let multiply = msg |> String.replicate 500000 |> String.replicate 100
            //dombx.Post {ctx = dctx; msgType = (msg |> Console)}
            dombx.Post {ctx = dctx; msgType = (multiply |> Console)}
        do! controlLoop cmbx ctmbx dombx
    | ConsoleKey.R ->
        printfn "attempting to connect by prompt"
        ctmbx.PostAndReply ReconnectCt |> ignore
        do! controlLoop cmbx ctmbx dombx
    | ConsoleKey.P ->
        crlf ()
        cmbx.PostAndReply GetCtx
        |> List.iter (fun ctx -> ctx.guid.ToString() |> printfn "%s")
        do! controlLoop cmbx ctmbx dombx
    | _            -> 
        crlf ()
        do! controlLoop cmbx ctmbx dombx
    }