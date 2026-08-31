using UnityEngine;
using Mirror;
using MySecondWorld.Combat.Companion;

namespace MySecondWorld.Network.Companion
{
    public class CompanionAdminService
    {
        [System.Serializable]
        public struct CompanionEntry
        {
            public string id;
            public GameObject prefab;
        }

        private readonly ServerSessionService _sessionService;
        private readonly CompanionEntry[] _registry;

        public CompanionAdminService(ServerSessionService sessionService, CompanionEntry[] registry)
        {
            _sessionService = sessionService;
            _registry = registry;
        }

        public GameObject GetPrefab(string companionId)
        {
            if (_registry == null) return null;
            foreach (var entry in _registry)
                if (entry.id == companionId) return entry.prefab;
            return null;
        }

        // 테스트용 — Recruit() 실제 인게임 진입점(퀘스트/이벤트)이 생기면 이 메서드는 삭제
        public void Recruit(NetworkConnectionToClient conn, string companionId)
        {
            var prefab = GetPrefab(companionId);
            if (prefab == null)
            {
                Debug.LogWarning($"[CompanionAdmin] companionId '{companionId}'에 매칭되는 프리팹이 레지스트리에 없음");
                return;
            }
            _sessionService.RecruitCompanion(conn, prefab, companionId);
        }

        // F10 디버그 키 전용 — 사망 애니메이션까지 거치는 진짜 죽음
        public void Kill(NetworkConnectionToClient conn)
        {
            if (!_sessionService.Sessions.TryGetValue(conn.connectionId, out var session)) return;
            if (session.companionIdentity == null) return;
            if (session.companionIdentity.TryGetComponent(out ICompanionServerState serverState))
                serverState.DebugKill();
        }

        // F11 디버그 키 — 부활(생사만 되돌림)
        public void Revive(NetworkConnectionToClient conn)
        {
            if (!_sessionService.Sessions.TryGetValue(conn.connectionId, out var session)) return;
            if (session.companionIdentity == null) return;
            if (session.companionIdentity.TryGetComponent(out ICompanionServerState serverState))
                serverState.Revive(conn.identity.transform.position);
        }

        // 초상화 클릭 소환해제 — 생사와 무관하게 감춘다. 재접속 시 자동 재소환 안 되도록 기록.
        public void Dismiss(NetworkConnectionToClient conn)
        {
            if (!_sessionService.Sessions.TryGetValue(conn.connectionId, out var session)) return;
            if (session.companionIdentity == null) return;
            if (session.companionIdentity.TryGetComponent(out ICompanionServerState serverState))
                serverState.Dismiss();
            session.companionPresent = false;
        }

        // 초상화 클릭 재소환 — 이미 존재하는 컴패니언을 내 위치로 다시 보여준다(죽어있었다면 죽은 채로).
        // 아예 처음 영입하는 경우(컴패니언이 존재조차 안 함)는 Recruit()가 처리한다.
        public void Summon(NetworkConnectionToClient conn)
        {
            if (!_sessionService.Sessions.TryGetValue(conn.connectionId, out var session)) return;
            if (session.companionIdentity == null) return;
            if (session.companionIdentity.TryGetComponent(out ICompanionServerState serverState))
                serverState.Summon(conn.identity.transform.position);
            session.companionPresent = true;
        }

        public void ToggleFollow(NetworkConnectionToClient conn)
        {
            if (!_sessionService.Sessions.TryGetValue(conn.connectionId, out var session)) return;
            if (session.companionIdentity == null) return;
            if (session.companionIdentity.TryGetComponent(out ICompanionServerState serverState))
                serverState.ToggleFollowPaused();
        }
    }
}