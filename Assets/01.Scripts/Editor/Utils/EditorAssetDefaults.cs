using UnityEditor;
using UnityEngine;

namespace MySecondWorld.Editor
{
    // 에디터 창이 열릴 때 "값이 비어있으면 고정 경로에서 기본 에셋을 채워 넣는" 반복 패턴을 통합.
    public static class EditorAssetDefaults
    {
        public const string AvatarPartKeyTablePath = "Assets/06.ScriptableObject/AvatarPartKeyTableSO.asset";

        public static T LoadIfNull<T>(T current, string path) where T : Object
        {
            return current != null ? current : AssetDatabase.LoadAssetAtPath<T>(path);
        }

        // Object 타입 값을 T 타입 변수에 그냥 대입하면 컴파일 에러남. (T)로 명시적 다운캐스트를 해줘야 대입이 가능
        // (typeof(T))는 Unity가 Inspector UI를 그릴 때 "이 칸에는 T 타입만 드래그 가능하게 필터링해라"라고 알려주는 용도
        public static T DrawWithDefault<T>(string label, T current, string defaultPath) where T : Object
        {
            current = LoadIfNull(current, defaultPath);
            return (T)EditorGUILayout.ObjectField(label, current, typeof(T), false);
        }

        public static T DrawWithDefault<T>(GUIContent label, T current, string defaultPath) where T : Object
        {
            current = LoadIfNull(current, defaultPath);
            return (T)EditorGUILayout.ObjectField(label, current, typeof(T), false);
        }
    }
}
