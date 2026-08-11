using System.Threading;

namespace DSPPluginManager.Bootstrap
{
    internal sealed class OneShotGate
    {
        private int entered;

        internal bool TryEnter()
        {
            return Interlocked.CompareExchange(ref entered, 1, 0) == 0;
        }
    }
}
