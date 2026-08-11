using System;

namespace DSPPluginManager.ContractTests
{
    internal static class TestAssert
    {
        internal static void Equal<T>(T expected, T actual, string field)
        {
            if (!object.Equals(expected, actual))
            {
                throw new InvalidOperationException(
                    field + ": expected '" + expected + "', found '" + actual + "'."
                );
            }
        }

        internal static void True(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
