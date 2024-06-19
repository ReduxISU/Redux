## Running
After cloning the repository to your machine, ensure that you have .Net 6 installed. If that is working correctly you should be able to run the back end locally using the command, 

```bash
$ dotnet run
```
this will start a dotnet API server that will listen on port 27000

When running properly, you should be able to access and test the API locally as seen in [apiDoc](./APIDocumentation.md).
    
## Docker
This application can alternatively be deployed via a docker docker image. Assuming you have Docker installed, run the following:

````

docker build -t reduxapi .
docker run -it --rm -p 27000:80 --name reduxapi reduxapi

````
This will start a local server via docker. Note that this server is using production binaries, so warnings will be distinct from using dotnet run, which is not converted to
a binary only standalone image. 