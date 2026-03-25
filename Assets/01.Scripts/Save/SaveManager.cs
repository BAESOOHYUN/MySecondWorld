using UnityEngine;

namespace MySecondWorld.Save
{
    /// <summary>
    /// PlayerPrefs 기반 세이브 싱글톤.
    /// 향후 서버 DB 교체 시 이 클래스 내부만 교체한다.
    /// </summary>
    public class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }

        private const string KeyScenes = "save_scenes_";
        private const string KeyPosX   = "save_pos_x_";
        private const string KeyPosY   = "save_pos_y_";
        private const string KeyPosZ   = "save_pos_z_";
        private const char   SceneSep  = '|';

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <param name="playerId">플레이어 식별자 (netId 등). 키 충돌 방지용.</param>
        public void Save(uint playerId, SaveData data)
        {
            string id = playerId.ToString();
            PlayerPrefs.SetString(KeyScenes + id, string.Join(SceneSep, data.lastSceneNames ?? System.Array.Empty<string>()));
            PlayerPrefs.SetFloat(KeyPosX + id, data.lastPosition.x);
            PlayerPrefs.SetFloat(KeyPosY + id, data.lastPosition.y);
            PlayerPrefs.SetFloat(KeyPosZ + id, data.lastPosition.z);
            PlayerPrefs.Save();
        }

        /// <returns>저장된 데이터가 없으면 null.</returns>
        public SaveData? Load(uint playerId)
        {
            string id = playerId.ToString();
            if (!PlayerPrefs.HasKey(KeyScenes + id))
                return null;

            return new SaveData
            {
                lastSceneNames = PlayerPrefs.GetString(KeyScenes + id)
                    .Split(SceneSep, System.StringSplitOptions.RemoveEmptyEntries),
                lastPosition = new Vector3(
                    PlayerPrefs.GetFloat(KeyPosX + id),
                    PlayerPrefs.GetFloat(KeyPosY + id),
                    PlayerPrefs.GetFloat(KeyPosZ + id))
            };
        }

        public void Delete(uint playerId)
        {
            string id = playerId.ToString();
            PlayerPrefs.DeleteKey(KeyScenes + id);
            PlayerPrefs.DeleteKey(KeyPosX + id);
            PlayerPrefs.DeleteKey(KeyPosY + id);
            PlayerPrefs.DeleteKey(KeyPosZ + id);
            PlayerPrefs.Save();
        }
    }
}
