module MailboxContextTracker
open System
open System.Net
open System.Net.WebSockets
open System.Security.Cryptography.X509Certificates
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Server.Kestrel.Core

open Types
open Common
open WebSocketMsgHandlers


// This function is designed to nothing, with as relatively low an impact as
// possible. It only exists because the Asp.Net Core middleware will 
// automatically close any websocket as soon as the Async/Task completes.
// This this just does nothing until the WebSocket is closed elsewere in the
// stack. It's stupid as hell, but then the requirement is stupid as hell too.
let rec infiniSpin (ws: WebSocket) = async {
    do! Async.Sleep 2500
    if ws.State = WebSocketState.Open then
        do! infiniSpin ws
    else ws.GetHashCode() |> printfn "Socket closed:%i" 
    }


// A MailboxProcessor that contains and controls the shared state of the 
// WebSocket connections, called ServiceContexts. It tracks active 
// ServiceContexts ServiceContexts may be added, removed, or dropped. A list of
// active ServiceContexts will be returned on request. The incoming message 
// handler is asynchronously started when a new WebSocket connection is 
// successfully established. More simple than its Client counterpart, as it 
// doesn't track previous sessions nor does it contain the notion of future
// connections.
let serviceContextTracker (mbx: CtxMailboxProcessor) =
    let serviceContextList = []
    
    let rec postLoop (sCTL: ServiceContext list) = async {
        let! msg = mbx.Receive()
        
        match msg with
        | AddCtx ctx ->
            ctx.guid.ToString() |> printfn "Adding %s to context tracker"
            messageLoop mbx ctx |> Async.Start
            return! ctx::sCTL |> postLoop
        | RemoveCtx ctx ->
            ctx.guid.ToString() |> printfn "Removing %s from context tracker" 
            return! sCTL |> List.filter (fun sctx -> ctx.guid <> sctx.guid) |> postLoop
        | GetCtx r ->
            r.Reply sCTL
            return! postLoop sCTL
        | KillAllCtx r -> 
            r.Reply (0 |> Some)
            return! postLoop []
        } 
        
    postLoop serviceContextList

// Create the Context Tracker Mailbox instance, and provide a way to pass it out.
let cmbx = MailboxProcessor.Start serviceContextTracker

let returnCmbx () = cmbx

// A simple mock for using a PFX cert for wss:// connections. It is hacky and 
// garbage.
let configureKestrel (host: string, port: string) (options : KestrelServerOptions) =
    let addr = IPAddress.Parse(host)
    let iport = int(port)
    let serverCert = new X509Certificate2("", "")
    options.Listen(addr, iport, 
        fun listenOptions -> listenOptions.UseHttps(serverCert) |> ignore)


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