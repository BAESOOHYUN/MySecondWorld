using UnityEditor;

namespace MySecondWorld.Editor
{
    // AssetDatabase.CreateFolder 기반 폴더 보장 유틸.
    // CharacterPresetImporter / ModularCharacterPartExtractor / ThumbnailBakeHelper 3곳에
    // 흩어져 있던 동일 로직(없으면 상위부터 재귀 생성)을 하나로 합침.
    public static class EditorFolderUtils
    {
        // 재귀함수로 폴더경로 검색 후 없으면 생성
        public static void EnsureFolder(string path)
        {
            // System.IO.Directory.CreateDirectory()로 직접 만들면 메타파일이 없는 "고스트 폴더"가 생겨서 문제가 됨.
            // Unity는 .meta 파일과 GUID로 에셋을 추적하기 때문에 AssetDatabase를 통해 만들어야 GUID가 정상 생성
            if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path))
                return;
            // GetDirectoryName이 윈도우에서는 백슬래시(\)를 쓰는 경로를 반환할 수 있어서 .Replace("\\", "/")로 Unity가 쓰는 슬래시 형식으로 통일
            var parent = System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
            // GetFileName "경로의 마지막 세그먼트"를 반환하는 함수
            var folderName = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}