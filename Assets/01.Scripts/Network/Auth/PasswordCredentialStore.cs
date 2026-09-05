using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace MySecondWorld.Network
{
    // 서버 전용 자격증명 저장소 — 이름당 비밀번호 해시 하나. 최초 접속 시 자동 등록(회원가입 겸용).
    public class PasswordCredentialStore
    {
        private const int SaltSize = 16;
        private const int HashSize = 32;
        private const int Iterations = 100_000;

        private readonly string _filePath;
        private readonly Dictionary<string, CredentialEntry> _entries = new();

        public PasswordCredentialStore()
        {
            string basePath = !string.IsNullOrEmpty(Application.persistentDataPath)
                ? Application.persistentDataPath
                : AppDomain.CurrentDomain.BaseDirectory;
            string saveDirectory = Path.Combine(basePath, "saves");
            if (!Directory.Exists(saveDirectory))
                Directory.CreateDirectory(saveDirectory);

            _filePath = Path.Combine(saveDirectory, "credentials.json");
            Load();
        }

        private void Load()
        {
            if (!File.Exists(_filePath))
                return;

            try
            {
                string json = File.ReadAllText(_filePath);
                var file = JsonUtility.FromJson<CredentialFile>(json);
                if (file?.entries == null)
                    return;

                foreach (var entry in file.entries)
                    _entries[entry.username] = entry;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Auth] credentials.json 로드 실패: {ex.Message}");
            }
        }

        private void SaveToDisk()
        {
            try
            {
                var file = new CredentialFile { entries = new List<CredentialEntry>(_entries.Values) };
                string json = JsonUtility.ToJson(file, true);

                // 임시파일 → 교체 방식: 쓰는 도중 서버가 죽어도 원본 파일은 안전 (단일 파일 손상 리스크 완화)
                string tempPath = _filePath + ".tmp";
                File.WriteAllText(tempPath, json);
                // File.Move(a, b, overwrite:true)는 .NET Standard 2.1엔 없어서 직접 지우고 교체
                if (File.Exists(_filePath))
                    File.Delete(_filePath);

                File.Move(tempPath, _filePath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Auth] credentials.json 저장 실패: {ex.Message}");
            }
        }

        public bool TryGetCredential(string username, out CredentialEntry entry)
            => _entries.TryGetValue(username, out entry);

        // 신규 계정 등록 (최초 접속 = 회원가입)
        public void Register(string username, string password)
        {
            byte[] salt = new byte[SaltSize];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(salt);

            byte[] hash = HashPassword(password, salt, Iterations);

            _entries[username] = new CredentialEntry
            {
                username = username,
                saltBase64 = Convert.ToBase64String(salt),
                hashBase64 = Convert.ToBase64String(hash),
                iterations = Iterations
            };

            SaveToDisk();
        }

        // 기존 계정 비밀번호 검증
        public bool Verify(string username, string password)
        {
            if (!_entries.TryGetValue(username, out var entry))
                return false;
            try
            {
                byte[] salt = Convert.FromBase64String(entry.saltBase64);
                byte[] expectedHash = Convert.FromBase64String(entry.hashBase64);
                byte[] actualHash = HashPassword(password, salt, entry.iterations);

                return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Auth] Verify 실패 (username={username}, 자격증명 항목 손상 가능성): {ex.Message}");
                return false;
            }
        }

        private static byte[] HashPassword(string password, byte[] salt, int iterations)
        {
            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
            return pbkdf2.GetBytes(HashSize);
        }
    }
}