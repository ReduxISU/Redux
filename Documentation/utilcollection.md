
# UtilCollection

## Introduction
UtilCollections is a class that combines sets and lists, or *collections*, into one type of object, and provides a constructor that builds specified collections from strings. UtilCollections is also recursive, as the elements of the collection can themselves be collections. 

UtilCollections is meant as a helper class to parse instances of problems in Redux. The class understands the format of Problem instance strings (the strings that define a particular instance of a problem). It provides methods that assert that the problem instance is correctly defines a problems variables, and also provides methods to do calculations with those variables. 

## Format of input string
UtilCollection expects strings in the format that is typically used by Redux. A set of elements \(e_0,...,e_k\) uses curly braces as follows:
```
{e_0,...,e_k}
```
So a set may look something like \{a,b,c\}. A list will be represented using parenthesis as follows:
```
(e_0,...,e_k)
```
As such, an example of a list could look something like (1,2,3). 

As an example, the default instance of clique is:
```
(({1,2,3,4},{{4,1},{1,2},{4,3},{3,2},{2,4}}),3)
```

## Utility Methods
UtilCollections provides a few Utility methods to help process and extract the instance information. Each method works for both ordered and unordered collections, unless specified otherwise.

### Indexing
If the collection is ordered, then you can index into the list using square brackets to get each element of the list, the same as you would for an array. Returns a UtilCollections object.

### Enumeration
Using foreach loops to iterate over each element in the collection is also possible, for example:
```
foreach (UtilCollection item in Collection)
```

### Intersect
Returns a new UtilCollection object that is the intersection of the two collections. Only works with unordered collections for now.

### Union
Returns a new UtilCollections object that is the union of the two collections. Only works with unordered collections.

### Equals
Returns if the two UtilCollections objects are equal to each other. Comparison is done by value.

### Count
Returns the length of the collection.

### ToList
Returns a list representing the elements of the collection. If the collection is ordered, it returns the same list. If the collection is unordered, it is cast to a list and then returned.

### ToString
Returns the string representation of the UtilCollection.

### Contains
Checks if the collection contains another UtilCollection object. Comparison is done by value. Returns true if it contains it, false if it doesn't.

### Add
Adds a UtilCollection object to the collection.

### parseInt
Attempts to parse and return itself as an integer.

## Assertion Methods
UtilCollection also provides ways to assert that the instance is structured the way that it should be, that things are sets where they should be, and things are lists where they should be. Asserts will cause an error to be thrown if the conditions are not met.

### assertOrdered
Asserts that the collection is ordered.

### assertUnordered
Asserts that the collection is unordered.

### assertPair
Asserts that the collection is both unordered and has a certain length. The default length is 2, and a custom length can be passed in.

### assertCount
Asserts that the collection has the same count or length as the argument. Works for both unordered and ordered collections.\


## Example

The following code is how the knapsack problem uses the UtilCollections class to parse its problem instance.

```c#
    UtilCollection collection = new UtilCollection(HWVInput);
    instance = collection.ToString();
    collection.assertPair(3);
    items = collection[0];
    items.assertUnordered();
    W = int.Parse(collection[1].ToString());
    V = int.Parse(collection[2].ToString());
    foreach (UtilCollection item in items) item.assertPair();
```
Knapsack is defined as {<H, W, V> where H is a set of items (w,v) and there is a subset of items in H whose collective weight is less than or equal to W and whose collective value is equal or greater than V.} 'collection.assertPair(3)' makes sure that the top level of the collection is a list with three elements, H, W, and V. We can then assign the first element of the collection to items, a field of the knapsack class. Since W and V are integers, we can parse them as so and assign them to the appropiate fields. The last line also asserts that each element in items is a pair, as described in the problem definition.
