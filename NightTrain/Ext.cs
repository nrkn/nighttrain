using System;
using System.Collections.Generic;

public static class Ext
{
    // random
    public static void Shuffle<T>(this Random rng, List<T> list )
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);        // 0..i inclusive
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    public static T Pick<T>(this Random rng, List<T> list) {
        return list[ rng.Next(list.Count) ];
    }
}
