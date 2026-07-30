using System;

namespace MySecondWorld.Network.Web
{
    [Serializable]
    public class DiskCacheEntry
    {
        public string Url;
        public string ETag;
        public long SizeBytes;
        public long LastAccessTick;
    }
}

// ETag: 서버가 파일에 붙여주는 버전 식별자. 파일 내용이 바뀌면 ETag도 바뀜
// 1. 처음 다운로드 → ETag "abc123" 저장
// 2. 나중에 같은 URL 재요청 시 → 서버에 HEAD 요청
// 3. 서버 ETag가 여전히 "abc123" → 파일 안 바뀜 → 디스크 캐시 그대로 사용
// 4. 서버 ETag가 "xyz789" → 파일 바뀜 → 재다운로드

// LastAccessTick: 마지막으로 이 캐시 항목에 접근한 시각
// 용량 초과 시 가장 오래된 것부터 삭제하는 LRU에 쓰임. 숫자가 작을수록 오래됨.