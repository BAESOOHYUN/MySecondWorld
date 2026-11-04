using System;
using System.IO;
using UnityEngine;

namespace MySecondWorld.Save
{
    /// <summary>
    /// 서버 전용 JSON 세이브 관리자 (VContainer Lifetime.Singleton으로 등록).
    /// 유저별 데이터(위치, 씬, 컴패니언 등)를 saves/{playerId}.json 파일로 안전하게 보관합니다.
    /// </summary>
    public class SaveManager
    {
        private readonly string _saveDirectory;

        public SaveManager()
        {
            // 에디터/서버 공통: 데이터 저장 경로 아래 saves 폴더
            string basePath = !string.IsNullOrEmpty(Application.persistentDataPath)
                ? Application.persistentDataPath
                : AppDomain.CurrentDomain.BaseDirectory;

            _saveDirectory = Path.Combine(basePath, "saves");

            if (!Directory.Exists(_saveDirectory))
                Directory.CreateDirectory(_saveDirectory);
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
                string json = JsonUtility.ToJson(data, true); // 보기 편하게 들여쓰기 포맷
                string tempPath = path + ".tmp";

                // 임시파일 → 교체 방식: 쓰는 도중 서버가 죽어도 원본 파일은 안전 (단일 파일 손상 리스크 완화)
                File.WriteAllText(tempPath, json);
                // File.Move(a, b, overwrite:true)는 .NET Standard 2.1엔 없어서 직접 지우고 교체
                if (File.Exists(path))
                    File.Delete(path);

                File.Move(tempPath, path);
                Debug.Log($"[Save] 저장 완료 ({playerId}) -> {path}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Save] 저장 실패 ({playerId}): {ex.Message}");
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
                Debug.LogError($"[Save] 로드 실패 ({playerId}): {ex.Message}");
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
                Debug.LogError($"[Save] 삭제 실패 ({playerId}): {ex.Message}");
            }
        }
    }
}