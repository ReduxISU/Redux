using SPADE;

/// <summary>
/// provides extension to UtilCollection class for generating every possible subset of a set
/// </summary>
public static class UtilCollectionChooseExtension
{
    /// <summary>
    /// gets every combinination of k items from the collection
    /// </summary>
    /// <param name="coll"></param>
    /// <param name="k">the number of items to get</param>
    /// <returns></returns>
    public static IEnumerable<UtilCollection> Choose(this UtilCollection coll, int k)
    {

        if (k < 0 || k > coll.Count()) yield break;

        UtilCollection[] items = coll.ToList().ToArray();
        int[] indices = Enumerable.Range(0, k).ToArray();

        while (true)
        {
            var comboSet = new HashSet<UtilCollection>(indices.Select(i => items[i]));
            yield return new UtilCollection(comboSet);

            // find next combination
            int i;
            for (i = k - 1; i >= 0; i--)
            {
                if (indices[i] != i + items.Length - k)
                    break;
            }
            if (i < 0) yield break;

            indices[i]++;
            for (int j = i + 1; j < k; j++)
                indices[j] = indices[j - 1] + 1;
        }
    }

    public static IEnumerable<UtilCollection> ChooseUpTo(this UtilCollection coll, int k)
    {
        for (int i = 1; i <= k; i++)
        {
            foreach (var res in coll.Choose(i))
            {
                yield return res;
            }
        }

        yield break;
    }
}