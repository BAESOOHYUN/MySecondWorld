using System;
using System.Collections.Generic;

namespace MySecondWorld.Network
{
    /// <summary>
    /// 던전 씬별 Room 목록을 서버에서 관리하는 클래스.
    /// </summary>
    public class DungeonRoomRegistry
    {
        private class DungeonRoom
        {
            public Guid roomId;
            public List<int> connIds;
            public int maxPlayers = 4;

            public DungeonRoom(Guid id)
            {
                roomId = id;
                connIds = new List<int>();
            }
        }

        // 같은 이름의 씬이라도 room은 여러개 있을 수 있다.
        private Dictionary<string, List<DungeonRoom>> _rooms
            = new Dictionary<string, List<DungeonRoom>>();

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
            var newRoom = new DungeonRoom(Guid.NewGuid()); 
            newRoom.connIds.Add(connId);
            roomList.Add(newRoom);
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

            roomList.RemoveAll(r => r.connIds.Count == 0);
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
