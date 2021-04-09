open System
open System.Net.WebSockets
open System.Threading
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting

open Types
open Common
open Console
open IncomingMessageAgent
open OutgoingMessageAgent
open WebSocketMsgHandlers
open ProtocolContextTracker


// create agents

// protocolSendAgent handles application messages going out over the protocol
// contextTrackerAgent handles managing the state of current connected clients.
// outgoingAgent is the application-level outbox, for consumers to send their messages to. 
//   Handles the DTO transition from application message to protocol message

let protocolSendAgent = startProtocolAgent ()
let contextTracker = startProtocolContextTracker ()
let outgoingAgent = startOutgoingAgent protocolSendAgent

// Start the console

console contextTracker outgoingAgent |> Async.Start

// Complications are any functionality that needs to communicate
// over the transport to registered implants, operator consoles, or servers.

let complications = []

// Once the list of complications is known, it is passed to the Domain inbox
// so that it can determine which complication to send incoming messages to.

let incomingAgent = startIncomingAgent complications outgoingAgent

// Gross OP that's required to run the Asp.net Core Kestrel server. Due to
// how dumb this initialization process is, I can't cleanly move this down
// into the Types area, because it has to know about the CtxMailbox, but it
// seemingly has no way to take in arbitrary arguments/dependencies without
// becoming even gross-er. 

type Startup() = 
    member this.Configure (app : IApplicationBuilder) = 
        app.UseWebSockets() |> ignore
        app.Run (fun ctx -> 
            let sctx = 
                ctx.WebSockets.AcceptWebSocketAsync().Result
                |> createServiceCtx
            
            sctx |> AddCtx |> contextTracker.Post 
            sctx |> messageLoop incomingAgent |> Async.RunSynchronously
            sctx |> RemoveCtx |> contextTracker.Post
            closeWebSocket sctx.ws |> Async.StartAsTask :> Task
            )


// Webserver kickoff

let application uri = async { 
    WebHostBuilder()
    |> fun x -> x.UseKestrel()
    //|> fun x -> x.UseKestrel(configureKestrel (argv.[0], argv.[1])) // wss://
    |> fun x -> x.UseUrls(uri)
    |> fun x -> x.UseStartup<Startup>()
    |> fun x -> x.Build()
    |> fun x -> 
        try x.Run() 
        with exn -> 
            printfn "Server Failed to start! %s" exn.Message
            Environment.Exit(1)
    }


[<EntryPoint>]
let main argv =
    if argv.Length <> 2 then 
        printfn "Wrong number of arguments"
        Environment.Exit(1)
    
    let uri = [|sprintf "http://%s:%s" argv.[0] argv.[1]|]
    
    application uri |> Async.RunSynchronously
    
    0 // return an integer exit code
