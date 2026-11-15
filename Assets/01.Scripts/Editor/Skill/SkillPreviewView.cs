using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace MySecondWorld.Combat.EditorTools
{
    // 스킬 미리보기: 타임라인을 시간 순서대로 재생해 본다. 실제 게임 로직(데미지·서버)은 돌리지 않는다.
    //  - VFX 클립: 격리된 미리보기 장면에 프리팹을 만들고 ParticleSystem.Simulate로 그 시각의 모습을 그린다.
    //  - SFX 클립: 에디터 미리듣기로 소리를 낸다(재생 중에만, 슬라이더로 움직일 때는 안 낸다).
    //  - 효과/애니 클립: 장면에 그릴 게 없어 "몇 초에 발동"만 글자로 알려 준다.
    // 시전자는 원점에서 +Z를 보고, 폭심지(Epicenter)는 앞쪽 EpicenterDistance 지점으로 가정한다.
    internal class SkillPreviewView : VisualElement
    {
        private const float DefaultEnemyDistance = 2f;
        private float _enemyDistance = DefaultEnemyDistance; // 적(=폭심지)이 시전자 앞 몇 m에 있나
        private GameObject _enemy;      // 적 더미(빨간 큐브)
        private GameObject _disc;       // 폭심지 표시 원판
        private readonly List<Material> _materials = new(); // 색을 입히려고 만든 머티리얼(정리용)
        private const float ViewHeight = 230f;

        private readonly SkillDefinition _skill;
        private readonly PreviewRenderUtility _pru;
        private readonly Dictionary<VfxClip, GameObject> _vfx = new();
        private readonly List<Object> _scenery = new();
        private readonly IMGUIContainer _view;
        private readonly Slider _slider;
        private readonly Label _timeLabel;
        private readonly Label _events;
        private readonly Button _playButton;

        private readonly float _end;
        private float _t;
        private bool _playing;
        private double _lastTime;

        public SkillPreviewView(SkillDefinition skill)
        {
            _skill = skill;
            _end = Mathf.Max(0.5f, ComputeEnd());

            style.marginTop = 12;
            style.paddingLeft = 8;
            style.paddingRight = 8;
            style.paddingTop = 6;
            style.paddingBottom = 8;
            style.backgroundColor = SkillEditorWindow.PanelColor;

            Add(new Label("미리보기 (PreviewRenderUtility)") { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 13 } });
            var how = new Label("실제 씬이 아니라 PreviewRenderUtility가 만든 보이지 않는 임시 씬을 카메라로 찍어 보여 줍니다. 창을 닫으면 모두 지워집니다.");
            how.style.fontSize = 11;
            how.style.opacity = 0.6f;
            how.style.whiteSpace = WhiteSpace.Normal;
            Add(how);
            var hint = new Label("VFX와 사운드만 재생됩니다. 데미지·타겟팅은 실제 게임에서만 동작합니다. " +
                                 "파란 캡슐이 시전자(흰 코 큐브가 앞), 빨간 큐브가 적, 바닥의 노란 원이 폭심지(적 위치)입니다. " +
                                 "'적 거리'로 적을 움직여 Epicenter 이펙트가 어떻게 따라오는지 보세요. 화면을 끌면 회전, 휠로 확대합니다.");
            hint.style.fontSize = 11;
            hint.style.opacity = 0.6f;
            hint.style.whiteSpace = WhiteSpace.Normal;
            Add(hint);

            _pru = new PreviewRenderUtility();
            BuildScene();

            _view = new IMGUIContainer(DrawView);
            _view.style.height = ViewHeight;
            _view.style.marginTop = 6;
            Add(_view);

            var controls = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = 4 } };
            _playButton = new Button(TogglePlay) { text = "▶ 재생" };
            controls.Add(_playButton);
            _slider = new Slider(0f, _end) { value = 0f };
            _slider.style.flexGrow = 1;
            _slider.style.marginLeft = 8;
            _slider.RegisterValueChangedCallback(e => Scrub(e.newValue));
            controls.Add(_slider);
            _timeLabel = new Label();
            _timeLabel.style.width = 90;
            _timeLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            controls.Add(_timeLabel);
            Add(controls);

            // 적 거리: 폭심지(Epicenter)는 적이 서 있는 자리다. 멀고 가까움에 따라 Epicenter 이펙트가 따라간다.
            var enemyRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = 4 } };
            enemyRow.Add(new Label("적 거리 (m)") { style = { width = 90 } });
            var enemySlider = new Slider(0.5f, 6f) { value = _enemyDistance, showInputField = true };
            enemySlider.style.flexGrow = 1;
            enemySlider.RegisterValueChangedCallback(e => MoveEnemy(e.newValue));
            enemyRow.Add(enemySlider);
            Add(enemyRow);

            _events = new Label();
            _events.style.fontSize = 11;
            _events.style.whiteSpace = WhiteSpace.Normal;
            _events.style.marginTop = 4;
            Add(_events);

            Apply();
            RegisterCallback<DetachFromPanelEvent>(_ => Cleanup());
        }

        // ── 장면 ───────────────────────────────────────────────

        private float ComputeEnd()
        {
            float end = 0f;
            foreach (var clip in _skill.Timeline)
                if (clip != null) end = Mathf.Max(end, clip.StartTime + clip.Duration);
            return end;
        }

        private void BuildScene()
        {
            var cam = _pru.camera;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.17f, 0.2f);
            cam.fieldOfView = 35f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            PlaceCamera();

            _pru.lights[0].intensity = 1.2f;
            _pru.lights[0].transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // 기준이 되는 바닥과 시전자(캡슐). 기본 머티리얼이 그대로 쓰여 단색으로 보인다.
            AddScenery(GameObject.CreatePrimitive(PrimitiveType.Plane), Vector3.zero, new Vector3(1.2f, 1f, 1.2f));
            var caster = AddScenery(GameObject.CreatePrimitive(PrimitiveType.Capsule), new Vector3(0f, 1f, 0f), new Vector3(0.7f, 1f, 0.7f));
            Tint(caster, new Color(0.35f, 0.6f, 0.95f)); // 시전자 = 파랑
            // 코: 캡슐이 어디를 보는지(+Z) 알려 주는 작은 큐브
            var nose = AddScenery(GameObject.CreatePrimitive(PrimitiveType.Cube), new Vector3(0f, 1.35f, 0.42f), new Vector3(0.22f, 0.22f, 0.4f));
            Tint(nose, new Color(0.9f, 0.9f, 0.9f));
            // 적 더미(빨강)와 폭심지 원판(노랑). 위치는 PlaceEnemy가 _enemyDistance로 정한다.
            _enemy = AddScenery(GameObject.CreatePrimitive(PrimitiveType.Cube), Vector3.zero, new Vector3(0.7f, 1.7f, 0.7f));
            Tint(_enemy, new Color(0.9f, 0.3f, 0.3f));
            _disc = AddScenery(GameObject.CreatePrimitive(PrimitiveType.Cylinder), Vector3.zero, new Vector3(0.9f, 0.01f, 0.9f));
            Tint(_disc, new Color(1f, 0.85f, 0.2f));
            PlaceEnemy();
        }

        // 적을 시전자 앞 _enemyDistance 지점에 세우고, 폭심지 원판도 같은 자리 바닥에 둔다.
        private void PlaceEnemy()
        {
            _enemy.transform.position = new Vector3(0f, 0.85f, _enemyDistance);
            _disc.transform.position = new Vector3(0f, 0.02f, _enemyDistance);
        }

        // 기본 머티리얼을 복사해 색만 바꾼다. 못 하면(셰이더 차이) 조용히 기본색으로 둔다.
        private void Tint(GameObject go, Color color)
        {
            if (!go.TryGetComponent(out Renderer renderer) || renderer.sharedMaterial == null) return;
            var material = new Material(renderer.sharedMaterial) { color = color };
            renderer.sharedMaterial = material;
            _materials.Add(material);
        }

        // 카메라는 바라보는 지점(CameraTarget)을 중심으로 회전·확대한다.
        private static readonly Vector3 CameraTarget = new(0f, 1f, 1.2f);
        private float _yaw = 0f;      // 좌우 회전
        private float _pitch = 25f;   // 위아래 각도
        private float _distance = 7.5f;

        private void PlaceCamera()
        {
            var rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            var cam = _pru.camera;
            cam.transform.position = CameraTarget + rotation * new Vector3(0f, 0f, -_distance);
            cam.transform.LookAt(CameraTarget);
        }

        private GameObject AddScenery(GameObject go, Vector3 position, Vector3 scale)
        {
            go.transform.position = position;
            go.transform.localScale = scale;
            if (go.TryGetComponent(out Collider collider)) Object.DestroyImmediate(collider);
            _pru.AddSingleGO(go);
            _scenery.Add(go);
            return go;
        }

        private void DrawView()
        {
            var e = Event.current;
            var rect = _view.contentRect;
            var local = new Rect(0, 0, rect.width, rect.height);

            // 마우스로 끌면 회전, 휠로 확대/축소
            if (local.Contains(e.mousePosition))
            {
                if (e.type == EventType.MouseDrag)
                {
                    _yaw += e.delta.x * 0.5f;
                    _pitch = Mathf.Clamp(_pitch + e.delta.y * 0.5f, 3f, 80f);
                    PlaceCamera();
                    e.Use();
                    _view.MarkDirtyRepaint();
                }
                else if (e.type == EventType.ScrollWheel)
                {
                    _distance = Mathf.Clamp(_distance + e.delta.y * 0.3f, 2f, 20f);
                    PlaceCamera();
                    e.Use();
                    _view.MarkDirtyRepaint();
                }
            }

            if (e.type != EventType.Repaint) return;
            if (rect.width < 4f || rect.height < 4f) return;

            _pru.BeginPreview(new Rect(0, 0, rect.width, rect.height), GUIStyle.none);
            _pru.camera.Render();
            var tex = _pru.EndPreview();
            GUI.DrawTexture(new Rect(0, 0, rect.width, rect.height), tex, ScaleMode.StretchToFill, false);
        }

        // ── 재생 ───────────────────────────────────────────────

        private void TogglePlay()
        {
            if (_playing) { Stop(); return; }

            if (_t >= _end) _t = 0f;
            if (_t <= 0f) FireAudio(-1f, 0f); // 0초에 시작하는 사운드
            _playing = true;
            _playButton.text = "■ 정지";
            _lastTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
        }

        private void Stop()
        {
            _playing = false;
            _playButton.text = "▶ 재생";
            EditorApplication.update -= Tick;
            AssetPaletteView.StopPreview();
        }

        private void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            float prev = _t;
            _t += (float)(now - _lastTime);
            _lastTime = now;

            bool finished = _t >= _end;
            if (finished) _t = _end;

            FireAudio(prev, _t);
            _slider.SetValueWithoutNotify(_t);
            Apply();
            if (finished) Stop();
        }

        // 슬라이더를 직접 움직일 때: 소리는 내지 않고 그 시각의 장면만 보여 준다.
        private void Scrub(float t)
        {
            if (_playing) Stop();
            AssetPaletteView.StopPreview();
            _t = t;
            Apply();
        }

        private void FireAudio(float from, float to)
        {
            foreach (var clip in _skill.Timeline.OfType<SfxClip>())
                if (clip.Sound != null && from < clip.StartTime && clip.StartTime <= to)
                    AssetPaletteView.PlayPreview(clip.Sound);
        }

        // 현재 시각 _t의 모습을 만든다: 살아 있을 구간의 VFX는 만들고 시뮬레이션, 아닌 것은 지운다.
        private void Apply()
        {
            foreach (var clip in _skill.Timeline.OfType<VfxClip>())
            {
                bool alive = clip.Prefab != null && _t >= clip.StartTime && _t < clip.StartTime + clip.Duration;
                _vfx.TryGetValue(clip, out var instance);

                if (!alive)
                {
                    if (instance != null) Object.DestroyImmediate(instance);
                    _vfx.Remove(clip);
                    continue;
                }

                if (instance == null)
                {
                    instance = Object.Instantiate(clip.Prefab);
                    instance.transform.SetPositionAndRotation(ResolvePosition(clip), Quaternion.identity);
                    _pru.AddSingleGO(instance);
                    _vfx[clip] = instance;
                }

                float local = _t - clip.StartTime;
                foreach (var ps in instance.GetComponentsInChildren<ParticleSystem>())
                    ps.Simulate(local, false, true, true); // 각 파티클을 그 시각으로 직접 맞춘다
            }

            _timeLabel.text = $"{_t:0.00} / {_end:0.00}초";
            _slider.SetValueWithoutNotify(_t);
            _events.text = BuildEventText();
            _view.MarkDirtyRepaint();
        }

        private Vector3 ResolvePosition(VfxClip clip)
        {
            Vector3 basePos = clip.Anchor == VfxAnchor.Epicenter ? new Vector3(0f, 0f, _enemyDistance) : Vector3.zero;
            return basePos + clip.Offset;
        }

        // 적 거리를 바꾸면 이미 만들어 둔 이펙트의 위치도 달라져야 하므로 전부 지우고 현재 시각으로 다시 만든다.
        private void MoveEnemy(float distance)
        {
            _enemyDistance = distance;
            PlaceEnemy();
            foreach (var go in _vfx.Values) if (go != null) Object.DestroyImmediate(go);
            _vfx.Clear();
            Apply();
        }

        private string BuildEventText()
        {
            var fired = _skill.Timeline
                .Where(c => c is EffectClip or AnimClip && c.StartTime <= _t)
                .OrderBy(c => c.StartTime)
                .Select(c => $"{c.StartTime:0.00}초 {(c is EffectClip ? "효과 발동" : "애니 재생")}");
            string text = string.Join("  ·  ", fired);
            return string.IsNullOrEmpty(text) ? "(아직 발동한 효과/애니 없음)" : text;
        }

        private void Cleanup()
        {
            if (_playing) Stop();
            else AssetPaletteView.StopPreview();
            foreach (var go in _vfx.Values) if (go != null) Object.DestroyImmediate(go);
            _vfx.Clear();
            foreach (var go in _scenery) if (go != null) Object.DestroyImmediate(go);
            _scenery.Clear();
            foreach (var material in _materials) if (material != null) Object.DestroyImmediate(material);
            _materials.Clear();
            _pru.Cleanup();
        }
    }
}
