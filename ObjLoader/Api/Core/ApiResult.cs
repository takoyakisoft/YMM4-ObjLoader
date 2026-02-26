namespace ObjLoader.Api.Core
{
    public readonly struct ApiResult<T>
    {
        public bool IsSuccess { get; }
        public T? Value { get; }
        public string? Error { get; }

        private ApiResult(bool isSuccess, T? value, string? error)
        {
            IsSuccess = isSuccess;
            Value = value;
            Error = error;
        }

        public static ApiResult<T> Ok(T value) => new(true, value, null);
        public static ApiResult<T> Fail(string error) => new(false, default, error ?? throw new ArgumentNullException(nameof(error)));
    }
}