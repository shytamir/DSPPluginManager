using System;

namespace DSPPluginManager.Logging
{
    internal static class LogPayloadFormatter
    {
        internal static string Format(object payload)
        {
            if (payload == null)
            {
                return "<null>";
            }

            try
            {
                return payload.ToString() ?? "<null>";
            }
            catch (Exception exception)
            {
                return "<formatting failed for " +
                    GetTypeName(payload) + "; formatter threw " +
                    GetTypeName(exception) + ": " +
                    GetExceptionMessage(exception) + ">";
            }
        }

        private static string GetTypeName(object value)
        {
            try
            {
                Type type = value.GetType();
                return type.FullName ?? type.Name;
            }
            catch (Exception)
            {
                return "<type unavailable>";
            }
        }

        private static string GetExceptionMessage(Exception exception)
        {
            try
            {
                return exception.Message ?? "<message unavailable>";
            }
            catch (Exception)
            {
                return "<message unavailable>";
            }
        }
    }
}
