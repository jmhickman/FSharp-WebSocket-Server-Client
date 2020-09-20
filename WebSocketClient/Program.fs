open System

open Types
open Console
open MailboxDomainIncoming
open MailboxDomainOutgoing
open WebSocketMsgHandlers
open MailboxContextTracker
open MailboxConnectionTracker


[<EntryPoint>]
let main argv =
    if argv.Length <> 2 then 
        printfn "Wrong number of arguments"
        Environment.Exit(1)
    
    // Initialization stuff. Unlike the server, the context and connection 
    // trackers are separate in order to deal with cyclic dependency issues.
    // Complications are consumers of DomainMsg record from the DomainInbox.
    // The outbox is a protocol-level send mechanism
    // the domain outbox is the handler for complications wanting to send
    // messages out of the domain.
    // The Ctx mailbox is the Context Tracker.
    // The domain inbox is for DomainMsg records that have come in from the
    // protocol layer.
    // And finally, the Ct mailbox is for the client to manage its active and
    // potential server connections and is unique to the client code.
    let complications = []
    let ombx = getOutbox ()
    let dombx = getDomainOutbox ombx
    let cmbx = getCtxbox ()
    let dimbx = getDomainInbox complications dombx cmbx
    let ctmbx = getCtbox dimbx cmbx
    
    // Place the initial target host into the tracker.
    ctmbx.Post ({host = argv.[0]; port = argv.[1]}|> AddFailoverCt)
    
    // Kick off initial connection attempts. Bails out if it can't connect.
    match ctmbx.PostAndReply ReconnectCt with
    | Ok _ -> ()
    | Failed -> 
        printfn "Failed to connect to initial server(s)"
        Environment.Exit(1)
    
    controlLoop cmbx ctmbx dombx |> Async.Start

    // Do nothing on a long schedule. Just here so that the program doesn't 
    // terminate until ended in another portion of the app.
    let rec idleloop () = async {
        do! Async.Sleep 600000
        do! idleloop ()
        }

    idleloop () |> Async.RunSynchronously

    0 // return an integer exit code
