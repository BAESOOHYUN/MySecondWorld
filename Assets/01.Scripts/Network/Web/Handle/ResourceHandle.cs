using System;

namespace MySecondWorld.Network.Web
{
    // sealed : 상속 금지로 선언
    // IDisposable 구현체처럼 "이걸 상속해서 변형하면 Dispose 로직이 꼬일 수 있다"는 경우에 씀. 
    public sealed class ResourceHandle<T> : IResourceHandle<T>
    {
        public T Value { get; }
        private readonly Action _onRelease;
        private bool _disposed;

        public ResourceHandle(T value, Action onRelease)
        {
            Value = value;
            _onRelease = onRelease;
        }

        // using var handle = await service.AcquireTextureAsync(url);
        // 패턴으로 쓰면 블록 벗어날 때 자동으로 Dispose() 호출됨
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _onRelease?.Invoke();
        }
    }
}