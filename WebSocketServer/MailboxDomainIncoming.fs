module MailboxDomainIncoming
open Types

// The 'incoming' domain mailbox. 'Complications' can be plugged in here
// in order to send messages to other MailboxProcessors based on what comes in
// and what function needs handled. Plugged in with a dummy printfn.
let domainMailbox 
    (cplx: Complication list) 
    (dombx: DomainMailboxProcessor)
    (dimbx: DomainMailboxProcessor) =
    let rec mailboxLoop () = async {
        let! dmsg = dimbx.Receive()
        match dmsg.msgType with
        | AllMsg -> 
            do! mailboxLoop ()
        | CloseMsg -> do! mailboxLoop ()
        | Console s -> 
            s |> printfn "%s"
            do! mailboxLoop ()
        | DeadMsg -> do! mailboxLoop ()
        }
    mailboxLoop ()

// Creates the MailboxProcessor and passes it back. Used in Program.fs in order
// to pass to various consumers and/or complications.
let getDomainInbox complications dombx = 
    MailboxProcessor.Start (domainMailbox complications dombx)
    

