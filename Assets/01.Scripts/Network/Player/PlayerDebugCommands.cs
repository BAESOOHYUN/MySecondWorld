using System;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace MySecondWorld.Network.Player
{
    // 테스트용 임시 스크립트 — 골드 지급/동행 영입의 실제 인게임 트리거(퀘스트/이벤트/상점)가
    // 생기면 이 파일은 삭제한다.
    // 버튼이 부를 수 있도록 public 진입점을 두고, 에디터 편의용 단축키도 함께 유지한다.
    public class PlayerDebugCommands : NetworkBehaviour
    {
        private static GameNetworkManager Manager => (GameNetworkManager)NetworkManager.singleton;

        // 다른 어셈블리(Combat 등)에서 구독할 수 있도록 이벤트 노출 (순환참조 방지)
        public static event Action<Vector3, Quaternion, Guid, Scene> OnSpawnDummyRequested;

        #region 버튼 진입점 (DebugButtonRelay가 호출)

        public void RequestStartingGold() { if (isOwned) CmdGrantStartingGold(); }

        public void RequestWeaponTestKit() { if (isOwned) CmdGrantWeaponTestKit(); }
        public void RequestArmorTestKit() { if (isOwned) CmdGrantArmorTestKit(); }
        public void RequestSpawnDummy() 
        { 
            Debug.Log($"[DebugClient] 🎯 샌드백 소환 버튼 클릭됨! isOwned={isOwned}, isLocalPlayer={isLocalPlayer}");
            if (isOwned) CmdSpawnDummy(); 
        }

        #endregion

        // 안드로이드는 하드웨어 키보드가 없으면 Keyboard.current가 null이라 반드시 가드해야 한다.
        private void Update()
        {
            if (!isLocalPlayer) return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.f7Key.wasPressedThisFrame) RequestStartingGold();
            if (keyboard.f8Key.wasPressedThisFrame) RequestSpawnDummy();
            if (keyboard.f11Key.wasPressedThisFrame) RequestArmorTestKit();
            if (keyboard.f12Key.wasPressedThisFrame) RequestWeaponTestKit();
        }

        #region Command (서버에서 실행)


        [Command]
        private void CmdGrantStartingGold()
        {
            // 🛡️ Manager.SessionService로 직접 깔끔하게 호출!
            Manager.SessionService.GrantStartingGold(connectionToClient);
        }

        [Command]
        private void CmdGrantWeaponTestKit()
        {
            Manager.SessionService.GrantWeaponTestKit(connectionToClient);
        }

        [Command]
        private void CmdGrantArmorTestKit()
        {
            Manager.SessionService.GrantArmorTestKit(connectionToClient);
        }

        [Command]
        private void CmdSpawnDummy()
        {
            Debug.Log($"[DebugServer] 📩 CmdSpawnDummy 서버 도착! 플레이어={gameObject.name}, 위치={transform.position}");

            // 플레이어 앞 2.0미터 지점 및 플레이어를 마주보는 각도 계산
            Vector3 spawnPos = transform.position + transform.forward * 2.0f;
            Quaternion spawnRot = Quaternion.LookRotation(-transform.forward, Vector3.up);

            // 네비메시 유효 위치 샘플링
            if (UnityEngine.AI.NavMesh.SamplePosition(spawnPos, out var hit, 5.0f, UnityEngine.AI.NavMesh.AllAreas))
            {
                spawnPos = hit.position;
            }

            Guid matchId = TryGetComponent(out NetworkMatch m) ? m.matchId : Guid.Empty;
            int listenerCount = OnSpawnDummyRequested != null ? OnSpawnDummyRequested.GetInvocationList().Length : 0;
            Debug.Log($"[DebugServer] 🚀 OnSpawnDummyRequested 이벤트 발송: 구독자={listenerCount}명, matchId={matchId}, scene={gameObject.scene.name}, pos={spawnPos}");

            if (listenerCount == 0)
            {
                Debug.LogError("[DebugServer] ❌ OnSpawnDummyRequested 구독자가 0명입니다! MonsterDummySpawner가 초기화되지 않았습니다.");
            }

            OnSpawnDummyRequested?.Invoke(spawnPos, spawnRot, matchId, gameObject.scene);
        }
        #endregion
    }
}