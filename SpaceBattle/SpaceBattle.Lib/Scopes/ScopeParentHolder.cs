namespace SpaceBattle.Lib
{
    public sealed class ScopeParentHolder
    {
        private object _current;

        public ScopeParentHolder(object initialBaseScope)
        {
            ArgumentNullException.ThrowIfNull(initialBaseScope);
            _current = initialBaseScope;
            Volatile.Write(ref _current, initialBaseScope);
        }

        public object Current => Volatile.Read(ref _current);

        public void Swap(object nextBaseScope)
        {
            ArgumentNullException.ThrowIfNull(nextBaseScope);
            Volatile.Write(ref _current, nextBaseScope);
        }
    }
}
