using System;
using System.Collections.Generic;

namespace API.Problems.NPComplete.NPC_SHORTESTPATH.Solvers;

class DijkstraPriorityQueue<T>
{
    // (T item, int priority) is a tuple
    private readonly List<(T node, int priority)> _heap = new List<(T node, int priority)>();

    public int Count => _heap.Count;

    /* Note the indices for storing a binary tree in a linear collection:
     * parent: (i-1)/2
     * left child: (2*i)+1
     * right child: (2*i)+2
     */

    // insert
    public void Enqueue(T item, int priority)
    {
        _heap.Add((item, priority));
        BubbleUp(_heap.Count - 1);
    }

    // remove minimum
    public T Dequeue()
    {
        if (_heap.Count == 0)
            throw new InvalidOperationException("Queue is empty");

        T min = _heap[0].node; // we just want the item, priorty isn't relevent externally

        // move the bottom element to the top
        _heap[0] = _heap[^1];
        // remove the last element
        _heap.RemoveAt(_heap.Count - 1);

        // rectify by bubbling down as needed
        if(_heap.Count > 0)
            BubbleDown(0);
    
        return min;
    }

    private void BubbleUp(int index)
    {
        while (index > 0)
        {
            int parentIndex = (index - 1) / 2;

            // already sorted
            if (_heap[index].priority >= _heap[parentIndex].priority)
                return;

            // otherwise swap with parent
            Swap(parentIndex, index);
            index = parentIndex; // keep bubbling up from there
        }
    }

    private void BubbleDown(int index)
    {
        int lastIndex = _heap.Count - 1;
        while (true)
        {
            int leftChildIndex = (2 * index) + 1;
            int rightChildIndex = (2 * index) + 2;

            int smallest = index;

            if (leftChildIndex <= lastIndex && _heap[leftChildIndex].priority < _heap[smallest].priority)
                smallest = leftChildIndex; // Check left child

            if (rightChildIndex <= lastIndex && _heap[rightChildIndex].priority < _heap[smallest].priority)
                smallest = rightChildIndex; // Check right child

            if (smallest == index)
                return; // Heap property is satisfied, stop bubbling down

            Swap(index, smallest);
            index = smallest; // Continue bubbling down from the new position
        }
    }

    private void Swap(int i, int j)
    {
        (_heap[j], _heap[i]) = (_heap[i], _heap[j]); // Swap the tuples at indices i and j
    }
}
