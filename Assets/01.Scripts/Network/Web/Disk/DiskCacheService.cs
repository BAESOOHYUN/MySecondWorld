using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace MySecondWorld.Network.Web
{
    public class DiskCacheService
    {
        private const long LimitBytes = 1_073_741_824L; // 1 GB
        private static readonly SHA256 s_sha256 = SHA256.Create(); // static readonly로 한 번만 생성해서 재사용
        private readonly string _cacheRoot;
        private readonly Dictionary<string, DiskCacheEntry> _manifest = new();
        private readonly string _manifestPath;

        public DiskCacheService()
        {
            _cacheRoot = Path.Combine(Application.persistentDataPath, "WebCache");
            _manifestPath = Path.Combine(_cacheRoot, "manifest.json");
            Directory.CreateDirectory(_cacheRoot);
            LoadManifest();
        }

        // 캐시 히트 시 byte[] 반환, 미스 시 null
        public async UniTask<byte[]> TryGetAsync(string url, CancellationToken ct = default)
        {
            var key = GetKey(url);
            var path = GetPath(key);

            // 매니페스트에 없거나 실제 파일이 없으면 캐시 미스
            if (!_manifest.TryGetValue(key, out var entry) || !File.Exists(path))
                return null;
            // 접근 시각 갱신 — LRU 삭제 순서 계산에 사용
            // DateTime.UtcNow.Ticks는 1/10000 밀리초 단위 숫자
            entry.LastAccessTick = DateTime.UtcNow.Ticks;
            await SaveManifestAsync();

            // UniTask.RunOnThreadPool: 파일 읽기는 블로킹 I/O → 스레드풀에서 실행해 메인 스레드 보호
            return await UniTask.RunOnThreadPool(() => File.ReadAllBytes(path), cancellationToken: ct);
        }

        public async UniTask SaveAsync(string url, byte[] bytes, string etag = null, CancellationToken ct = default)
        {
            var key = GetKey(url);
            var path = GetPath(key);
            // 파일 쓰기도 블로킹 I/O → 스레드풀에서 실행
            await UniTask.RunOnThreadPool(() => File.WriteAllBytes(path, bytes), cancellationToken: ct);
            // 매니페스트에 항목 추가/갱신
            _manifest[key] = new DiskCacheEntry
            {
                Url = url,
                ETag = etag,
                SizeBytes = bytes.Length,
                LastAccessTick = DateTime.UtcNow.Ticks
            };
            await SaveManifestAsync();
            // 저장 후 용량 초과 여부 확인
            await CleanupIfOverLimitAsync(ct);
        }

        // HEAD 요청으로 서버의 현재 ETag만 확인 (파일 본문 다운로드 없음)
        public async UniTask<string> GetServerETagAsync(string url, CancellationToken ct = default)
        {
            using var req = UnityWebRequest.Head(url);
            await req.SendWebRequest().ToUniTask(cancellationToken: ct);
            return req.GetResponseHeader("ETag");
        }

        // 로컬에 저장된 ETag 반환 — 서버 ETag와 비교해 변경 여부 판단에 사용
        public string GetCachedETag(string url)
        {
            return _manifest.TryGetValue(GetKey(url), out var entry) ? entry.ETag : null;
        }

        public async UniTask CleanupIfOverLimitAsync(CancellationToken ct)
        { 
            // 전체 캐시 용량 합산
            long total = 0;
            foreach (var entry in _manifest.Values)
                total += entry.SizeBytes;

            if (total <= LimitBytes) return;

            // LastAccessTick 오름차순 정렬 → 가장 오래된 항목부터 삭제
            var sorted = new List<DiskCacheEntry>(_manifest.Values);
            sorted.Sort((a, b) => a.LastAccessTick.CompareTo(b.LastAccessTick));

            foreach (var entry in sorted)
            {
                if (total <= LimitBytes) break;

                var key = GetKey(entry.Url);
                var path = GetPath(key);

                if (File.Exists(path)) // 파일이 없는데 Delete 하면 예외가 터지므로 File.Exists 체크
                    await UniTask.RunOnThreadPool(() => File.Delete(path), cancellationToken: ct); // 디스크에서 .bin 파일 삭제

                // 삭제한 파일 크기만큼 합산에서 뺌
                // total = 1.1 GB,  entry.SizeBytes = 200 MB
                // 삭제 후 → total = 0.9 GB → LimitBytes(1GB) 이하 → 삭제 루프 종료
                total -= entry.SizeBytes;
                // Dictionary에서도 삭제
                _manifest.Remove(key);
            }
            await SaveManifestAsync();
        }

        // json → List로 읽어서 Dictionary로 변환 (런타임 조회 O(1))
        // 디스크 (manifest.json)     ──LoadManifest()──▶    메모리 (_manifest Dictionary)
        private void LoadManifest()
        {
            if (!File.Exists(_manifestPath)) return;
            var json = File.ReadAllText(_manifestPath); // 디스크에서 json 문자열로 읽기
            var data = JsonUtility.FromJson<DiskCacheManifest>(json); // json → DiskCacheManifest(List)
            if (data == null) return;
            foreach (var entry in data.Entries) // List를 순회하면서
                _manifest[GetKey(entry.Url)] = entry; // Dictionary<string, DiskCacheEntry>에 추가
        }

        // Dictionary → List로 변환해서 json 저장 (JsonUtility는 Dictionary 미지원)
        // 메모리 (_manifest Dictionary) ──SaveManifest()──▶  디스크 (manifest.json)
        private async UniTask SaveManifestAsync()
        {
            var data = new DiskCacheManifest();
            // _manifest.Values가 Dictionary의 값들만 꺼내주는 컬렉션.
            // AddRange가 이걸 data.Entries(List)에 통째로 추가
            // 키는 버리고 값만 List에 넣는 게 변환 과정
            data.Entries.AddRange(_manifest.Values);
            var json = JsonUtility.ToJson(data);
            await UniTask.RunOnThreadPool(() => File.WriteAllText(_manifestPath, json));
        }

        // URL → 특수문자 없는 64자리 SHA256 해시 → 파일명으로 안전하게 사용
        private static string GetKey(string url)
        {
            // 1. Encoding.UTF8.GetBytes(url) : URL 문자열 → byte 배열 [104, 116, 116, 112, 115, ...]
            // 2. SHA256.Create().ComputeHash(...) : byte 배열 → 32바이트 해시 [42, 17, 255, 8, ...] SHA256 비트 ÷ 8 = 32 바이트
            var hash = s_sha256.ComputeHash(Encoding.UTF8.GetBytes(url));
            // 3. BitConverter.ToString(hash) : 각 바이트를 16진수 두 글자로 변환, 하이픈으로 연결 "2A-11-FF-08-..."
            // 4. .Replace("-", "").ToLowerInvariant() : 하이픈 제거 + 소문자 "2a11ff08..."  // 64자리 고정 문자열
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        private string GetPath(string key) => Path.Combine(_cacheRoot, key + ".bin");
    }
}
