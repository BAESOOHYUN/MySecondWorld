using UnityEngine;

public class GraphicsApiChecker : MonoBehaviour
{
    private void Start()
    {
        Debug.Log($"[GraphicsAPI] 현재 사용 중인 API: {SystemInfo.graphicsDeviceType}");
    }
}