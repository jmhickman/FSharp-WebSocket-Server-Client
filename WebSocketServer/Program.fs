// Learn more about F# at http://fsharp.org
open System
open System.Threading
open Microsoft.AspNetCore.Hosting

open Types
open Common
open Common.Server
open Common.WebSocketContextTracker
open Common.ServerStartup

[<EntryPoint>]
let main argv =
    
    // A tiny message sender for testing. Dumps all the GUIDs first for
    // convenience. `msg` is usually preset to some huge message.
    let sendMsg () =
        if wsctxtracker.Count = 0 then printfn "No clients!"
        else 
            printfn "Select a client: "
            pollCtxTracker wsctxtracker
            let guid = Console.ReadLine() |> Guid
            let ctx = snd (wsctxtracker.TryGetValue guid)
            printf "Message $> "
            let msg = Console.ReadLine()
            sendWebSocketMsg (msg |> TextMsg) ctx.websocket

    
    
    // Startup/kickoff for the webserver
    // Yes, I know a series of lambdas looks silly. I think
    // the alternate form looks sillier, esp in fsharp
    let application = async { 
        WebHostBuilder()
        |> fun x -> x.UseKestrel()
        |> fun x -> x.UseUrls("http://localhost:5000")
        |> fun x -> x.UseStartup<Startup>()
        |> fun x -> x.Build()
        |> fun x -> x.Run()
        }

    let cts = new CancellationTokenSource()
    Async.Start (application, cts.Token)

    
    // Shoddy little control loop just to accomplish testing tasks
    let rec ruupu () =
        match Console.ReadKey().Key with
        | ConsoleKey.Q -> 
            crlf " "
            killAllCtx wsctxtracker |> ignore
            cts.Cancel()
            Environment.Exit(0)
        | ConsoleKey.S -> 
            crlf ""
            sendMsg ()
            ruupu ()
        | ConsoleKey.P ->
            crlf " "
            crlf ""
            pollCtxTracker wsctxtracker
            ruupu ()
        | _            -> 
            crlf " "
            ruupu ()
    ruupu ()
    0 // return an integer exit code
