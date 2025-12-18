# Steps to run/edit the project locally

## Initial setup:

1. Clone the Redux backend and frontend repositories

	Backend: https://github.com/ReduxISU/Redux

	Frontend: https://github.com/ReduxISU/Redux_GUI

2. Install .NET version 6.0

    https://dotnet.microsoft.com/en-us/download/dotnet/6.0 

3. Install node.js 

    https://nodejs.org/en/download

4. Download npm packages via the following command

    ```npm install next react react-dom```

## Each runtime (run these concurrently in separate terminals):

1. Run backend (in Redux file structure) (may or may not need to run as admin):

    ```dotnet run```

2. Run frontend (will need to run cmd as admin) (in Redux GUI file structure):

    ```npm run dev```

3. Access webpage at the following link

    http://localhost:3000/

4. Access Swagger API at the following link:

    http://127.0.0.1:27000/swagger/index.html 