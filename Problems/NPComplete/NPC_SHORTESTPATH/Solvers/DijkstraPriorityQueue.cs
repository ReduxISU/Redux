using System;

public class DijkstraPriorityQueue
{
	private List<(string Node, int Priority)> elements = new List<(string, int)>();

	public int Count => elements.Count;

	public void Enqueue(string node, int priority)
	{
		elements.Add((node, priority));
	}

	public string Dequeue()
	{
		if(elements.Count == 0)
		{
			throw new InvalidOperationException("The queue is empty.");
		}

		int minIndex = 0; 

		for(int i = 1; i < elements.Count; i++)
		{
			if (elements[i].Priority < elements[minIndex].Priority)
			{
				minIndex = i;
			}
		}

		var minElement = elements[minIndex];
		elements.RemoveAt(minIndex);

		return minElement.Node;
	}
}
