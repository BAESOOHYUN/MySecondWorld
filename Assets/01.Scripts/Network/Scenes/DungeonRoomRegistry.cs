using System;
using System.Collections.Generic;

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

        /// <summary>
        /// 방이 새로 만들어졌을 때 (씬 경로, roomId). 그 방 전용 몬스터 세트를 스폰하는 신호.
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
        public Guid AssignRoom(string sceneName, int connId)
        {
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
                    return room.roomId;
                }
            }

            // 빈 방 없음 → 새 방 생성 (던전 방 하나 생성 → 고유 roomId)
            var newRoom = new DungeonRoom(Guid.NewGuid(), _maxPlayersPerRoom);
            newRoom.connIds.Add(connId);
            roomList.Add(newRoom);

            // 목록에 넣은 뒤에 알린다 — 구독자(스포너 배선)가 곧바로
            // GetRoomPlayerCount 같은 조회를 해도 일관된 상태가 보이도록.
            OnRoomOpened?.Invoke(sceneName, newRoom.roomId);

            return newRoom.roomId;
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

            // RemoveAll 대신 역순 루프 — 사라지는 방마다 OnRoomClosed를 발행해야 하기 때문.
            // (역순이라 제거해도 남은 인덱스가 밀리지 않는다)
            for (int i = roomList.Count - 1; i >= 0; i--)
            {
                if (roomList[i].connIds.Count > 0) continue;

                Guid closedRoomId = roomList[i].roomId;
                roomList.RemoveAt(i);
                OnRoomClosed?.Invoke(sceneName, closedRoomId);
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
