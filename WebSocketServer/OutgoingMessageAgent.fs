module OutgoingMessageAgent
open System.Net.WebSockets
open System.Threading

open Types
open Common

// The 'outgoing' domain mailbox implemented with MailboxProcessor. Passed to 
// complications in order for them to create outgoing messages over the 
// transport protocol. Requires the underlying protocol's outbox, as well as
// the 'self' MailboxProcessor.

let outgoingAgent 
    (protocolOutbox: ProtocolOutbox)
    (agentMsgbox: ActionMsgAgent)
    =

    let rec agentLoop () = async {
        let! dmsg = agentMsgbox.Receive()
        
        match dmsg.msgType with
        | AllMsg -> 
            // call a All function with the dmsg
            do! agentLoop ()
        | CloseMsg -> 
            do! closeWebSocket dmsg.ctx.ws
            do! agentLoop ()
        | Console s -> 
            {ctx = dmsg.ctx; msg = (s|> packStringBytes|> BinaryMsg)} |> protocolOutbox.Post 
            do! agentLoop ()
        | DeadMsg -> do! agentLoop ()
        }
    agentLoop ()

// Creates the MailboxProcessor and passes it back. Used in Program.fs in order
// to pass to various consumers and/or complications.

let startOutgoingAgent (protocolOutbox: ProtocolOutbox) =
    MailboxProcessor.Start (outgoingAgent protocolOutbox)
