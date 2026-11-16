using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MySecondWorld.Combat.EditorTools
{
    // 스킬 하나의 타임라인 — 시간 눈금 + 클립 종류별 트랙. 블록을 좌우로 끌어 시작 시간을 바꾼다.
    // 데이터는 SkillDefinition._timeline(SerializeReference 리스트)에 SerializedProperty로 읽고 쓴다.
    // 화면을 새로 그리는 일은 호출한 창이 맡는다(onChanged로 알려준다).
    public class SkillTimelineView : VisualElement
    {
        private const float PxPerSecond = 180f;   // 1초가 차지하는 가로 픽셀
        private const float Snap = 0.05f;         // 시작 시간 자석 간격(초)
        private const float TrackHeight = 28f;
        private const float RulerHeight = 20f;
        private const float LabelWidth = 64f;
        private const float MinClipWidth = 34f;   // 시작 시간 숫자(0.00)가 잘리지 않을 최소 폭
        private const float TickInterval = 0.5f;  // 눈금 간격(초)

        private static readonly (Type type, string label, Color color)[] Tracks =
        {
            (typeof(EffectClip), "효과", new Color(0.85f, 0.38f, 0.38f)),
            (typeof(VfxClip), "VFX", new Color(0.65f, 0.45f, 0.90f)),
            (typeof(SfxClip), "SFX", new Color(0.30f, 0.72f, 0.52f)),
            (typeof(AnimClip), "애니", new Color(0.95f, 0.75f, 0.30f)),
        };

        private readonly SerializedObject _so;
        private readonly SkillDefinition _skill;
        private readonly SerializedProperty _list;
        private readonly Action<int> _onChanged;  // 인자: 이어서 선택할 클립 번호(-1 = 선택 없음)
        private readonly int _selected;

        public SkillTimelineView(SerializedObject so, SkillDefinition skill, int selected, Action<int> onChanged)
        {
            _so = so;
            _skill = skill;
            _list = so.FindProperty("_timeline");
            _selected = selected;
            _onChanged = onChanged;

            style.marginTop = 14;
            Add(BuildHeader());
            Add(BuildBoard());
            if (_selected >= 0 && _selected < _skill.Timeline.Count) Add(BuildDetail());
        }

        // ── 머리줄: 제목 + 추가/삭제 ─────────────────────────────

        private VisualElement BuildHeader()
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween } };
            row.Add(new Label("시퀀스 · 타임라인") { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 13 } });

            var buttons = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            buttons.Add(new Button(ShowAddMenu) { text = "+ 클립 추가" });
            var delete = new Button(DeleteSelected) { text = "선택 삭제" };
            delete.SetEnabled(_selected >= 0 && _selected < _skill.Timeline.Count);
            buttons.Add(delete);
            row.Add(buttons);
            return row;
        }

        private void ShowAddMenu()
        {
            var menu = new GenericMenu();
            foreach (var type in TypeCache.GetTypesDerivedFrom<SkillClip>().Where(t => !t.IsAbstract).OrderBy(t => t.Name))
            {
                var captured = type;
                menu.AddItem(new GUIContent(LabelOf(type)), false, () => AddClip(captured));
            }
            menu.ShowAsContext();
        }

        private void AddClip(Type type)
        {
            _so.Update();
            _list.arraySize++;
            int index = _list.arraySize - 1;
            // 배열을 늘리면 마지막 요소가 복사돼 같은 인스턴스를 가리킬 수 있어, 반드시 새 인스턴스로 덮어쓴다.
            _list.GetArrayElementAtIndex(index).managedReferenceValue = Activator.CreateInstance(type);
            _so.ApplyModifiedProperties();
            _onChanged(index);
        }

        private void DeleteSelected()
        {
            if (_selected < 0 || _selected >= _list.arraySize) return;
            _so.Update();
            _list.DeleteArrayElementAtIndex(_selected);
            _so.ApplyModifiedProperties();
            _onChanged(-1);
        }

        // ── 보드: 눈금 + 트랙 ────────────────────────────────────

        private VisualElement BuildBoard()
        {
            float length = Mathf.Max(2f, MaxEnd() + 0.5f);
            float laneWidth = length * PxPerSecond;

            var scroll = new ScrollView(ScrollViewMode.Horizontal);
            scroll.style.marginTop = 6;

            var board = new VisualElement { style = { width = LabelWidth + laneWidth } };
            board.Add(BuildRuler(length, laneWidth));
            foreach (var track in Tracks)
                board.Add(BuildTrack(track.type, track.label, track.color, laneWidth));

            // 알 수 없는 클립 종류(나중에 추가될 클래스)는 맨 아래 "기타" 줄에 모은다.
            if (Enumerable.Range(0, _skill.Timeline.Count).Any(i => IsUnknown(_skill.Timeline[i])))
                board.Add(BuildTrack(null, "기타", new Color(0.6f, 0.6f, 0.6f), laneWidth));

            scroll.Add(board);
            return scroll;
        }

        private static VisualElement BuildRuler(float length, float laneWidth)
        {
            var ruler = new VisualElement { style = { height = RulerHeight, flexDirection = FlexDirection.Row } };
            ruler.Add(new VisualElement { style = { width = LabelWidth } });

            var lane = new VisualElement { style = { width = laneWidth, height = RulerHeight } };
            for (float t = 0f; t <= length + 0.001f; t += TickInterval)
            {
                var tick = new Label($"{t:0.0}") { style = { position = Position.Absolute, left = t * PxPerSecond + 2, fontSize = 10, opacity = 0.6f } };
                lane.Add(tick);
            }
            ruler.Add(lane);
            return ruler;
        }

        private VisualElement BuildTrack(Type type, string label, Color color, float laneWidth)
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, height = TrackHeight, marginTop = 2 } };
            row.Add(new Label(label) { style = { width = LabelWidth, unityTextAlign = TextAnchor.MiddleLeft, color = color, unityFontStyleAndWeight = FontStyle.Bold } });

            var lane = new VisualElement { style = { width = laneWidth, height = TrackHeight, backgroundColor = new Color(1f, 1f, 1f, 0.05f) } };
            for (float t = TickInterval; t * PxPerSecond < laneWidth; t += TickInterval)
                lane.Add(new VisualElement { style = { position = Position.Absolute, left = t * PxPerSecond, top = 0, bottom = 0, width = 1, backgroundColor = new Color(1f, 1f, 1f, 0.08f) } });

            for (int i = 0; i < _skill.Timeline.Count; i++)
            {
                var clip = _skill.Timeline[i];
                if (clip == null) continue;
                bool belongs = type == null ? IsUnknown(clip) : clip.GetType() == type;
                if (belongs) lane.Add(BuildBlock(i, clip, color));
            }
            row.Add(lane);
            return row;
        }

        // 클립 블록 — 클릭하면 선택, 좌우로 끌면 시작 시간 변경.
        private VisualElement BuildBlock(int index, SkillClip clip, Color color)
        {
            var block = new VisualElement();
            block.style.position = Position.Absolute;
            block.style.left = clip.StartTime * PxPerSecond;
            block.style.top = 3;
            block.style.height = TrackHeight - 6;
            block.style.width = Mathf.Max(MinClipWidth, clip.Duration * PxPerSecond);
            block.style.backgroundColor = new Color(color.r, color.g, color.b, 0.85f);
            block.style.borderTopLeftRadius = 3;
            block.style.borderBottomLeftRadius = 3;
            block.style.borderTopRightRadius = 3;
            block.style.borderBottomRightRadius = 3;
            if (index == _selected) SetBorder(block, 2, Color.white);
            block.tooltip = $"{LabelOf(clip.GetType())} @ {clip.StartTime:0.00}s";

            block.Add(new Label($"{clip.StartTime:0.00}") { style = { fontSize = 10, color = Color.black, unityTextAlign = TextAnchor.MiddleCenter, flexGrow = 1 }, pickingMode = PickingMode.Ignore });

            float pressX = 0f, startAtPress = 0f, pending = 0f;
            bool pressed = false, moved = false;

            block.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0) return;
                pressed = true;
                moved = false;
                pressX = e.position.x;
                startAtPress = clip.StartTime;
                block.CapturePointer(e.pointerId);
            });
            block.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!pressed) return;
                float raw = startAtPress + (e.position.x - pressX) / PxPerSecond;
                pending = Mathf.Max(0f, Mathf.Round(raw / Snap) * Snap);
                moved = moved || Mathf.Abs(e.position.x - pressX) > 3f;
                if (moved)
                {
                    block.style.left = pending * PxPerSecond;
                    block.tooltip = $"{pending:0.00}s";
                }
            });
            block.RegisterCallback<PointerUpEvent>(e =>
            {
                if (!pressed) return;
                pressed = false;
                block.ReleasePointer(e.pointerId);
                if (moved) SetStartTime(index, pending);
                _onChanged(index);
            });
            return block;
        }

        private void SetStartTime(int index, float value)
        {
            _so.Update();
            _list.GetArrayElementAtIndex(index).FindPropertyRelative("_startTime").floatValue = value;
            _so.ApplyModifiedProperties();
        }

        // ── 선택한 클립의 상세 ───────────────────────────────────

        private VisualElement BuildDetail()
        {
            var clip = _skill.Timeline[_selected];
            var box = new VisualElement();
            box.style.marginTop = 10;
            box.style.paddingLeft = 8;
            box.style.paddingRight = 8;
            box.style.paddingTop = 6;
            box.style.paddingBottom = 8;
            box.style.backgroundColor = new Color(1f, 1f, 1f, 0.05f);

            box.Add(new Label($"선택한 클립 — {(clip != null ? LabelOf(clip.GetType()) : "(비어 있음)")}")
                { style = { unityFontStyleAndWeight = FontStyle.Bold } });

            var element = _list.GetArrayElementAtIndex(_selected);
            if (clip is EffectClip)
            {
                box.Add(BuildEffectClipEditor(element));
                return box;
            }

            element.isExpanded = true;
            var field = new PropertyField(element, "상세");
            field.Bind(_so);
            box.Add(field);
            return box;
        }

        // 효과 클립 — 시작 시간 + 축2(타겟팅) 슬롯 + 축3(효과) 슬롯. 보관함 카드를 끌어다 놓는다.
        private VisualElement BuildEffectClipEditor(SerializedProperty element)
        {
            var root = new VisualElement();

            var start = new PropertyField(element.FindPropertyRelative("_startTime"), "시작 시간(초)");
            start.style.marginTop = 6;
            start.Bind(_so);
            root.Add(start);

            // 판정 기준 — 마법은 조준 지점, 근접은 이 순간 시전자 몸 앞
            foreach (var (name, label) in new[] { ("_hitBasis", "판정 기준"), ("_hitForward", "앞 거리(m)"), ("_hitRadius", "판정 반경(m)") })
            {
                var f = new PropertyField(element.FindPropertyRelative(name), label);
                f.Bind(_so);
                root.Add(f);
            }

            // 축2 · 타겟팅 (하나)
            var targetProp = element.FindPropertyRelative("_targetSelector");
            var targetSlot = SkillEditorWindow.BuildSlot("축2 · 타겟팅", "누구를 맞히나?",
                SkillEditorWindow.TargetColor, typeof(TargetSelectorSO), out var targetContent, dropped =>
                {
                    _so.Update();
                    targetProp.objectReferenceValue = dropped;
                    _so.ApplyModifiedProperties();
                    _onChanged(_selected);
                });
            if (targetProp.objectReferenceValue is ScriptableObject target)
            {
                targetContent.Add(SkillEditorWindow.BuildCard(target, SkillEditorWindow.TargetColor, () =>
                {
                    _so.Update();
                    targetProp.objectReferenceValue = null;
                    _so.ApplyModifiedProperties();
                    _onChanged(_selected);
                }));
            }
            else targetContent.Add(SkillEditorWindow.EmptyHint());
            root.Add(targetSlot);

            // 축3 · 효과 (여러 개)
            var effectsProp = element.FindPropertyRelative("_effects");
            var effectSlot = SkillEditorWindow.BuildSlot("축3 · 효과", "맞으면 무슨 일이 생기나? (여러 개 가능)",
                SkillEditorWindow.EffectColor, typeof(SkillEffectSO), out var effectContent, dropped =>
                {
                    _so.Update();
                    effectsProp.arraySize++;
                    effectsProp.GetArrayElementAtIndex(effectsProp.arraySize - 1).objectReferenceValue = dropped;
                    _so.ApplyModifiedProperties();
                    _onChanged(_selected);
                });

            bool any = false;
            for (int i = 0; i < effectsProp.arraySize; i++)
            {
                int index = i;
                if (effectsProp.GetArrayElementAtIndex(i).objectReferenceValue is not ScriptableObject effect) continue;
                any = true;
                effectContent.Add(SkillEditorWindow.BuildCard(effect, SkillEditorWindow.EffectColor, () =>
                {
                    _so.Update();
                    effectsProp.GetArrayElementAtIndex(index).objectReferenceValue = null; // 참조 배열은 null로 비운 뒤 삭제해야 칸이 사라진다
                    effectsProp.DeleteArrayElementAtIndex(index);
                    _so.ApplyModifiedProperties();
                    _onChanged(_selected);
                }));
            }
            if (!any) effectContent.Add(SkillEditorWindow.EmptyHint());

            // 같은 종류(예: 데미지)의 효과가 둘 이상이면 효과가 겹쳐 적용되므로 슬롯 맨 위에 경고를 띄운다.
            string conflict = FindExclusiveConflict(effectsProp);
            if (conflict != null)
            {
                var warning = new HelpBox($"'{conflict}' 종류의 효과가 2개 이상이라 겹쳐서 적용됩니다. 하나만 남기세요.", HelpBoxMessageType.Warning);
                warning.style.marginTop = 6;
                effectContent.Insert(0, warning);
            }
            root.Add(effectSlot);

            return root;
        }

        // 효과 목록에서 ExclusiveGroup이 같은 부품이 두 번 나오면 그 그룹 이름을, 없으면 null을 돌려준다.
        private static string FindExclusiveConflict(SerializedProperty effects)
        {
            var seen = new HashSet<string>();
            for (int i = 0; i < effects.arraySize; i++)
            {
                var effect = effects.GetArrayElementAtIndex(i).objectReferenceValue;
                if (effect == null) continue;

                var attr = (SkillPartAttribute)Attribute.GetCustomAttribute(effect.GetType(), typeof(SkillPartAttribute));
                if (attr?.ExclusiveGroup == null) continue;
                if (!seen.Add(attr.ExclusiveGroup)) return attr.ExclusiveGroup;
            }
            return null;
        }

        // ── 도우미 ──────────────────────────────────────────────

        private float MaxEnd()
        {
            float max = 0f;
            for (int i = 0; i < _skill.Timeline.Count; i++)
            {
                var clip = _skill.Timeline[i];
                if (clip != null) max = Mathf.Max(max, clip.StartTime + clip.Duration);
            }
            return max;
        }

        private static bool IsUnknown(SkillClip clip) => clip != null && !Tracks.Any(t => t.type == clip.GetType());

        private static string LabelOf(Type type)
        {
            foreach (var track in Tracks)
                if (track.type == type) return $"{track.label} ({type.Name})";
            return type.Name;
        }

        private static void SetBorder(VisualElement element, float width, Color color)
        {
            element.style.borderLeftWidth = width;
            element.style.borderRightWidth = width;
            element.style.borderTopWidth = width;
            element.style.borderBottomWidth = width;
            element.style.borderLeftColor = color;
            element.style.borderRightColor = color;
            element.style.borderTopColor = color;
            element.style.borderBottomColor = color;
        }
    }
}
