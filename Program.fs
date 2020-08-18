// Learn more about F# at http://fsharp.org
open System
open System.Net.WebSockets
open Common
open Common.ServerAsync
open Common.ServerEvents
open Common.HttpContextTracker
open Types

[<EntryPoint>]
let main argv =
    
    let httpctxlist = initCtxTracker ()

    let s = initServer ()
    printfn "%b" s.IsListening 
    
    // create the event streams and actual event objects
    let ctxevt, ctxevtStream = createServerCtxEvent ()
    let ectxevt, ectxevStream = endServerCtxEvent ()
    let imsgevt, imsgevtStream = createIncomingMsgEvent ()

    // subscribe our streams - 2 for new ServerContexts that arrive, one for 
    // new incoming websocket messages, and one for when ServerContexts end
    // and need to be removed from the tracker
    ctxevtStream |> Observable.subscribe (insertCtx httpctxlist) |> ignore
    ctxevtStream |> Observable.subscribe (beginTakingMessages imsgevt ectxevt) |> ignore
    imsgevtStream |> Observable.subscribe (fun x -> x.incomingMsg |> extractIncomingMsg |> printfn "%s") |> ignore
    ectxevStream |> Observable.subscribe (removeCtx httpctxlist)|> ignore

    // Just an example of sending a WebSocket message of type Text
    // Use the poll to print Guids and copy paste, or copy from the 
    // new context notification.
    let sendMessage msg =
        let tmsg = msg |> TextMsg
        printfn "Select a GUID to send a message to"
        let target = Console.ReadLine()
        printfn "Sending message to %s" target
        let key = Guid(target)
        let ctx = httpctxlist.TryGetValue key
        let outMsg = {outgoingMsgType = WebSocketMessageType.Text; outgoingMsg = tmsg}
        handleOutgoingMsg outMsg (snd ctx)

    // start primary async task
    runloop ctxevt s |> Async.Start
    
    // a derpy loop to interact with the contexts. Nothing manually cleans
    // up/sends Close the connected contexts
    let rec evalloop _ =
        printfn "Do a thing: "
        match Console.ReadKey().KeyChar.ToString().ToUpper() with
        | "P" -> pollCtxTracker httpctxlist
        | "S" -> sendMessage "message"
        | "Q" -> Environment.Exit(0)
        | _ -> ()
        evalloop ()
    evalloop ()
    
    0 // return an integer exit code
