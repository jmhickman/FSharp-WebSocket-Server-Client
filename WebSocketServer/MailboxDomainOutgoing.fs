module MailboxDomainOutgoing
open Types
open Common

// The 'outgoing' domain mailbox. Passed to complications in order for them to
// create outgoing messages over the transport protocol. Plugged in with a 
// dummy 'echo' function.
let domainMailbox 
    (ombx: OutgoingMailboxProcessor)
    (dombx: DomainMailboxProcessor) =
    let rec mailboxLoop () = async {
        let! dmsg = dombx.Receive()
        match dmsg.msgType with
        | AllMsg -> 
            // call a All function with the dmsg
            do! mailboxLoop ()
        | CloseMsg -> 
            closeWebSocket dmsg.ctx.ws |> Async.Start
            do! mailboxLoop ()
        | Console s -> 
            {ctx = dmsg.ctx; msg = (s|> packStringBytes|> BinaryMsg)} |> ombx.Post 
            do! mailboxLoop ()
        | DeadMsg -> do! mailboxLoop ()
        }
    mailboxLoop ()

// Creates the MailboxProcessor and passes it back. Used in Program.fs in order
// to pass to various consumers and/or complications.
let getDomainOutbox (ombx: OutgoingMailboxProcessor) =
    MailboxProcessor.Start (domainMailbox ombx)
