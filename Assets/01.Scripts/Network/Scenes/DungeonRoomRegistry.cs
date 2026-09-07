using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace MySecondWorld.Network
{
    /// <summary>
    /// 던전 씬별 Room 목록을 서버에서 관리하는 클래스.
    /// </summary>
    public class DungeonRoomRegistry
    {
        public const int DefaultMaxPlayersPerRoom = 4;

        // 방 정원. AssignRoom이 "빈자리 있는 첫 방부터 채우기" 방식이라 이 값이 곧 파티 분할 규칙이 된다.
        // 접속 순서대로 앞에서부터 채우므로 5명 입장 시:
        //   정원 4 → 4+1 / 정원 3 → 3+2 / 정원 2 → 2+2+1
        // 시연 중에 구성을 바꿔볼 수 있도록 GameNetworkManager 인스펙터에서 주입한다.
        private readonly int _maxPlayersPerRoom;

        public DungeonRoomRegistry(int maxPlayersPerRoom = DefaultMaxPlayersPerRoom)
        {
            _maxPlayersPerRoom = Math.Max(1, maxPlayersPerRoom);
        }

        private class DungeonRoom
        {
            public Guid roomId;
            public List<int> connIds;
            public int maxPlayers;

            public DungeonRoom(Guid id, int maxPlayers)
            {
                roomId = id;
                connIds = new List<int>();
                this.maxPlayers = maxPlayers;
            }
        }

        // 같은 이름의 씬이라도 room은 여러개 있을 수 있다.
        private Dictionary<string, List<DungeonRoom>> _rooms
            = new Dictionary<string, List<DungeonRoom>>();

        // 인원 0명이 된 방을 즉시 닫지 않고 유예 중인 방의 취소 토큰.
        // 씬 경계에서 판정이 깜빡이면 LeaveRoom→AssignRoom이 반복되는데,
        // 즉시 닫아버리면 그때마다 몬스터가 소멸→재스폰을 반복하고,
        // 소멸 중이던 몬스터의 matchId가 새로 배정된 방과 어긋나
        // "서버엔 있는데 안 보이는데 때리는" 유령 몬스터가 된다.
        private readonly Dictionary<Guid, CancellationTokenSource> _pendingClose = new();
        private const float LeaveGraceSeconds = 3f;

        /// <summary>
        /// 방이 새로 만들어졌을 때 (씬 경로, roomId). 그 방 전용 몬스터 세트를 스폰하는 신호.
        ///
        /// AssignRoom이 직접 발행하지 않고 호출부가 <see cref="NotifyRoomOpened"/>로 발행한다.
        /// 이 신호를 받으면 몬스터가 그 자리에서 스폰되는데, 그 시점에 플레이어의 matchId가
        /// 아직 새 방으로 안 바뀌어 있으면 Mirror가 옵저버를 계산할 때 플레이어를 빼버린다.
        /// 결과는 "서버엔 있는데 클라엔 안 보이는 몬스터" — 반드시 matchId를 먼저 세팅할 것.
        /// </summary>
        public event Action<string, Guid> OnRoomOpened;

        /// <summary>
        /// 마지막 인원이 나가 방이 사라졌을 때 (씬 경로, roomId). 그 방 몬스터를 정리하는 신호.
        /// </summary>
        public event Action<string, Guid> OnRoomClosed;

        /// <summary>
        /// 씬 경로가 던전 씬인지 판단한다.
        /// </summary>
        public bool IsDungeonScene(string sceneName)
        {
            return sceneName.Contains("Dungeon/");
        }

        /// <summary>
        /// 플레이어를 해당 씬의 방에 배정한다.
        /// 빈 자리가 있는 방이 있으면 그 방에 추가하고, 없으면 새 방을 만든다.
        /// </summary>
        /// <returns>배정된 방의 roomId</returns>
        public Guid AssignRoom(string sceneName, int connId) => AssignRoom(sceneName, connId, out _);

        /// <param name="isNewRoom">
        /// 새 방이 만들어졌으면 true. 이때만 호출부가 matchId를 세팅한 뒤
        /// <see cref="NotifyRoomOpened"/>를 불러야 한다.
        /// 기존 방에 합류한 경우(false)는 그 방 몬스터가 이미 있으므로 발행하지 않는다.
        /// </param>
        public Guid AssignRoom(string sceneName, int connId, out bool isNewRoom)
        {
            isNewRoom = false;

            //기존 방들 중 해당 씬 이름이 없으면, 해당 씬 이름으로 새로 방 목록 생성
            if (!_rooms.ContainsKey(sceneName))
                _rooms[sceneName] = new List<DungeonRoom>();

            var roomList = _rooms[sceneName];

            // 해당 씬 이름으로 된 방 목록을 순회하여
            // 방에 연결된 인원의 수가 maxPlayer보다 낮으면 해당 방에 추가 후 roomId반환
            foreach (var room in roomList)
            {
                if (room.connIds.Count < room.maxPlayers)
                {
                    room.connIds.Add(connId);
                    // 유예 중이던 방이면 닫힘을 취소 — 경계 깜빡임으로 잠깐 나갔다 온 것과 동일 취급
                    if (_pendingClose.TryGetValue(room.roomId, out var pendingCts))
                    {
                        pendingCts.Cancel();
                        pendingCts.Dispose();
                        _pendingClose.Remove(room.roomId);
                    }
                    return room.roomId;
                }
            }

            // 빈 방 없음 → 새 방 생성 (던전 방 하나 생성 → 고유 roomId)
            var newRoom = new DungeonRoom(Guid.NewGuid(), _maxPlayersPerRoom);
            newRoom.connIds.Add(connId);
            roomList.Add(newRoom);

            isNewRoom = true;
            return newRoom.roomId;
        }

        /// <summary>
        /// 새 방이 열렸음을 알린다. 호출부가 플레이어의 matchId를 그 방으로 세팅한 '뒤에' 부를 것.
        /// 순서가 뒤바뀌면 몬스터가 스폰되는 시점에 플레이어가 아직 옛 match에 속해 있어
        /// 옵저버에서 제외되고, 서버에만 존재하는 몬스터가 된다.
        /// </summary>
        public void NotifyRoomOpened(string sceneName, Guid roomId)
        {
            OnRoomOpened?.Invoke(sceneName, roomId);
        }

        /// <summary>
        /// 플레이어를 해당 씬의 모든 방에서 제거한다.
        /// 인원이 0이 된 방은 목록에서 삭제한다.
        /// </summary>
        public void LeaveRoom(string sceneName, int connId)
        {
            if (!_rooms.ContainsKey(sceneName))
                return;

            var roomList = _rooms[sceneName];

            foreach (var room in roomList)
                room.connIds.Remove(connId);

            // 즉시 삭제하지 않고, 빈 방마다 유예 기간을 건다.
            foreach (var room in roomList)
            {
                if (room.connIds.Count > 0) continue;
                if (_pendingClose.ContainsKey(room.roomId)) continue; // 이미 유예 중

                var cts = new CancellationTokenSource();
                _pendingClose[room.roomId] = cts;
                DelayedCloseAsync(sceneName, room.roomId, cts.Token).Forget();
            }
        }

        private async UniTaskVoid DelayedCloseAsync(string sceneName, Guid roomId, CancellationToken ct)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(LeaveGraceSeconds), cancellationToken: ct);
            }
            catch (OperationCanceledException)
            {
                return; // 유예 중 재입장 — 닫지 않는다
            }
            finally
            {
                _pendingClose.Remove(roomId);
            }

            if (!_rooms.TryGetValue(sceneName, out var roomList)) return;
            for (int i = roomList.Count - 1; i >= 0; i--)
            {
                if (roomList[i].roomId != roomId) continue;
                if (roomList[i].connIds.Count > 0) break; // 유예 중 다른 사람이 들어옴
                roomList.RemoveAt(i);
                OnRoomClosed?.Invoke(sceneName, roomId);
                break;
            }
        }

        /// <summary>
        /// 디버그용: 특정 방의 현재 인원 수를 반환한다.
        /// </summary>
        public int GetRoomPlayerCount(string sceneName, Guid roomId)
        {
            if (!_rooms.ContainsKey(sceneName))
                return 0;

            foreach (var room in _rooms[sceneName])
            {
                if (room.roomId == roomId)
                    return room.connIds.Count;
            }

            return 0;
        }
    }
}
