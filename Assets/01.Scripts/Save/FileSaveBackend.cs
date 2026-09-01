using System;
using System.IO;
using UnityEngine;

namespace MySecondWorld.Save
{
    // 서버용 JSON 파일 저장소
    public class FileSaveBackend : ISaveBackend
    {
        private readonly string _saveDirectory;

        public FileSaveBackend(string saveDirectory = null)
        {
            if (string.IsNullOrEmpty(saveDirectory))
            {
                string basePath = !string.IsNullOrEmpty(Application.persistentDataPath)
                    ? Application.persistentDataPath
                    : AppDomain.CurrentDomain.BaseDirectory;
                _saveDirectory = Path.Combine(basePath, "saves");
            }
            else
            {
                _saveDirectory = saveDirectory;
            }

            if (!Directory.Exists(_saveDirectory))
            {
                Directory.CreateDirectory(_saveDirectory);
            }
        }

        private string GetFilePath(string playerId)
        {
            string sanitized = string.Join("_", playerId.Split(Path.GetInvalidFileNameChars()));
            return Path.Combine(_saveDirectory, $"{sanitized}.json");
        }

        public void Save(string playerId, SaveData data)
        {
            try
            {
                string path = GetFilePath(playerId);
                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(path, json);
                Debug.Log($"[Save] FileSaveBackend: Saved playerId={playerId} to {path}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Save] FileSaveBackend 저장 실패 (playerId={playerId}): {ex.Message}");
            }
        }

        public SaveData? Load(string playerId)
        {
            try
            {
                string path = GetFilePath(playerId);
                if (!File.Exists(path))
                    return null;

                string json = File.ReadAllText(path);
                if (string.IsNullOrEmpty(json))
                    return null;

                return JsonUtility.FromJson<SaveData>(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Save] FileSaveBackend 로드 실패 (playerId={playerId}): {ex.Message}");
                return null;
            }
        }

        public void Delete(string playerId)
        {
            try
            {
                string path = GetFilePath(playerId);
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Save] FileSaveBackend 삭제 실패 (playerId={playerId}): {ex.Message}");
            }
        }
    }
}