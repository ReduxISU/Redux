Redux is an open source repository of problems, their solutions, verifiers, and reductions. The project has a particular emphasis on NP-Complete and NP-Hard problems

## Documentation

Documentation can be found on github [here](Documentation/index.md)

## Deployment

This application can be deployed as its own standalone server. 

Assuming you have dotnet installed, you can run the following: 

````
dotnet run 

````
this will start a dotnet API server that will listen on port 27000


This application can alternatively be deployed via a docker docker image. Assuming you have Docker installed, run the following:

````

docker build -t reduxapi .
docker run -it --rm -p 27000:80 --name reduxapi reduxapi

````
This will start a local server via docker. Note that this server is using production binaries, so warnings will be distinct from using dotnet run, which is not converted to
a binary only standalone image. 