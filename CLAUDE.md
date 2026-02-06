# GardenLogic 프로젝트 가이드

## 프로젝트 정보
- **언어**: C#
- **플랫폼**: Unity 6 2D 프로젝트
- **응답 언어**: 한국어

---

## 코드 컨벤션

### 네이밍 규칙

| 대상 | 규칙 | 예시 |
|------|------|------|
| 클래스, 인터페이스, 메소드 | PascalCase | `GameManager`, `PlayerController` |
| 인터페이스 | I 접두사 + 형용사 | `IInteractable`, `IDamageable` |
| private 필드 | _camelCase | `_playerHealth`, `_moveSpeed` |
| [SerializeField] private 필드 | camelCase (언더스코어 없음) | `moveSpeed`, `targetObject` |
| public 필드 | camelCase | `currentHealth` |
| public static 필드 | PascalCase | `Instance`, `MaxHealth` |
| private static 필드 | camelCase | `instance`, `maxCount` |
| 프로퍼티 | PascalCase | `Health`, `IsAlive` |
| 열거형 (enum) 이름 | PascalCase (영어) | `ObjectType`, `PlantState` |
| 열거형 (enum) 값 | **한글로 작성** | `ObjectType.길`, `PlantState.싱싱함` |
| 클래스 멤버변수 | 영어만 사용 (한글 금지) | `_currentState`, `moveSpeed` |

### 코드 스타일

#### 중괄호
- 모든 `if`, `for`, `while`, `foreach` 등에 중괄호 필수 사용
- 중괄호는 **새 줄에서 시작** (Allman 스타일)

```csharp
if (condition)
{
    DoSomething();
}
```

#### 접근 제한자
- **생략하지 않고 명시적으로 작성**

```csharp
private int _count;      // O
int _count;              // X (생략 금지)
```

### Inspector 어트리뷰트 규칙

#### Header와 Tooltip
- `[SerializeField]` 또는 `public` 필드는 Inspector에서 구분되도록 **한글로** `[Header]`와 `[Tooltip]` 작성
- `[Header]`는 카테고리별로 묶어서 하나만 작성
- `[Tooltip]`은 필드의 용도를 설명

```csharp
[Header("이동 설정")]
[SerializeField, Tooltip("캐릭터 이동 속도")]
private float moveSpeed = 5f;

[SerializeField, Tooltip("최대 이동 속도")]
private float maxSpeed = 10f;

[Header("참조")]
[SerializeField, Tooltip("타겟 트랜스폼")]
private Transform targetTransform;
```

### 필드 정렬 순서

```csharp
public class ExampleClass : MonoBehaviour
{
    #region SerializeField 필드

    [Header("설정")]
    [SerializeField, Tooltip("이동 속도")]
    private float moveSpeed;

    [SerializeField, Tooltip("대상 오브젝트")]
    private GameObject targetObject;

    #endregion

    #region Private 필드

    private int _currentIndex;
    private bool _isActive;

    #endregion

    #region Public 필드 및 프로퍼티

    public int CurrentHealth { get; private set; }
    public bool IsAlive => CurrentHealth > 0;

    #endregion
}
```

### 주석 규칙

#### 함수 주석
- `<summary>` 태그를 사용하여 **한글로 작성**
- Unity 고유 메소드 (`Awake`, `Start`, `Update`, `OnEnable` 등)는 주석 **작성하지 않음**

```csharp
/// <summary>
/// 플레이어에게 데미지를 적용하고 체력을 감소시킵니다.
/// </summary>
/// <param name="damage">적용할 데미지 양</param>
public void TakeDamage(int damage)
{
    _currentHealth -= damage;
}

// Unity 메소드는 주석 없이
private void Start()
{
    Initialize();
}
```

### Region 사용
- 기능별로 `#region` 지시문을 사용하여 구분
- **한글로 작성**

```csharp
#region 초기화

private void Initialize()
{
    // ...
}

#endregion

#region 이동 관련

private void Move()
{
    // ...
}

#endregion
```

---

## 코드 템플릿 예시

```csharp
using UnityEngine;

namespace GardenLogic
{
    /// <summary>
    /// 클래스에 대한 설명을 작성합니다.
    /// </summary>
    public class ExampleClass : MonoBehaviour
    {
        #region SerializeField 필드

        [Header("이동 설정")]
        [SerializeField, Tooltip("캐릭터 이동 속도")]
        private float moveSpeed = 5f;

        [Header("참조")]
        [SerializeField, Tooltip("이동 목표 트랜스폼")]
        private Transform targetTransform;

        #endregion

        #region Private 필드

        private bool _isInitialized;
        private int _currentScore;

        #endregion

        #region Public 필드 및 프로퍼티

        public int Score => _currentScore;
        public bool IsReady { get; private set; }

        #endregion

        #region Unity 메소드

        private void Awake()
        {
            _isInitialized = false;
        }

        private void Start()
        {
            Initialize();
        }

        private void Update()
        {
            if (_isInitialized)
            {
                UpdateMovement();
            }
        }

        #endregion

        #region 초기화

        /// <summary>
        /// 컴포넌트를 초기화합니다.
        /// </summary>
        private void Initialize()
        {
            _isInitialized = true;
            IsReady = true;
        }

        #endregion

        #region 이동 관련

        /// <summary>
        /// 매 프레임 이동을 처리합니다.
        /// </summary>
        private void UpdateMovement()
        {
            if (targetTransform != null)
            {
                Vector3 direction = (targetTransform.position - transform.position).normalized;
                transform.position += direction * moveSpeed * Time.deltaTime;
            }
        }

        #endregion
    }
}
```