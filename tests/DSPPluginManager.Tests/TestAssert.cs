using System;

namespace DSPPluginManager.Tests
{
    internal static class TestAssert
    {
        internal static void Equal<T>(T expected, T actual, string field)
        {
            if (!object.Equals(expected, actual))
            {
                throw new InvalidOperationException(
                    field + " mismatch: expected '" + expected +
                    "', found '" + actual + "'."
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

        internal static void Throws<TException>(
            Action action,
            params string[] expectedMessageParts
        ) where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException exception)
            {
                foreach (string expectedPart in expectedMessageParts)
                {
                    if (exception.Message.IndexOf(
                            expectedPart,
                            StringComparison.OrdinalIgnoreCase
                        ) < 0)
                    {
                        throw new InvalidOperationException(
                            "Exception message did not contain '" +
                            expectedPart + "': " + exception.Message
                        );
                    }
                }
                return;
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    "Expected " + typeof(TException).Name + ", found " +
                    exception.GetType().Name + ".",
                    exception
                );
            }

            throw new InvalidOperationException(
                "Expected " + typeof(TException).Name + " but no exception occurred."
            );
        }
    }
}
