using Cysharp.Threading.Tasks;
using UnityEngine;
using Mirror;

namespace MySecondWorld.Combat.Monster
{
    // Animator Write Defaults 이슈로 SkinnedMeshRenderer가 꺼진 채 고정되는 버그 방어.
    // MonsterAI(서버 전용 동적부착)와 달리 purePrefab에 사전 부착되어 서버/모든 클라에서 각자 독립적으로 실행됨
    // — 각 피어의 Animator가 겪는 동일 버그를 개별적으로 고쳐야 하기 때문.
    [RequireComponent(typeof(Animator))] 
    public class MonsterRendererVisibilityFix : MonoBehaviour
    {
        private SkinnedMeshRenderer[] _renderers;
        private void Awake() => _renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true); 
        private void OnEnable()
        {
            // 진단용 — 원인 확정되면 제거
            Debug.Log($"[MeshFix] OnEnable {gameObject.name} isServer={NetworkServer.active} isClient={NetworkClient.active} renderers={_renderers.Length}", this);
            FixNextFrame().Forget();
        }
        // 스폰 직후 SkinnedMeshRenderer가 꺼진 채로 고정되는 보스 버그 방어(Write Defaults + 특정 클립 커브 추정).
        // NetworkAnimator가 초기 동기화 시 animator.Play()로 상태를 한 번 더 강제 점프시켜
        // Spawn() 직후 바로 켜도 다시 꺼지므로, 한 프레임 기다렸다가 재보정한다.
        private async UniTaskVoid FixNextFrame()
        {
            await UniTask.Yield(this.GetCancellationTokenOnDestroy());
            foreach (var r in _renderers)
                r.enabled = true;

            // 진단용 — 원인 확정되면 제거
            Debug.Log($"[MeshFix] applied {gameObject.name} renderers={_renderers.Length}", this);
        }
    }
}
