// Learn more about F# at http://fsharp.org
open System
open Microsoft.AspNetCore.Hosting

open Types
open Common
open Common.WebSocketContextTracker
open Common.Server

[<EntryPoint>]
let main argv =
    
    let sendMsg () =
        let ws =  
            [|for i in wsctxtracker.ToArray() do yield i.Value|]
            |> Array.head
        printf "$> "
        let msg = Console.ReadLine() |> TextMsg
        sendWebSocketMsg msg ws.websocket

    let application = async { 
        WebHostBuilder()
        |> fun x -> x.UseKestrel()
        |> fun x -> x.UseUrls("http://*:5000")
        |> fun x -> x.UseStartup<Startup>()
        |> fun x -> x.Build()
        |> fun x -> x.Run()
        }

    application |> Async.Start

    let rec ruupu () =
        match Console.ReadKey().KeyChar with
        | 'Q' -> Environment.Exit(0)
        | 'S' -> 
            sendMsg ()
            ruupu ()
        | 'P' -> 
            pollCtxTracker wsctxtracker
            ruupu ()
        | _ -> ruupu ()
    ruupu ()
    
    
    
        
    


    0 // return an integer exit code
