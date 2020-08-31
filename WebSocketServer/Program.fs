open System
open Microsoft.AspNetCore.Hosting

open Types
open Common
open MailboxOutgoingMessage
open Server

[<EntryPoint>]
let main argv =
    if argv.Length <> 2 then 
        printfn "Wrong number of arguments"
        Environment.Exit(1)
    
    let uri = sprintf "http://%s:%s" argv.[0] argv.[1]
    let imbx = returnMbox ()
    let smbx = MailboxProcessor.Start outgoingWsMsgMailboxAgent


    let sendMsg () =
        let currCtxs = imbx.PostAndReply GetCtx
        if currCtxs.Length = 0 then printfn "No clients!"
        else 
            printfn "Select a client: "
            currCtxs |> List.iter (fun c -> printfn "%s" <| c.guid.ToString())
            let guid = Console.ReadLine() |> Guid
            let dctx = currCtxs |> List.filter( fun c -> c.guid = guid) |> List.head
            printf "Message $> "
            let msg = Console.ReadLine()
            smbx.Post {ctx = dctx; msg = (msg |> TextMsg)}

    
    // Server start ceremony. Yes, it looks weird, but better than 
    // .thing.thing.thing IMO
    let application = async { 
        WebHostBuilder()
        |> fun x -> x.UseKestrel()
        |> fun x -> x.UseUrls(uri)
        |> fun x -> x.UseStartup<Startup>()
        |> fun x -> x.Build()
        |> fun x -> x.Run()
        }


    application |> Async.Start

    // Basic control loop to interact with the server for testing
    let rec controlLoop () =
        match Console.ReadKey().Key with
        | ConsoleKey.Q -> 
            crlf " "
            crlf ""
            imbx.PostAndReply GetCtx
            |> List.iter(fun ctx -> 
                printfn "sending NullMsg to %i" <| ctx.ws.GetHashCode()
                smbx.Post {ctx = ctx; msg = (() |> NullMsg)})
            imbx.PostAndReply KillAllCtx |> ignore
            Environment.Exit(0)
        | ConsoleKey.S -> 
            crlf ""
            sendMsg ()
            controlLoop ()
        | ConsoleKey.P ->
            crlf " "
            crlf ""
            imbx.PostAndReply GetCtx
            |> List.iter (fun ctx -> ctx.guid.ToString() |> printfn "%s")
            controlLoop ()
        | _            -> 
            crlf " "
            controlLoop ()

    controlLoop ()
    0 // return an integer exit code
