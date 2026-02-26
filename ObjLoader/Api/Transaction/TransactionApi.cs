using ObjLoader.Api.Events;

namespace ObjLoader.Api.Transaction
{
    internal sealed class TransactionApi : ITransactionApi
    {
        private readonly SceneEventApi _eventApi;

        internal TransactionApi(SceneEventApi eventApi)
        {
            _eventApi = eventApi ?? throw new ArgumentNullException(nameof(eventApi));
        }

        public SceneTransaction BeginTransaction()
        {
            return new SceneTransaction(_eventApi);
        }
    }
}