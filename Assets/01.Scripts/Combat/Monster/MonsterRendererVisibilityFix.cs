using Cysharp.Threading.Tasks;
using UnityEngine;

namespace MySecondWorld.Combat.Monster
{
    // SkinnedMeshRenderer가 꺼진 채 고정되는 버그 방어(원인 미확정 — 필요시 재조사).
    // MonsterAI(서버 전용 동적부착)와 달리 purePrefab에 사전 부착되어 서버/모든 클라에서 각자 독립적으로 실행됨
    // — 각 피어가 자기 로컬 인스턴스를 스스로 재보정해야 하기 때문(재활성화 로직이 서버 전용 컴포넌트에만
    // 있었던 게 원래 버그의 진짜 원인 — 21종 프리팹 전체에 이 컴포넌트를 붙여 해결).
    //
    // 던전 방 재배정(경계 유예 등)으로 "이미 애니메이션 중인 몬스터를 뒤늦게 옵저버로 합류해서 보는" 경로가
    // 생기면서, 그 초기 NetworkAnimator 풀싱크(animator.Play() 강제 점프)가 기존의 "딱 1프레임" 보정
    // 창을 지나서 도착하는 사례가 나왔다 — 그러면 재보정 기회가 없어 렌더러가 영구히 꺼진 채로 남는다.
    // 그래서 한 프레임이 아니라 일정 시간 동안 매 프레임 강제로 켠다.
    [RequireComponent(typeof(Animator))]
    public class MonsterRendererVisibilityFix : MonoBehaviour
    {
        private const float GuardDurationSeconds = 1.0f;
        private SkinnedMeshRenderer[] _renderers;
        private void Awake() => _renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        private void OnEnable() => GuardAsync().Forget();

        private async UniTaskVoid GuardAsync()
        {
            var token = this.GetCancellationTokenOnDestroy();
            float elapsed = 0f;
            while (elapsed < GuardDurationSeconds)
            {
                foreach (var r in _renderers)
                    r.enabled = true;
                await UniTask.Yield(token);
                elapsed += Time.deltaTime;
            }
        }
    }
}
