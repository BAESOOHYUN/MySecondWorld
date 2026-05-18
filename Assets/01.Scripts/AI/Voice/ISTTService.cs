using System.Threading;
using Cysharp.Threading.Tasks;

namespace MySecondWorld.AI.Voice
{
    public interface ISTTService
    {
        public bool IsListening { get; }
        public UniTask<string> ListenAsync(CancellationToken ct = default);
        public void StopListening();
    }
}
