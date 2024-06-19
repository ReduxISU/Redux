
The back-end code has four primarily important sections you may have to work with.

### Interfaces
All interfaces are contained in the Interfaces folder. These are generally complete but occasionally require some additions. Any changes to interfaces will also require fixing all classes that implement that interface

### Problems
Any problems that need to be fixed or added will be in the Problems/NPComplete folder. With some minor nuance, they all follow a similar pattern, and should all implement any necessary interfaces.

### Additional Tools
There are a number of additional classes which can be used to make problems better aligned and easier to understand. Currently, these are primarily classes and functions for dealing with directed and undirected graphs. They can be found in the Interfaces folder.

### Navigation
In the AdditionalControllers/Navigation folder all API controllers regarding the retrieval of available problems and algorithms can be found. For the most part these should not require any changes. If additions do need to be made be extra careful, as the front end is heavily reliant on many of these calls.