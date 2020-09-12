open System
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting

open Types
open Common
open WebSocketMsgHandlers
open MailboxContextTracker


let smbx = MailboxProcessor.Start outgoingWsMsgMailboxAgent
let cmbx = MailboxProcessor.Start (serviceContextTracker messageLoop)

// Gross OP that's required to run the Asp.net Core Kestrel server.
type Startup() = 
    member this.Configure (app : IApplicationBuilder) = 
        app.UseWebSockets() |> ignore
        app.Run (fun ctx -> 
            let ws = ctx.WebSockets.AcceptWebSocketAsync().Result
            printfn "Connection incoming..."
            ws |> createServiceCtx |> cmbx.Post 
            
            infiniSpin ws
            |> Async.StartAsTask :> Task
            )

let application uri = async { 
    WebHostBuilder()
    |> fun x -> x.UseKestrel()
    //|> fun x -> x.UseKestrel(configureKestrel (argv.[0], argv.[1]))
    |> fun x -> x.UseUrls(uri)
    |> fun x -> x.UseStartup<Startup>()
    |> fun x -> x.Build()
    |> fun x -> 
        try x.Run() 
        with exn -> 
            printfn "Server Failed to start! %s" exn.Message
            Environment.Exit(1)
    }

let sendMsg () =
    let currCtxs = cmbx.PostAndReply GetCtx
    if currCtxs.Length = 0 then printfn "No clients!"
    else 
        printfn "Select a client: "
        currCtxs |> List.iter (fun c -> printfn "%s" <| c.guid.ToString())
        let guid = Console.ReadLine() |> Guid
        let dctx = currCtxs |> List.filter( fun c -> c.guid = guid) |> List.head
        printf "Message $> "
        let msg = Console.ReadLine()
        //let multiply = msg |> String.replicate 5000
        smbx.Post {ctx = dctx; msg = (msg |> TextMsg)}
        //smbx.Post {ctx = dctx; msg = (multiply |> TextMsg)}

[<EntryPoint>]
let main argv =
    if argv.Length <> 2 then 
        printfn "Wrong number of arguments"
        Environment.Exit(1)
    
    let uri = [|sprintf "http://%s:%s" argv.[0] argv.[1]|]
    
    application uri |> Async.Start

    // Basic control loop to interact with the server for testing
    let rec controlLoop () =
        match Console.ReadKey().Key with
        | ConsoleKey.Q -> 
            crlf ()
            cmbx.PostAndReply GetCtx
            |> List.iter(fun ctx -> 
                printfn "sending NullMsg to %i" <| ctx.ws.GetHashCode()
                smbx.Post {ctx = ctx; msg = (() |> NullMsg)})
            cmbx.PostAndReply KillAllCtx |> ignore
            Environment.Exit(0)
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
