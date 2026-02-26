namespace ObjLoader.Api.Events
{
    internal sealed class SceneEventApi : ISceneEventApi
    {
        private readonly object _lock = new();
        private readonly List<WeakReference<EventHandler<SceneChangedEventArgs>>> _sceneHandlers = new();
        private readonly List<WeakReference<EventHandler<ObjectTransformChangedEventArgs>>> _transformHandlers = new();
        private readonly List<WeakReference<EventHandler<LightChangedEventArgs>>> _lightHandlers = new();
        private readonly List<WeakReference<EventHandler<MaterialChangedEventArgs>>> _materialHandlers = new();

        public event EventHandler<SceneChangedEventArgs> SceneChanged
        {
            add { if (value != null) AddWeak(_sceneHandlers, value); }
            remove { RemoveWeak(_sceneHandlers, value); }
        }

        public event EventHandler<ObjectTransformChangedEventArgs> ObjectTransformChanged
        {
            add { if (value != null) AddWeak(_transformHandlers, value); }
            remove { RemoveWeak(_transformHandlers, value); }
        }

        public event EventHandler<LightChangedEventArgs> LightChanged
        {
            add { if (value != null) AddWeak(_lightHandlers, value); }
            remove { RemoveWeak(_lightHandlers, value); }
        }

        public event EventHandler<MaterialChangedEventArgs> MaterialChanged
        {
            add { if (value != null) AddWeak(_materialHandlers, value); }
            remove { RemoveWeak(_materialHandlers, value); }
        }

        internal void RaiseSceneChanged(SceneChangedEventArgs args) => FireWeak(_sceneHandlers, args);
        internal void RaiseObjectTransformChanged(ObjectTransformChangedEventArgs args) => FireWeak(_transformHandlers, args);
        internal void RaiseLightChanged(LightChangedEventArgs args) => FireWeak(_lightHandlers, args);
        internal void RaiseMaterialChanged(MaterialChangedEventArgs args) => FireWeak(_materialHandlers, args);

        private void AddWeak<T>(List<WeakReference<T>> list, T handler) where T : class
        {
            lock (_lock)
            {
                PurgeDeadWeak(list);
                list.Add(new WeakReference<T>(handler));
            }
        }

        private void RemoveWeak<T>(List<WeakReference<T>> list, T? handler) where T : class
        {
            if (handler == null) return;
            lock (_lock)
            {
                list.RemoveAll(wr => !wr.TryGetTarget(out var t) || ReferenceEquals(t, handler));
            }
        }

        private void FireWeak<TArgs>(List<WeakReference<EventHandler<TArgs>>> list, TArgs args) where TArgs : EventArgs
        {
            List<WeakReference<EventHandler<TArgs>>> snapshot;
            lock (_lock) { snapshot = new List<WeakReference<EventHandler<TArgs>>>(list); }

            var dead = new List<WeakReference<EventHandler<TArgs>>>();
            foreach (var wr in snapshot)
            {
                if (wr.TryGetTarget(out var handler))
                    handler(null, args);
                else
                    dead.Add(wr);
            }

            if (dead.Count > 0)
            {
                lock (_lock)
                {
                    foreach (var d in dead) list.Remove(d);
                }
            }
        }

        private static void PurgeDeadWeak<T>(List<WeakReference<T>> list) where T : class
        {
            list.RemoveAll(wr => !wr.TryGetTarget(out _));
        }
    }
}