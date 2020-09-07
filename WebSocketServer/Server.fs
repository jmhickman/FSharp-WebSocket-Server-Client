module Server
open System.Net.WebSockets
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder

open Types
open Common
open WebSocketMsgHandlers
open MailboxContextTracker


// This function is designed to nothing, with as relatively low an impact as
// possible. It only exists because the Asp.Net Core middleware will 
// automatically close any websocket as soon as the Async/Task completes.
// This this just does nothing until the WebSocket is closed elsewere in the
// stack. It's stupid as hell, but then the requirement is stupid as hell too.
let rec infiniSpin (ws: WebSocket) = async {
    do! Async.Sleep 2500
    if ws.State = WebSocketState.Open then
        do! infiniSpin ws
    else ()
    }


let mbox = MailboxProcessor<ContextTrackerMessage>.Start (serviceContextTrackerAgent messageLoop)

let returnMbox () = mbox

// Gross OP that's required to run the Asp.net Core Kestrel server.
type Startup() = 
    member this.Configure (app : IApplicationBuilder) = 
        app.UseWebSockets() |> ignore
        app.Run (fun ctx -> 
            let ws = ctx.WebSockets.AcceptWebSocketAsync().Result
            ws
            |> createServiceCtx 
            |> postServiceCtxMsg mbox 
            |> Async.Start
            
            infiniSpin ws
            |> Async.StartAsTask :> Task
            )
