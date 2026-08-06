using UnityEngine;

namespace MySecondWorld.Save
{
    /// <summary>
    /// PlayerPrefs 기반 세이브 서비스.
    /// VContainer에 Singleton으로 등록됨. 직접 new 하지 말 것.
    /// 향후 서버 DB 교체 시 이 클래스 내부만 교체한다.
    /// </summary>
    public class SaveManager
    {
        private const string KeyScenes     = "save_scenes_";
        private const string KeyPosX       = "save_pos_x_";
        private const string KeyPosY       = "save_pos_y_";
        private const string KeyPosZ       = "save_pos_z_";
        private const string KeyCompanions = "save_companions_";
        private const char   SceneSep      = '|';

        /// <param name="playerId">플레이어 식별자 (netId 등). 키 충돌 방지용.</param>
        public void Save(string playerId, SaveData data)
        {
            PlayerPrefs.SetString(KeyScenes + playerId, string.Join(SceneSep, data.lastSceneNames ?? System.Array.Empty<string>()));
            PlayerPrefs.SetFloat(KeyPosX + playerId, data.lastPosition.x);
            PlayerPrefs.SetFloat(KeyPosY + playerId, data.lastPosition.y);
            PlayerPrefs.SetFloat(KeyPosZ + playerId, data.lastPosition.z);
            PlayerPrefs.SetString(KeyCompanions + playerId, string.Join(SceneSep, data.recruitedCompanionIds ?? System.Array.Empty<string>()));
            PlayerPrefs.Save();
        }

        /// <returns>저장된 데이터가 없으면 null.</returns>
        public SaveData? Load(string playerId)
        {
            if (!PlayerPrefs.HasKey(KeyScenes + playerId))
                return null;

            return new SaveData
            {
                lastSceneNames = PlayerPrefs.GetString(KeyScenes + playerId)
                    .Split(SceneSep, System.StringSplitOptions.RemoveEmptyEntries),
                lastPosition = new Vector3(
                    PlayerPrefs.GetFloat(KeyPosX + playerId),
                    PlayerPrefs.GetFloat(KeyPosY + playerId),
                    PlayerPrefs.GetFloat(KeyPosZ + playerId)),
                recruitedCompanionIds = PlayerPrefs.GetString(KeyCompanions + playerId)
                    .Split(SceneSep, System.StringSplitOptions.RemoveEmptyEntries)
            };
        }

        // 현재 미사용.
        // -회원 탈퇴
        // -새 게임 시작(세이브 초기화)
        // -테스트/디버그용 리셋
        public void Delete(string playerId)
        {
            PlayerPrefs.DeleteKey(KeyScenes + playerId);
            PlayerPrefs.DeleteKey(KeyPosX + playerId);
            PlayerPrefs.DeleteKey(KeyPosY + playerId);
            PlayerPrefs.DeleteKey(KeyPosZ + playerId);
            PlayerPrefs.DeleteKey(KeyCompanions + playerId);
            PlayerPrefs.Save();
        }
    }
}
