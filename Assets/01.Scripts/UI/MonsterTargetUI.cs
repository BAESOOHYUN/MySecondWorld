using Cysharp.Threading.Tasks;
using UnityEngine;
using R3;
using MySecondWorld.Combat.Monster;
using MySecondWorld.Interaction;

namespace MySecondWorld.UI
{
    /// <summary>
    /// 탭으로 고른 몬스터 위에 조준점을 띄운다. 캔버스에 하나만 둔다.
    /// 선택은 항상 한 명이므로 인스턴스도 하나면 된다.
    /// HP바는 MonsterHealthBarSpawner가 몬스터마다 따로 붙인다 — 여기서 다루지 않는다.
    /// 
    /// PlayerTapAttack이 이걸 호출하지 않고 리졸버를 직접 구독하는 이유:
    /// 전투(Combat)가 UI를 알면 UI→Combat과 맞물려 순환 참조가 된다.
    /// 표현 계층이 도메인 이벤트를 읽는 방향만 남긴다.
    /// </summary>
    [RequireComponent(typeof(ScreenFollowUI))]
    public class MonsterTargetUI : MonoBehaviour
    {
        private ScreenFollowUI _follow;
        private MonsterServerState _state;

        private void Awake() => _follow = GetComponent<ScreenFollowUI>();

        private void Start()
        {
            var resolver = FindAnyObjectByType<TapTargetResolver>();
            if (resolver == null)
            {
                Debug.LogError("[MonsterTargetUI] TapTargetResolver를 찾지 못했습니다.", this);
                return;
            }

            resolver.OnTargetTapped
                .Subscribe(HandleTargetTapped)
                .AddTo(destroyCancellationToken);
        }

        private void OnDisable() => Clear();

        private void HandleTargetTapped(ITapTargetable target)
        {
            if (target is not MonsterTapTarget monster) return;

            Clear();
            _follow.SetTarget(monster.Anchor);

            if (!monster.TryGetComponent(out _state)) return;
            _state.Died += Clear;     // 죽으면 조준점만 걷는다 (HP바는 Layer가 따로 정리)
        }

        private void Clear()
        {
            if (_state != null)
            {
                _state.Died -= Clear;
                _state = null;
            }
            _follow.SetTarget(null);
        }
    }
}