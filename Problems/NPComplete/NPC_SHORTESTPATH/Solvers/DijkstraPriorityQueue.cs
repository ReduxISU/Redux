using System;
using System.Collections.Generic;

public class DijkstraPriorityQueue<T>
{
    // (T item, int priority) is a tuple
    private List<(T node, int priority)> heap = new();

    public int Count => heap.Count;

    /* Note the indices for storing a binary tree in a linear collection:
     * parent: (i-1)/2
     * left child: (2*i)+1
     * right child: (2*i)+2
     */


    // insert
    public void Enqueue(T item, int priority)
    {
        heap.Add((item, priority));

        // bubble up as needed
        BubbleUp(heap.Count - 1);
    }

    // remove minimum
    public T Dequeue()
    {
        if (heap.Count == 0)
            throw new InvalidOperationException("queue is empty");

        T min = heap[0].node; // we just want the item, priorty isn't relevent externally

        // move the bottom element to the top
        heap[0] = heap[heap.Count - 1];

        // remove the last element
        heap.RemoveAt(heap.Count - 1);

        // rectify by bubbling down as needed
        if(heap.Count > 0)
        {
            BubbleDown(0);
        }

        return min;
    }

    private void BubbleUp(int index)
    {
        while (index > 0)
        {
            int parentIndex = (index - 1) / 2;

            // already sorted
            if (heap[index].priority >= heap[parentIndex].priority)
                break;

            // otherwise swap with parent
            Swap(parentIndex, index);

            index = parentIndex; // keep bubbling up from there
        }
    }

    private void BubbleDown(int index)
    {
        int lastIndex = heap.Count - 1;
        while (true)
        {
            int leftChildIndex = (2 * index) + 1;
            int rightChildIndex = (2 * index) + 2;

            int smaller = index;

            if (leftChildIndex <= lastIndex && heap[leftChildIndex].priority < heap[smaller].priority)
            {
                smaller = leftChildIndex;
            }

            if (rightChildIndex <= lastIndex && heap[rightChildIndex].priority < heap[smaller].priority)
            {
                smaller = rightChildIndex;
            }

            if (smaller == index)
            {
                break;
            }

            Swap(index, smaller);
            index = smaller;
        }
    }

    private void Swap(int i, int j)
    {
        (T, int) temp = heap[i];
        heap[i] = heap[j];
        heap[j] = temp;
    }
}
