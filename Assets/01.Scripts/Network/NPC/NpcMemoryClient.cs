using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using MySecondWorld.Save;

namespace MySecondWorld.Network
{
    public static class NpcMemoryClient
    {
        private const float ResendInterval = 3f;  // Ack가 이 시간 안에 안 오면 재전송
        private const int MaxAttempts = 5;        // 실제로 보낸 횟수 기준
        private const int MaxPending = 50;        // 오래 끊겨 있어도 큐가 무한히 커지지 않게

        // 대기중인 메세지
        private class Pending
        {
            public NpcMemoryAppendMessage msg;
            public float lastSent = -999f;
            public int attempts;
        }

        private static readonly List<Pending> _pending = new();

        // (npcKey, 서버가 준 대화 기록) — NPCBrain이 구독
        public static event Action<string, NpcDialoguePairSave[]> OnLoaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Reset()
        {
            _pending.Clear();
            OnLoaded = null;
        }

        private static bool CanSend =>
            NetworkClient.isConnected && NetworkClient.connection != null && NetworkClient.connection.isAuthenticated;

        // [송신: 클라 → 서버] 대화 기록 요청 (NPC 메뉴를 열 때)
        // 답은 서버가 NpcMemoryResponseMessage로 보내오고, OnResponse가 받는다
        public static void Request(string npcKey)
        {
            if (string.IsNullOrEmpty(npcKey) || !CanSend) return;
            NetworkClient.Send(new NpcMemoryRequestMessage { npcKey = npcKey });
        }


        // 클라 기기                              서버(EC2)
        // NpcMemoryClient.Append
        //   └ NetworkClient.Send(Append) ───▶  OnNpcMemoryAppend 실행
        //      (큐에 보관)                       ├ 검증·캐시에 추가
        //                                        └ conn.Send(Ack) ───┐
        // NpcMemoryClient.OnAck  ◀────────────────────────────────┘
        //   └ 큐에서 제거
           
        //  [송신: 클라 → 서버] 새 대화 한 쌍 올리기 — 미확인 큐에 넣고 보낸다
        //  서버가 받으면 NpcMemoryAckMessage로 확인을 보내오고, OnAck가 받아 큐에서 지운다
        public static void Append(string npcKey, string user, string npc)
        {
            if (string.IsNullOrEmpty(npcKey)) return;
            if (string.IsNullOrWhiteSpace(user) && string.IsNullOrWhiteSpace(npc)) return;

            if (_pending.Count >= MaxPending) _pending.RemoveAt(0);

            var p = new Pending
            {
                msg = new NpcMemoryAppendMessage
                {
                    npcKey = npcKey,
                    pair = new NpcDialoguePairSave
                    {
                        pairId = Guid.NewGuid().ToString("N"),
                        timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                        userText = user,
                        npcText = npc
                    }
                }
            };
            _pending.Add(p);
            TrySend(p);
        }

        // [송신: 클라 → 서버] 한 쌍을 실제로 보낸다 (연결·인증이 안 돼 있으면 큐에만 남김)
        private static void TrySend(Pending p)
        {
            if (!CanSend) return;   // 끊겨 있으면 큐에 남겨 두고, 복구되면 Tick이 다시 보낸다
            NetworkClient.Send(p.msg);
            p.lastSent = Time.unscaledTime;
            p.attempts++;
        }

        // [송신: 클라 → 서버] 1초마다 호출 — 서버 Ack를 못 받은 것을 재전송
        public static void Tick()
        {
            if (_pending.Count == 0 || !CanSend) return;

            foreach (var p in new List<Pending>(_pending))   // 순서대로 보내되 순회 중 제거 안전
            {
                if (Time.unscaledTime - p.lastSent < ResendInterval) continue;
                if (p.attempts >= MaxAttempts)
                {
                    Debug.LogWarning($"[NpcMemory] 전송 포기: {p.msg.pair.pairId}");
                    _pending.Remove(p);
                    continue;
                }
                TrySend(p);
            }
        }

        // [수신: 서버 → 클라] 핸들러 두 개 (GameNetworkManager.OnStartClient가 등록)

        // 서버가 보낸 대화 기록을 받는다 → NPCBrain에 알림
        public static void OnResponse(NpcMemoryResponseMessage msg) => OnLoaded?.Invoke(msg.npcKey, msg.pairs);

        // 서버가 보낸 "받았음" 확인을 받는다 → 미확인 큐에서 제거
        public static void OnAck(NpcMemoryAckMessage msg) => _pending.RemoveAll(p => p.msg.pair.pairId == msg.pairId);
    }
}
