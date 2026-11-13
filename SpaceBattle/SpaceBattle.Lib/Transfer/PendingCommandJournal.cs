namespace SpaceBattle.Lib
{
    public class PendingCommandJournal
    {
        private readonly List<CommandDescription> _pending = new List<CommandDescription>();

        public IReadOnlyList<CommandDescription> Pending => _pending;

        public void Add(CommandDescription description)
        {
            _pending.Add(description);
        }

        public bool Remove(string id)
        {
            return _pending.RemoveAll(description => description.Id == id) > 0;
        }

        public void Clear()
        {
            _pending.Clear();
        }
    }
}
