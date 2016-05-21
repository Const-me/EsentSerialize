This sample shows how to use ESENT database for the backend of a WCF service.

The "Shared" project builds the DLL with service contracts that's shared between server and client.

"Server" project is a WCF server host, and "Client" it a simple command-line client.

The service uses named pipe binding configured in code.