using System;

namespace GasVentilation.Core.Tests;

internal static class TestOrders
{
    public static int[] Identity(int count)
    {
        int[] order = new int[count];
        for (int i = 0; i < count; i++)
        {
            order[i] = i;
        }
        return order;
    }

    public static int[] Shuffled(int count, int seed)
    {
        int[] order = Identity(count);
        Random rng = new Random(seed);
        for (int i = count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }
        return order;
    }
}
