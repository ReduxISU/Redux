using System;
using System.Collections.Generic;

namespace API.Problems.NPComplete.NPC_SHORTESTPATH.Solvers;

class Queue<T>
{
	private LinkedList<T> elements = new LinkedList<T>();

	//Count the number of elements in the queue
	public int Count => elements.Count;

    //Add elements to the queue
    public void Enqueue(T item)
	{
		elements.AddLast(item);
	}

	//Remove elements from the queue
	public T Dequeue()
	{
		if(elements.Count == 0)
		{
			throw new InvalidOperationException("The queue is empty");
		}

		T item = elements.First.Value;
		elements.RemoveFirst();
		return item;
	}

	//Peek item at the front of the queue
	public T Peek()
	{
		if(elements.Count == 0)
		{
			throw new InvalidOperationException("The queue is empty");
		}

		return elements.First.Value;
	}
}
