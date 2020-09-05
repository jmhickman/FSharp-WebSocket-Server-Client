open System
open System.Net.WebSockets

open Types
open Common
open WebSocketMsgHandlers
open MailboxOutgoingMessage
open MailboxContextTracker
//open Client

[<EntryPoint>]
let main argv =
    if argv.Length <> 2 then 
        printfn "Wrong number of arguments"
        Environment.Exit(1)
    
    let cmbx = MailboxProcessor<ContextTrackerMessage>.Start (serviceContextTrackerAgent messageLoop)
    let hostandport = argv.[0], argv.[1]
    cmbx.Post ({host = argv.[0]; port = argv.[1]}|> AddFailoverCtx)
    
    match cmbx.PostAndReply ReconnectCtx with
    | Ok -> ()
    | Failed -> 
        printfn "Failed to connect to initial server(s)"
        Environment.Exit(1)
    
    let smbx = MailboxProcessor.Start outgoingWsMsgMailboxAgent
    
    let sendMsg () =
        let currCtxs = cmbx.PostAndReply GetCtx
        if currCtxs.Length = 0 then printfn "No connections!"
        else 
            printfn "Select a connection: "
            currCtxs |> List.iter (fun c -> printfn "%s" <| c.guid.ToString())
            let guid = Console.ReadLine() |> Guid
            let dctx = currCtxs |> List.filter( fun c -> c.guid = guid) |> List.head
            printf "Message $> "
            let msg = Console.ReadLine()
            smbx.Post {ctx = dctx; msg = (msg |> TextMsg)}


    // initial client connect attempt start goes here    

    // Basic control loop to interact with the server for testing
    let rec controlLoop () =
        match Console.ReadKey().Key with
        | ConsoleKey.Q -> 
            crlf ()
            cmbx.PostAndReply GetCtx
            |> List.iter(fun ctx -> 
                smbx.Post {ctx = ctx; msg = (() |> NullMsg)})
            cmbx.PostAndReply KillAllCtx |> ignore
            Environment.Exit(0)
        | ConsoleKey.A ->
            crlf ()
            printf "ip and port: "
            let raw = Console.ReadLine()
            let rr = raw.Split(' ')
            cmbx.Post ({host = rr.[0]; port = rr.[1]} |> AddFailoverCtx)
            controlLoop ()
        | ConsoleKey.L ->
            crlf ()
            cmbx.PostAndReply GetCt
            |> List.iter (printfn "%A")
            controlLoop ()
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
