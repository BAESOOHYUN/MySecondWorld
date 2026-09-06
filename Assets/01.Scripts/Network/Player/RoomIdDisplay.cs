using System;
using Mirror;
using UnityEngine;

namespace MySecondWorld.Network
{
    /// <summary>
    /// [시연/디버그용] 내가 지금 어느 방(match)에 속해 있는지 화면 좌상단에 표시한다.
    ///
    /// 던전 인스턴싱은 "다른 파티가 안 보인다"가 정상 동작이라, 화면만 봐서는
    /// 제대로 도는 건지 그냥 아무도 없는 건지 구분이 안 된다. 그 증거를 눈에 보이게 하는 용도.
    ///
    /// NetworkMatch.matchId는 서버에서만 읽고 쓸 수 있어(클라에선 항상 Guid.Empty) 클라가 직접 못 본다.
    /// 그래서 서버가 배정 시점에 SyncVar로 내려준다.
    /// </summary>
    public class RoomIdDisplay : NetworkBehaviour
    {
        [Tooltip("끄면 오버레이를 그리지 않는다. 최종 녹화 때 정리용.")]
        [SerializeField] private bool _show = true;

        // 방을 한눈에 구분시키는 게 목적이라 Guid 앞 8자리면 충분하다.
        [SyncVar] private string _roomLabel = "";
        // roomId에서 결정적으로 뽑은 색 — 두 파티가 화면에서 즉시 갈려 보이게 한다.
        [SyncVar] private Color _roomColor = Color.white;

        private GUIStyle _style;

        [Server]
        public void SetRoom(Guid roomId)
        {
            if (roomId == Guid.Empty)
            {
                _roomLabel = "FIELD";
                _roomColor = Color.white;
                return;
            }

            string full = roomId.ToString("N");
            _roomLabel = $"ROOM {full.Substring(0, 8)}";

            // 같은 roomId면 어느 클라에서든 같은 색이 나온다(해시 기반).
            // 채도·명도를 고정해 어두운 던전 배경 위에서도 잘 읽히게 한다.
            float hue = (full.GetHashCode() & 0x7FFFFFFF) % 360 / 360f;
            _roomColor = Color.HSVToRGB(hue, 0.65f, 1f);
        }

        private void OnGUI()
        {
            if (!_show || !isLocalPlayer || string.IsNullOrEmpty(_roomLabel)) return;

            // 해상도가 달라도 비슷하게 보이도록 화면 높이에 비례시킨다.
            int fontSize = Mathf.Max(14, Mathf.RoundToInt(Screen.height * 0.028f));

            _style ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            _style.fontSize = fontSize;

            var rect = new Rect(16f, 12f, Screen.width * 0.5f, fontSize * 1.6f);

            // 배경 위에서 읽히도록 검은 그림자를 한 겹 깔고 그 위에 방 색을 얹는다.
            var shadow = rect;
            shadow.x += 2f;
            shadow.y += 2f;
            _style.normal.textColor = new Color(0f, 0f, 0f, 0.75f);
            GUI.Label(shadow, _roomLabel, _style);

            _style.normal.textColor = _roomColor;
            GUI.Label(rect, _roomLabel, _style);
        }
    }
}
