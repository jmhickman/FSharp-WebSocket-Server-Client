module Common
open System
open System.Net
open System.Net.WebSockets
open System.Security.Cryptography.X509Certificates
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Server.Kestrel.Core

open Types

//
// General functions
//

// Small function to clean up ReadKey characters on the console
let crlf () = 
    Console.SetCursorPosition(0, Console.CursorTop)
    Console.Write(" ")
    Console.SetCursorPosition(0, Console.CursorTop)
    Console.Write("")

// This function is a convenience symbol for creating a ServiceContext to send
// to a MailboxProcessor Context Tracker.
let createServiceCtx (ws: WebSocket) : ContextTrackerMessage =
    let g = Guid.NewGuid()
    {ws = ws; guid = g} |> AddCtx

//
// WebServer Functions
//

// A simple mock for using a PFX cert for wss:// connections. It is hacky and 
// garbage.
let configureKestrel (host: string, port: string) (options : KestrelServerOptions) =
    let addr = IPAddress.Parse(host)
    let iport = int(port)
    let serverCert = new X509Certificate2("", "")
    options.Listen(addr, iport, fun listenOptions -> listenOptions.UseHttps(serverCert) |> ignore)


// Stupid thing to hold the "middleware" open in Kestrel so that it doesn't
// kill my sockets.
let rec infiniSpin (ws: WebSocket) = async {
    do! Async.Sleep 2500
    if ws.State = WebSocketState.Open then
        do! infiniSpin ws
    else ws.GetHashCode() |> printfn "Socket closed:%i" 
    }   
