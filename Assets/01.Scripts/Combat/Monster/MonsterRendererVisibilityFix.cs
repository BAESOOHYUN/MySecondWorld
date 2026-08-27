using Cysharp.Threading.Tasks;
using UnityEngine;
using Mirror;

namespace MySecondWorld.Combat.Monster
{
    // SkinnedMeshRenderer가 꺼진 채 고정되는 버그 방어(원인 미확정 — 필요시 재조사).
    // MonsterAI(서버 전용 동적부착)와 달리 purePrefab에 사전 부착되어 서버/모든 클라에서 각자 독립적으로 실행됨
    // — 각 피어가 자기 로컬 인스턴스를 스스로 재보정해야 하기 때문(재활성화 로직이 서버 전용 컴포넌트에만
    // 있었던 게 원래 버그의 진짜 원인 — 21종 프리팹 전체에 이 컴포넌트를 붙여 해결).
    [RequireComponent(typeof(Animator))] 
    public class MonsterRendererVisibilityFix : MonoBehaviour
    {
        private SkinnedMeshRenderer[] _renderers;
        private void Awake() => _renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        private void OnEnable() => FixNextFrame().Forget();

        // 스폰 직후 SkinnedMeshRenderer가 꺼진 채로 고정됨 — NetworkAnimator 초기 동기화가
        // animator.Play()로 상태를 강제 점프시키는데, Spawn() 직후 바로 켜면 다시 꺼지므로
        // 한 프레임 기다렸다가 재보정한다.
        private async UniTaskVoid FixNextFrame()
        {
            await UniTask.Yield(this.GetCancellationTokenOnDestroy());
            foreach (var r in _renderers)
                r.enabled = true;
        }
    }
}
