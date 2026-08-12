using System;

namespace DSPPluginManager.Contracts
{
    public sealed class PluginLogger
    {
        private readonly string identifier;
        private readonly string displayName;
        private readonly Action<object> information;
        private readonly Action<object> warning;
        private readonly Action<object> error;

        internal PluginLogger(
            string identifier,
            string displayName,
            Action<object> information,
            Action<object> warning,
            Action<object> error
        )
        {
            this.identifier = identifier ??
                throw new ArgumentNullException("identifier");
            this.displayName = displayName ??
                throw new ArgumentNullException("displayName");
            this.information = information ??
                throw new ArgumentNullException("information");
            this.warning = warning ??
                throw new ArgumentNullException("warning");
            this.error = error ?? throw new ArgumentNullException("error");
        }

        internal string Identifier => identifier;

        internal string DisplayName => displayName;

        public void Information(object payload)
        {
            TryWrite(information, payload);
        }

        public void Warning(object payload)
        {
            TryWrite(warning, payload);
        }

        public void Error(object payload)
        {
            TryWrite(error, payload);
        }

        private static void TryWrite(Action<object> write, object payload)
        {
            try
            {
                write(payload);
            }
            catch
            {
            }
        }
    }
}
