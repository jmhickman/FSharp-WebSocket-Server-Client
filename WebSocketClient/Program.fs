// Learn more about F# at http://fsharp.org

open System
open System.Net.WebSockets
open Types
open Common
open Common.ClientAsync

[<EntryPoint>]
let main argv =
    
    // basic setup
    let cws = connectClientWebSocket ()
    let e, eStream = createIncomingMsgEvent ()
    
    // basic event stream subscription to print messages
    eStream 
    |> Observable.subscribe (fun m -> m |> extractIncomingMsg |> printfn "%s" ) 
    |> ignore


    // basic little test loop
    let rec messageLoop () =
        printf "Command: "
        match Console.ReadKey().KeyChar with
        | 'S' ->
            printfn ""
            printf "Message $> "
            let msg = Console.ReadLine()
            sendWebSocketMsg (msg |> TextMsg) cws
            messageLoop ()
        | 'E' -> cws.State |> printfn "%A"; messageLoop ()
        | 'Q' -> cws.Dispose()
        | _ -> messageLoop ()
    messageLoop ()
    
    
    0 // return an integer exit code
