// Learn more about F# at http://fsharp.org

open System
open System.Net.WebSockets
open System.Threading
open Types
open Common
open Common.ClientAsync

[<EntryPoint>]
let main argv =
    
    let cws = connectClientWebSocket ()
    let e, eStream = createIncomingMsgEvent ()
    messageLoop e cws |> Async.Start
    
    eStream 
    |> Observable.subscribe (fun m -> m |> extractIncomingMsg |> printfn "%s" ) 
    |> ignore

    (*let rec messageLoop () =
        printf "Message $> "
        let msg = Console.ReadLine()
        handleOutgoingMsg {outgoingMsgType = WebSocketMessageType.Text; outgoingMsg = msg |> TextMsg} cws
        //printfn "Sending..."
        //[0..9999]
        //|> List.iter (fun _ -> handleOutgoingMsg {outgoingMsgType = WebSocketMessageType.Text; outgoingMsg = msg |> TextMsg} cws)
        //printfn "finished"
        messageLoop ()
    messageLoop ()*)
    
    Console.ReadKey() |> ignore
    cws.Dispose()
    0 // return an integer exit code
