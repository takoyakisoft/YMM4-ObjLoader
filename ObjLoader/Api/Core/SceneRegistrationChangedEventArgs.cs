namespace ObjLoader.Api.Core
{
    public sealed class SceneRegistrationChangedEventArgs : EventArgs
    {
        public string InstanceId { get; }
        public bool IsRegistered { get; }

        public SceneRegistrationChangedEventArgs(string instanceId, bool isRegistered)
        {
            InstanceId = instanceId ?? throw new ArgumentNullException(nameof(instanceId));
            IsRegistered = isRegistered;
        }
    }
}