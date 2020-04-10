# Distance Vector Routing Simulation

## Requirements

Please make sure that you have .NET Core 3.1 installed so that you are able to run these applications. 

## Running the System

This system is comprised of two projects: DistanceVectorRoutingSimNode and DistanceVectorRoutingSimMasterNode. 
  
The first step is to run DistanceVectorRoutingSimMasterNode. Open a terminal within the project root directory and type `dotnet run <port>` where <port> is the port number on which master should run. 
  
Once the Master Node is running, you can run the Network Nodes. For each Network Node, open a terminal in the DistanceVectorRoutingSimNode root directory and type `dotnet run <name> <node_port> <master_ip> <master_port>` where <name> is the name of the node (either a, b, c, d, e, or f and should be lowercase), <node_port> is the port where the Network Node should run (must be unique to the master and other nodes), <master_ip> is the IP address of the Master Node (this is output by the master node on startup), and <master_port> is the port on which master is running. 
  
To close all of these terminals, please type 'Q' followed by the enter key to gracefully shut down the node and output a log. The log will output into the logs directory in the project root preprended by a time stamp. For Network Nodes, the file name also includes the node name. 
  
Input files are located in the input-files directory. 
  
If you have any issues, please contact us. 
