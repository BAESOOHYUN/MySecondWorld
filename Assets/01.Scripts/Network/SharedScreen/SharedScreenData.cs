namespace MySecondWorld.Network
{
    /// <summary>
    /// SharedScreenController가 현재 공유 중인 파일 정보를 관리할 때 쓸 데이터 구조
    /// 네트워크 전송용이 아니라 '로컬 상태 추적용'
    /// </summary>
    public struct SharedFileInfo
    {
        public string fileName;
        public FileType fileType;
        public int totalPageCount;
        public int originalSize; // byte[] 길이 (파일 크기)

        // 정적 팩토리 메서드로 생성 시점에 초기화
        public static SharedFileInfo CreateFromPDF(string fileName, byte[] fileBytes, int pageCount)
        {
            return new SharedFileInfo
            {
                fileName = fileName,
                fileType = FileType.PDF,
                totalPageCount = pageCount,
                originalSize = fileBytes.Length
            };
        }

        public static SharedFileInfo CreateFromImage(string fileName, byte[] fileBytes)
        {
            return new SharedFileInfo
            {
                fileName = fileName,
                fileType = FileType.Image,
                totalPageCount = 1,
                originalSize = fileBytes.Length
            };
        }
    }

    public static class SharedScreenConstants
    {
        public const int ChunkSize = 12000;
        public const int MaxFileSize = 50 * 1024 * 1024; // 서버가 new byte[]로 잡는 상한 (EC2 t3.small 2GB 기준, 필요하면 조정)
    }
}