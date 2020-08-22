// Learn more about F# at http://fsharp.org

open System
open System.Net.WebSockets
open Types
open Common
open Common.ClientAsync

[<EntryPoint>]
let main argv =
    
    // basic setup
    let connectAttempt = connectClientWebSocket argv.[1] argv.[2]
    match connectAttempt.died with
    | true -> 
        printfn "couldn't connect..."
        Environment.Exit(1)
    | false -> ()

    
    let cws = connectAttempt.cws

    let e, eStream = createIncomingMsgEvent ()
    
    // basic event stream subscription to print messages
    eStream 
    |> Observable.subscribe (fun m -> m |> extractIncomingMsg |> printfn "%s" ) 
    |> ignore

    
    //kick off the receive loop
    messageLoop e cws |> Async.Start

    
    // basic little haphazard test loop
    let rec commandLoop () =
        printf "Command: "
        match Console.ReadKey().Key with
        | ConsoleKey.S ->
            crlf " "
            printf "Message $> "
            let msg = Console.ReadLine()
            sendWebSocketMsg (msg |> TextMsg) cws
            commandLoop ()
        | ConsoleKey.I -> 
            crlf ""
            cws.State |> printfn "Socket state -> %A"
            commandLoop ()
        | ConsoleKey.Q -> 
            crlf ""
            cws.Dispose()
        | _            -> 
            crlf " "
            printfn ""
            commandLoop ()
    commandLoop ()
    0 // return an integer exit code
