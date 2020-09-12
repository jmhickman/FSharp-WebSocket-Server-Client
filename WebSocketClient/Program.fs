open System
open System.Threading

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
    
    let cmbx = MailboxProcessor<ContextTrackerMessage>.Start (serviceContextTrackerAgent)
    cmbx.Post ({host = argv.[0]; port = argv.[1]}|> AddFailoverCt)
    
    match cmbx.PostAndReply ReconnectCtx with
    | Ok _ -> ()
    | Failed -> 
        printfn "Failed to connect to initial server(s)"
        Environment.Exit(1)
    
    let smbx = MailboxProcessor.Start outgoingWsMsgMailbox // since there's only one send-side Mailbox, this will probably be created in the Transceiver.
    
    let makeMore (str: string) (acc: string) = acc + str
    // Standin for the primary Transceiver send interface
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
            //let multiply = msg |> String.replicate 5000
            smbx.Post {ctx = dctx; msg = (msg |> TextMsg)}
            //smbx.Post {ctx = dctx; msg = (multiply |> TextMsg)}


    // Basic control loop to interact with the server for testing
    let rec controlLoop () =
        match Console.ReadKey().Key with
        | ConsoleKey.Q -> 
            crlf ()
            cmbx.PostAndReply GetCtx
            |> List.iter(fun ctx -> 
                smbx.Post {ctx = ctx; msg = (() |> NullMsg)})
            cmbx.Post KillAllCtx
            // I have to give time for the sockets to close. Strangely, 
            // I never had this issue during primary development
            Thread.Sleep 500
            Environment.Exit(0)
        | ConsoleKey.A ->
            crlf ()
            printf "ip and port: "
            let raw = Console.ReadLine()
            let rr = raw.Split(' ')
            cmbx.Post ({host = rr.[0]; port = rr.[1]} |> AddFailoverCt)
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
        | ConsoleKey.R ->
            printfn "attempting to connect by prompt"
            cmbx.PostAndReply ReconnectCtx |> ignore
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
