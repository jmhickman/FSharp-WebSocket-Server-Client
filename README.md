# WebSocket-ServerClient

An exploration of a WebSocket server and client in F#.

The purpose of this project was to learn and create a WebSocket server and client pair that had the following attributes:  

1) Both a server and a client in the same project.  
    Lots of stuff on Github is one half of what I need, or the other, but not both.  
2) No object programming (Creating a 'Server' type with methods and state and behavior, etc)  
    I don't like object programming, and will actively work to minimize its use here.  
3) No/minimal imperative style (`while` loops)
    I want to use functional workflows where ever I can.  
4) Not an entire framework for making web applications (Saturn, Giraffe, Sauve.io), just the parts I need.  
    I'm not using those things precisely because they contain far more functionality than I need.  
5) Learn `async` workflows and other F# forms I'm unfamiliar with.  
    I want to continue to push the boundaries of what I know.