open System
open System.Net.WebSockets
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting

open Types
open Common
open Console
open MailboxDomainIncoming
open MailboxDomainOutgoing
open WebSocketMsgHandlers
open MailboxContextTracker


// create mailboxes
// Outbox is the DTO layer outbound to the underlying WebSocket transport
// CtxBox is the ContextTracker, so that complications can determine who
// to address DomainMsgs to.
// DomainOutbox is the in-domain handler of Outbound DomainMsgs
let ombx = getOutbox ()
let cmbx = getCtxbox ()
let dombx = getDomainOutbox ombx


controlLoop cmbx dombx |> Async.Start

let complications = []
// Once the list of complications is known, it is passed to the Domain inbox
// so that it can determine which complication to send the messages to.
let dimbx = getDomainInbox complications dombx

// Gross OP that's required to run the Asp.net Core Kestrel server. Due to
// how dumb this initialization process is, I can't cleanly move this down
// into the Types area, because it has to know about the CtxMailbox, but it
// seemingly has no way to take in arbitrary arguments/dependencies.
type Startup() = 
    member this.Configure (app : IApplicationBuilder) = 
        app.UseWebSockets() |> ignore
        app.Run (fun ctx -> 
            let sctx = 
                ctx.WebSockets.AcceptWebSocketAsync().Result
                |> createServiceCtx
            
            sctx |> AddCtx |> cmbx.Post 
            sctx |> messageLoop dimbx |> Async.Start
            
            let rec infiniSpin (sctx: ServiceContext) = async {
                do! Async.Sleep 2500
                if sctx.ws.State = WebSocketState.Open then
                    do! infiniSpin sctx
                else 
                    sctx |> RemoveCtx |> cmbx.Post
                    sctx.ws.Dispose()
                }   

            infiniSpin sctx
            |> Async.StartAsTask :> Task
            )


// Webserver kickoff
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


[<EntryPoint>]
let main argv =
    if argv.Length <> 2 then 
        printfn "Wrong number of arguments"
        Environment.Exit(1)
    
    let uri = [|sprintf "http://%s:%s" argv.[0] argv.[1]|]
    
    application uri |> Async.Start
    
    // Do nothing on a long schedule. Just here so that the program doesn't 
    // terminate until ended in another portion of the app.
    let rec idleloop () = async {
        do! Async.Sleep 600000
        do! idleloop ()
        }

    idleloop () |> Async.RunSynchronously
    0 // return an integer exit code
