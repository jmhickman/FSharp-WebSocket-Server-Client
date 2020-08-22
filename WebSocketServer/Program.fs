// Learn more about F# at http://fsharp.org
open System
open System.Threading
open Microsoft.AspNetCore.Hosting

open Types
open Common.WebSocketContextTracker
open Common.ServerStartup

[<EntryPoint>]
let main argv =
    
    // A tiny message sender for testing. Dumps all the GUIDs first for
    // convenience. `msg` is usually preset to some huge message.
    //let sendMsg () =
    //    let ws =  
    //        [|for i in wsctxtracker.ToArray() do yield i.Value|]
    //        |> Array.head
    //    //printf "$> "
    //    sendWebSocketMsg (msg |> TextMsg) ws.websocket

    let cts = new CancellationTokenSource()
    
    // Startup/kickoff for the webserver
    let application = async { 
        WebHostBuilder()
        |> fun x -> x.UseKestrel()
        |> fun x -> x.UseUrls("http://*:5000")
        |> fun x -> x.UseStartup<Startup>()
        |> fun x -> x.Build()
        |> fun x -> x.Run()
        }

    Async.Start (application, cts.Token)

    
    // Shoddy little control loop just to accomplish testing tasks
    let rec ruupu () =
        match Console.ReadKey().KeyChar with
        | 'Q' -> 
            killAllCtx wsctxtracker
            cts.Cancel()
            Environment.Exit(0)
        //| 'S' -> 
        //    sendMsg ()
        //    ruupu ()
        | 'P' -> 
            pollCtxTracker wsctxtracker
            ruupu ()
        | _ -> ruupu ()
    ruupu ()
    0 // return an integer exit code
