# WebSocket-ServerClient

## Purpose

An exploration of a WebSocket server and client in F#.

The purpose of this project was to learn and create a WebSocket server and client pair that had the following attributes:  

1) Both a server and a client in the same project.  
    Lots of stuff on Github is a server, or a client, but not both. Or it employs object style, etc.  
2) No object programming (Creating a 'Server' type with methods and state and behavior, etc)  
    I don't like object programming, and actively worked to minimize its use here.  
3) No/minimal imperative style (such as `for`/`while` loops)  
    I want to use functional workflows where ever I can.  
4) Not an entire framework for making web applications (Saturn, Giraffe, Sauve.io), just the parts I need.  
    I'm not using those things precisely because they contain far more functionality than I need. Their documentation around the WebSocket usecase is also terrible/missing, etc.  
5) Learn `async` workflows and other F# forms I'm unfamiliar with.  
    
## Description

The project contains two Dotnet Core 3.1 console applications. One is a WebSocket server using Asp.net Core's Kestrel server, and the other is a client designed to establish a link with one or more WebSocket servers.

### Running

Server - Compile and run like `WebsockerServer 127.0.0.1 5000` or an IP on your host. The normal Asp.net Core output will appear, but Ctrl-C will not kill the entire application. Please see 'Q' below.  
Client - Compile and run like `WebsocketClient 127.0.0.1 5000` or the IP of the server. There is no output on successful connection, or on unsuccessful reconnection attempts, but if the initial connection attempt fails, it will say so and exit.

### Basic interface

Both applications have a similar console interface that is only good for testing. They are unapologetically low-function and contain almost no error handling! Certain keypresses have behavior associated with them:  
  * A - On the client, prompts for a ip address and port for adding an additional server to connect to  
  * L - On the client, dumps out the list of servers the client knows about to connect to
  * P - On client and server, lists currently active connections as GUIDs. It's normal for the client and server to list different GUIDs for their respective side of the connection; there is no synchronized 'identity.'
  * Q - On client and server, terminates active connections and exits to the terminal.
  * R - On the client, if there are no active connections, or if not all potential connections are active, will attempt to re-establish them. There is no logic for skipping existing connections!
  * S - On client and server, starts a short workflow for sending a message. The application will display all GUIDs representing a connection. Copy and paste the GUID you wish to send the message to, and hit Enter. A message prompt will appear, and a message can be entered. The client is hard coded to send Binary protocol messages; the server Text. This can be modified in `MailboxOutgoingMessage.fs` `CSendAsync` function.
  
## Warnings
In case this wasn't obvious, this project is a demo and is certainly not fit as-is for anything important. It contains zero logging, zero error handling ('error ignoring' is more apt), and almost certainly misses the finer points of the WebSocket protocol. It doesn't support connecting to or listening on hostnames instead of ip addresses. It doesn't support wss:// WebSockets. It doesn't sanity-check anything, really. Simply put, if you use it correctly, it works. If you don't, it will probably explode.
