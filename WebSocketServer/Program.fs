open System
open Microsoft.AspNetCore.Hosting

open Types
open Common
open MailboxOutgoingMessage
open MailboxContextTracker

// this stuff is mostly a testing mess, and doesn't reflect anything about the final application. It's all harness and no order.

[<EntryPoint>]
let main argv =
    if argv.Length <> 2 then 
        printfn "Wrong number of arguments"
        Environment.Exit(1)
    
    let uri = [|sprintf "http://%s:%s" argv.[0] argv.[1]|]
    let cmbx = returnCmbx ()
    let smbx = MailboxProcessor.Start outgoingWsMsgMailboxAgent

    
    // Standin for the primary Transceiver send interface
    let sendMsg () =
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
            smbx.Post {ctx = dctx; msg = (msg |> TextMsg)}
            //smbx.Post {ctx = dctx; msg = (multiply |> TextMsg)}

    
    // Server start ceremony. Yes, it looks weird, but better than 
    // .thing.thing.thing IMO
    application uri |> Async.Start

    // Basic control loop to interact with the server for testing
    let rec controlLoop () =
        match Console.ReadKey().Key with
        | ConsoleKey.Q -> 
            crlf ()
            cmbx.PostAndReply GetCtx
            |> List.iter(fun ctx -> 
                printfn "sending NullMsg to %i" <| ctx.ws.GetHashCode()
                smbx.Post {ctx = ctx; msg = (() |> NullMsg)})
            cmbx.PostAndReply KillAllCtx |> ignore
            Environment.Exit(0)
        | ConsoleKey.S -> 
            crlf ()
            sendMsg ()
            controlLoop ()
        | ConsoleKey.P ->
            crlf ()
            cmbx.PostAndReply GetCtx
            |> List.iter (fun ctx -> ctx.guid.ToString() |> printfn "%s")
            controlLoop ()
        | _            -> 
            crlf ()
            controlLoop ()

    controlLoop ()
    0 // return an integer exit code
