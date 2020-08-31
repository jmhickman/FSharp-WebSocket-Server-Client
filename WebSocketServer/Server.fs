module Server
open System.Net.WebSockets
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder

open Types
open Common
open WebSocketMsgHandlers
open MailboxContextTracker


// This is so stupid, but I have to hold the middleware open for websockets
// and I don't know of a more clever way to do it...
let rec infiniSpin (ws: WebSocket) = async {
    do! Async.Sleep 2500
    if ws.State = WebSocketState.Open then
        do! infiniSpin ws
    else ws.GetHashCode() |> printfn "Socket %i closed" 
    }


let mbox = MailboxProcessor<ContextTrackerMessage>.Start (serviceContextTrackerAgent messageLoop)

let returnMbox () = mbox

// Ugh
// I have no use for middleware at the moment, hence the use of `Run` and
// nothing else
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
