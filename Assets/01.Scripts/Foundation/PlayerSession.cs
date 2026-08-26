using System;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

namespace MySecondWorld.Network
{
    /// <summary>
    /// 플레이어 한 명의 상태 메모장 (서버만 보유).
    /// </summary>
    public class PlayerSession
    {
        #region 로컬 플레이어 이름 (정적, 세션과 무관 — 클라 로컬 표시용)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ResetLocalPlayerName()
        {
            LocalPlayerName = null; // Domain Reload OFF 환경에서도 매번 초기화
        }
        public static string LocalPlayerName { get; set; }
        #endregion

        #region 번들/씬 로드 상태
        public HashSet<string> sentBundleLabels = new(); // 이미 전송한 레이블 추적
        public HashSet<string> loadedScenes;       // 클라이언트에 로드된 씬 목록
        #endregion

        #region 스폰 상태
        public string currentScene;                // 플레이어 GameObject가 속한 씬 (MoveGameObjectToScene 대상)
        public Vector3 pendingSpawnPosition;       // ACK 대기 중 임시 저장할 스폰 위치
        public string pendingSpawnScene;           // ACK 대기 중 임시 저장할 스폰 씬
        public bool isSpawned;                     // 스폰 완료 여부 (CheckZones에서 아직 스폰 안 된 플레이어 스킵용. 0.5초 간격 순회 null 회피)
        #endregion

        #region 포탈 전환 상태
        public bool isTransitioningPortal;         // 포탈 페이드 전환 진행 중 (CheckZones 정기 루프/중복 트리거 가드)
        public string confirmedPortalScene;        // PortalSceneLoadedAckMessage로 확인된 씬 이름 (전환 시작 시 null로 리셋)
        public float portalReentryCooldownUntil;   // 이 시각(Time.time) 전에는 반대쪽 포탈 트리거를 무시 (막 도착한 직후 즉시 왕복 방지)
        #endregion

        #region 낙하 안전망
        public Vector3 lastSafePosition;           // 마지막 안전 위치
        public string lastSafeScene;               // 그 위치가 속해 있던 씬 (구조 시 복귀 대상)
        public float fallTimer;                    // 낙하 경과 시간
        public float ungroundedTimer;               // 발밑 지형 없음 경과 시간 (존 안이어도 허공일 수 있음)
        #endregion

        #region 던전 방 배정
        public Guid roomId = Guid.Empty;
        #endregion

        #region 세이브/식별
        public string playerId;                    // SaveManager 키
        public bool isNewPlayer;                    // 세이브 데이터 없이 최초 접속한 경우 true — 시작 골드 지급 판단용
        #endregion

        #region 동행 NPC
        public string[] recruitedCompanionIds = Array.Empty<string>(); // SaveData 왕복용 — RemovePlayer 저장 시 유실 방지
        public NetworkIdentity companionIdentity;   // 현재 스폰된 동행 NPC (없으면 null)
        #endregion
    }
}