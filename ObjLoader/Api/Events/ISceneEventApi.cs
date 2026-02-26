namespace ObjLoader.Api.Events
{
    public interface ISceneEventApi
    {
        event EventHandler<SceneChangedEventArgs> SceneChanged;
        event EventHandler<ObjectTransformChangedEventArgs> ObjectTransformChanged;
        event EventHandler<LightChangedEventArgs> LightChanged;
        event EventHandler<MaterialChangedEventArgs> MaterialChanged;
    }
}