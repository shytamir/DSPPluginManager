using System;

namespace DSPPluginManager.Contracts
{
    public sealed class PluginConfigurationEntry<T>
    {
        private readonly Func<T> read;
        private readonly Action<T> write;

        internal PluginConfigurationEntry(Func<T> read, Action<T> write)
        {
            this.read = read ?? throw new ArgumentNullException("read");
            this.write = write ?? throw new ArgumentNullException("write");
        }

        public T Value
        {
            get { return read(); }
            set
            {
                if (typeof(T) == typeof(string) &&
                    object.ReferenceEquals(value, null))
                {
                    throw new ArgumentNullException("value");
                }
                write(value);
            }
        }
    }
}
