# Unity Project — Claude Code 공통 규칙

## 1. 프로젝트 개요
- **엔진**: Unity 6000.x (또는 2022.3+ LTS)
- **렌더 파이프라인**: URP (Universal Render Pipeline)
- **언어**: C# (.NET Standard 2.1)
- **에디터 제어**: unity-cli (터미널에서 Unity 에디터를 직접 제어하는 CLI 도구)
- **아키텍처**: MonoBehaviour + ScriptableObject 기반

**유니티 에디터 제어가 필요할 시, 사용자에게 우선적으로 에디터 제어를 요청한다. UnityMCP 없음. unity-cli 사용.**
## 2. unity-cli 제어 방법
unity-cli로 Unity Editor를 제어한다 (UnityMCP 없음, 바이너리 하나, 직접 HTTP POST).
상세 명령어와 활용 가이드는 `.claude/skills/shared/unity-cli-reference.md` 참조.

## 3. 절대 수정 금지 파일

아래 파일은 어떤 에이전트도 수정하지 않는다.
PreToolUse 훅이 물리적으로 차단하지만, 애초에 시도하지 말 것.
훅은 마지막 방어선이고, 이 규칙은 첫 번째 방어선이다.

| 파일 패턴 | 이유 |
|----------|------|
| `*.meta` | Unity 에셋 참조 ID가 들어있음. 수정하면 모든 참조가 깨짐 |
| `*.unity` | Scene 파일. 바이너리 참조가 포함됨. 텍스트 수정 후에는 반드시 `unity-cli reserialize` 사용 |
| `ProjectSettings/*` | Unity 에디터 설정. CLI의 `exec`로 간접 수정 가능 |
| `Packages/manifest.json` | 패키지 의존성. 직접 수정하면 의존성 그래프가 깨질 수 있음 |

## 4. 전체 자동검사 흐름

```
지시 내리기 → 매뉴얼 자동 전달 → AI 작업 수행 → 파일 자동 기록 → 오류 자동 검사 → 셀프체크 리마인더 → AI 바로 수정
```

상세 7단계:
1. **지시 내리기** — 사용자가 요청을 입력한다
2. **매뉴얼 자동 전달** — `UserPromptSubmit` 훅이 키워드/의도/경로를 분석하여 관련 스킬의 INDEX.md를 안내한다
3. **AI 작업 수행** — 에이전트가 스킬을 참고하여 코드를 생성/수정한다
4. **파일 자동 기록** — `PostToolUse` 훅이 매 파일 수정마다 `.claude/memory/changelog.md`에 자동 기록한다
5. **오류 자동 검사** — `Stop` 훅이 `unity-cli console --filter error`로 컴파일 에러를 체크한다
6. **셀프체크 리마인더** — `Stop` 훅이 체크 질문을 AI에게 주입한다 ("null 체크 했나?", "이벤트 해제 했나?" 등)
7. **AI 바로 수정** — QA 판정에 따라 Dev가 즉시 수정하거나, Planning이 재설계한다

## 5. 스킬 로딩 규칙

### 구조
각 에이전트별 스킬은 `.claude/skills/{에이전트명}/` 에 위치한다.
```
.claude/skills/
├── planning/   ← Planning 에이전트 전용
├── dev/        ← Dev 에이전트 전용
├── qa/         ← QA 에이전트 전용
└── recorder/   ← Recorder 에이전트 전용
```
### 로딩 절차 (토큰 절약의 핵심)
1. **항상 INDEX.md를 먼저 읽는다** — 목차 + 라우팅 규칙표가 들어있다
2. **INDEX.md의 라우팅 규칙에 따라 필요한 챕터만 로드한다** — 전체를 한꺼번에 읽지 않는다
3. **라우팅 규칙 4가지**: 키워드 감지, 의도 패턴, 파일 경로, 코드 패턴

### 스킬은 살아있는 문서
- 새 패턴이나 규칙을 사용할 때마다 해당 챕터에 추가한다
- 프로젝트가 성장하면 스킬도 함께 성장한다

## 6. 비용 절약 규칙

| 규칙 | 이유 |
|------|------|
| 모든 에이전트는 Sonnet 4.6 사용 | Opus보다 5배 저렴, 스킬 파일이 보완 |
| 기능 하나 끝나면 `/clear` | 컨텍스트 초기화 = 가장 확실한 절약 |
| 맥락 유지 필요 시 `/compact` | 대화 요약으로 토큰 부피 축소 |
| 구체적 파일 경로 지정 | "코드 개선해줘" ❌ → "Assets/Scripts/Player/Health.cs의 TakeDamage 수정해줘" ✅ |
| INDEX.md 먼저 → 챕터 선택 로드 | 스킬 전체를 한꺼번에 읽지 않음 |

### 이진 에셋 조회 규칙 (전 에이전트 공통)

**이진 에셋(.fbx, .png, .wav, .mp3, .anim 등)은 절대 Read 툴로 열지 않는다.**
FBX가 수백 KB~수 MB라 토큰을 심각하게 낭비한다.
조회가 필요할 때만 `.claude/skills/shared/binary-asset-reference.md` 로드.

---

## 7. 에이전트 팀 구성 (4 subagents)

| 이름 | 역할 | 도구 | 만드는 문서 |
|------|------|------|-----------|
| **Planning** | 계획 수립, 문서 작성. C# 코드/에셋 작성 안 함. | Read, Write, Glob, Grep | PLAN.md, CONTEXT.md, CHECKLIST.md |
| **Dev** | 코드 작성, unity-cli 검증. 계획에 없는 작업 안 함. | Read, Write, Edit, MultiEdit, Bash, Glob, Grep | 없음 (CHECKLIST 체크박스 업데이트만) |
| **QA** | 코드 리뷰, 보고서 작성. 코드 수정 안 함. | Read, Write, Glob, Grep, Bash(제한) | QA-REPORT.md |
| **Recorder** | Notion 정리. 판단하지 않음, 정리만. | Read, Glob, Grep + Notion MCP | Notion 페이지 |


### 에이전트 간 흐름
```
사용자 요청 → Planning (계획) → 사용자 승인 → Dev (구현) → QA (검수)
                 ↑                                              │
                 └── 설계 변경 필요 시 QA-REPORT와 함께 ────────┘
                                                                │
                                          수정 필요 시 ──→ Dev가 즉시 수정
                                                                │
                                          통과 시 ──→ Recorder (Notion 기록)
```
