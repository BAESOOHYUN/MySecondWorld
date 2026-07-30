using System;

namespace MySecondWorld.Network.Web
{
    // out T : 제너릭 공변성(covariance) 선언
    // 인터페이스에서 T를 반환만 하고(get only) 입력으로 안 받을 때 쓸 수 있다.
    public interface IResourceHandle<out T> : IDisposable
    {
        T Value { get; }
    }
}
/*
out이 없으면 이게 안 됨:
IResourceHandle<Texture2D> tex = ...;
IResourceHandle<object> obj = tex; // 컴파일 에러

out이 있으면 됨:
IResourceHandle<object> obj = tex; // OK — Texture2D는 object의 하위타입이니까
*/