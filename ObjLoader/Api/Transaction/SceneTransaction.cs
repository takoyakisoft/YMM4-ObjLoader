using ObjLoader.Api.Events;

namespace ObjLoader.Api.Transaction
{
    public sealed class SceneTransaction : IDisposable
    {
        private readonly SceneEventApi _eventApi;
        private readonly List<Action> _pendingEvents = new();
        private bool _isDisposed;
        private bool _isRolledBack;

        internal SceneTransaction(SceneEventApi eventApi)
        {
            _eventApi = eventApi ?? throw new ArgumentNullException(nameof(eventApi));
        }

        public void RecordEvent(Action eventRaise)
        {
            if (_isDisposed || _isRolledBack) return;
            if (eventRaise != null) _pendingEvents.Add(eventRaise);
        }

        public void Rollback()
        {
            _isRolledBack = true;
            _pendingEvents.Clear();
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            if (!_isRolledBack)
            {
                foreach (var ev in _pendingEvents)
                {
                    try { ev(); }
                    catch { }
                }
            }
            _pendingEvents.Clear();
        }
    }
}