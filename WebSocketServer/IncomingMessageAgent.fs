module IncomingMessageAgent
open Types


// The 'incoming' domain mailbox implemented with MailboxProcessor. 
// 'Complications' can be plugged in here in order for the Agent to send those
// complications ActionMsgs. Requires the list of complications, the outgoing
// Agent (for ActionMsg relay), as well as the 'self' MailboxProcessor.

let incomingAgent 
    (cplx: Complication list) 
    (outgoingAgent: ActionMsgAgent)
    (incomingAgent: ActionMsgAgent) 
    =
    
    let rec agentLoop () = async {
        let! dmsg = incomingAgent.Receive()
        match dmsg.msgType with
        | AllMsg -> 
            do! agentLoop ()
        | CloseMsg -> 
            do! Async.Sleep 100
            do! agentLoop ()
        | Console s -> 
            s |> printfn "%s"
            do! agentLoop ()
        | DeadMsg -> do! agentLoop ()
        }
    agentLoop ()

// Creates the MailboxProcessor and passes it back. Used in Program.fs in order
// to pass to various consumers and/or complications.

let startIncomingAgent complications outgoingAgent = 
    MailboxProcessor.Start (incomingAgent complications outgoingAgent)
    

