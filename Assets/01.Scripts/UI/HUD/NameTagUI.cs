using TMPro;
using UnityEngine;
using MySecondWorld.Network;

namespace MySecondWorld.UI
{
    // 이름표 하나
    // HealthBarUI와 같은 역할.
    // ScreenFollowUI가 위치를 따라가고, 이 클래스는 글자만 채움.
    public class NameTagUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _text;

        private ScreenFollowUI _follow;

        private void Awake() => _follow = GetComponent<ScreenFollowUI>();

        public void Bind(NameTag tag)
        {
            _text.text = tag.DisplayName;
            _follow?.SetTarget(tag.Anchor);
        }

        public void Unbind() => _follow?.SetTarget(null);
    }
}
/*
[서버: 접속 직후]
① 플레이어
SpawnPlayer
 ├─ PlayerFactory.Create()
 ├─ ServerSetName(playerId)     ← _displayName = 로그인 ID
 └─ AddPlayerForConnection()
      └─ OnStartServer()        ← _defaultName 비어 있음, ①을 안 덮음
      └─ 스폰 메시지 ──→ [공통 클라 흐름]
────────────────────────────────────────

② 씬에 배치된 NPC
씬 로드 (GameNetworkManager:321)
 └─ netId == 0 인 오브젝트마다 NetworkServer.Spawn(go)
      └─ OnStartServer()        ← _defaultName("상점 주인") → _displayName
      └─ 스폰 메시지 ──→ [공통 클라 흐름]
────────────────────────────────────────
③ 몬스터 (풀 재사용)
MonsterAI 리스폰 (:370)
 └─ 풀에서 꺼낸 GameObject 상태 초기화
 └─ NetworkServer.Spawn(gameObject)
      └─ OnStartServer()        ← _defaultName("고블린") → _displayName
      └─ 스폰 메시지 ──→ [공통 클라 흐름]

죽음/반납 → Despawn → 클라 OnStopClient → ClientDespawned → 이름표 Release
────────────────────────────────────────
④ 컴패니언
CompanionFactory.Create (:21)
 ├─ Instantiate
 ├─ (선택) ServerSetName("○○의 펫")   ← 이름을 주인 기준으로 정할 때만
 └─ NetworkServer.Spawn(instance)
      └─ OnStartServer()        ← _defaultName이 있으면 채움
      └─ 스폰 메시지 ──→ [공통 클라 흐름]
────────────────────────────────────────
[공통 클라 흐름]                     │
[클라: 스폰 메시지 수신]             ▼
 Instantiate        ────→ Awake, OnEnable  (값 아직 없음)
   ▼
 NetworkClient.ApplySpawnPayload
   ├─ SetActive(true)
   ├─ DeserializeClient → _displayName = 로그인 ID ②  ← 여기서 값이 채워짐
   └─ InvokeIdentityCallbacks
        └─ NameTag.OnStartClient()
             ├─ _activeOnClient.Add(this)
             └─ ClientSpawned?.Invoke(this)        ③ ← 스포너에게 알림
                  ▼
 NameTagSpawner.Attach(tag)
   ├─ _pool.Rent()                  ← 이름표 UI 꺼내기(SetActive true)
   └─ ui.Bind(tag)                  ← Bind로 글자를 채우고, 따라갈 대상을 정함
        ├─ _text.text = tag.DisplayName   ← ②에서 채운 로그인 ID ✔
        └─ ScreenFollowUI.SetTarget(tag.Anchor)

*/

/* 
   스포너가 늦게 켜지는 경우
    NameTagSpawner.OnEnable
   ├─ 이벤트 구독 (이후 스폰 대비)
   └─ foreach NameTag.ActiveOnClient → Attach   ← 이미 스폰된 플레이어 따라잡기
*/