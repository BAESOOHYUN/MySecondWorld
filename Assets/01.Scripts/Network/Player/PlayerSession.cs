using System;
using System.Collections.Generic;
using UnityEngine;

namespace MySecondWorld.Network
{
    /// <summary>
    /// 플레이어 한 명의 상태 메모장 (서버만 보유).
    /// </summary>
    public class PlayerSession
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ResetLocalPlayerName()
        {
            LocalPlayerName = null; // Domain Reload OFF 환경에서도 매번 초기화
        }
        public static string LocalPlayerName { get; set; }

        public HashSet<string> loadedScenes;       // 클라이언트에 로드된 씬 목록
        public string currentScene;                // 플레이어 GameObject가 속한 씬 (MoveGameObjectToScene 대상)
        public Vector3 pendingSpawnPosition;       // ACK 대기 중 임시 저장할 스폰 위치
        public string pendingSpawnScene;           // ACK 대기 중 임시 저장할 스폰 씬
        public bool isSpawned;                     // 스폰 완료 여부 (CheckZones에서 아직 스폰 안 된 플레이어 스킵용)

        public string playerId;                    // SaveManager 키
        public Vector3 lastSafePosition;           // 마지막 안전 위치

        public float fallTimer;                    // 낙하 경과 시간

        public Guid roomId = Guid.Empty;
    }
}