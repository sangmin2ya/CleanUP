using UnityEngine;
using UnityEngine.InputSystem;

namespace CleanUp
{
    /// <summary>
    /// 마우스 입력을 받아 레이캐스트로 청소 명령을 전달하는 컨트롤러입니다.
    /// </summary>
    public class CleaningController : MonoBehaviour
    {
        #region SerializeField 필드

        [Header("청소 설정")]
        [SerializeField, Range(0.01f, 0.5f), Tooltip("브러시 반경 (UV 공간 기준, 0~1)")]
        private float brushRadius = 0.05f;

        [SerializeField, Range(0.01f, 1f), Tooltip("청소 강도")]
        private float cleanStrength = 0.1f;

        [Header("경로 보간")]
        [SerializeField, Range(1, 50), Tooltip("경로 보간 단계 수 (높을수록 부드러움)")]
        private int pathSteps = 20;

        [Header("브러시 텍스처")]
        [SerializeField, Tooltip("브러시 모양 텍스처 (알파 채널 사용, null이면 원형)")]
        private Texture2D brushTexture;

        [Header("각도 검출")]
        [SerializeField, Range(0f, 90f), Tooltip("이 각도 이내면 정상 청소, 초과하면 급감")]
        private float normalAngleThreshold = 45f;

        [Header("감쇠 곡선")]
        [SerializeField, Tooltip("거리에 따른 강도 감쇠 곡선 (0=가장자리, 1=중심)")]
        private AnimationCurve distanceFalloffCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [SerializeField, Tooltip("각도에 따른 강도 감쇠 곡선")]
        private AnimationCurve angleFalloffCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("디버그")]
        [SerializeField, Tooltip("히트 지점 시각화")]
        private bool showHitPoint = true;

        [SerializeField, Tooltip("씬에 배치된 히트 인디케이터 오브젝트")]
        private GameObject hitIndicator;

        #endregion

        #region Private 필드

        private Camera _mainCamera;
        private Vector2 _lastUV;
        private DirtySurface _lastSurface;
        private bool _hasPreviousPosition;
        private Vector3 _lastHitPoint;
        private Vector3 _lastHitNormal;

        #endregion

        #region Unity 메소드

        private void Start()
        {
            _mainCamera = Camera.main;

            if (_mainCamera == null)
            {
                Debug.LogError("[CleaningController] 메인 카메라를 찾을 수 없습니다!");
            }

            // 히트 인디케이터 초기화
            if (hitIndicator != null)
            {
                hitIndicator.SetActive(false);
            }
        }

        private void Update()
        {
            // 마우스 위치에 레이캐스트하여 인디케이터 업데이트
            UpdateHitIndicator();
        }

        private void LateUpdate()
        {
            if (_mainCamera == null)
            {
                return;
            }

            if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            {
                PerformCleaning();
            }
            else
            {
                _hasPreviousPosition = false;
                _lastSurface = null;
            }
        }

        private void OnDrawGizmos()
        {
            if (!showHitPoint || !Application.isPlaying)
            {
                return;
            }

            if (_lastHitPoint != Vector3.zero)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(_lastHitPoint, brushRadius * 0.5f);

                Gizmos.color = Color.green;
                Gizmos.DrawRay(_lastHitPoint, _lastHitNormal * 0.1f);
            }
        }

        /// <summary>
        /// 히트 인디케이터 위치와 회전을 업데이트합니다.
        /// </summary>
        private void UpdateHitIndicator()
        {
            if (hitIndicator == null || _mainCamera == null)
            {
                return;
            }

            if (Mouse.current == null)
            {
                hitIndicator.SetActive(false);
                return;
            }

            Ray ray = _mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                hitIndicator.SetActive(showHitPoint);

                // 큐브의 하단 면이 표면에 닿도록 위치 조정
                // 큐브의 Y축이 법선 방향을 향하도록 회전
                Quaternion rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
                hitIndicator.transform.rotation = rotation;

                // 큐브 높이의 절반만큼 법선 방향으로 오프셋
                float halfHeight = hitIndicator.transform.localScale.y * 0.5f;
                hitIndicator.transform.position = hit.point + hit.normal * halfHeight;
            }
            else
            {
                hitIndicator.SetActive(false);
            }
        }

        #endregion

        #region 청소 처리

        /// <summary>
        /// 청소 로직을 수행합니다.
        /// </summary>
        private void PerformCleaning()
        {
            if (Mouse.current == null)
            {
                return;
            }

            Ray ray = _mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                DirtySurface surface = hit.collider.GetComponent<DirtySurface>();

                if (surface == null)
                {
                    return;
                }

                MeshCollider meshCollider = hit.collider as MeshCollider;
                if (meshCollider == null)
                {
                    return;
                }

                Vector2 currentUV = hit.textureCoord;
                Vector3 viewDirection = (hit.point - _mainCamera.transform.position).normalized;

                // 디버그: UV 좌표 확인
                if (showHitPoint)
                {
                    Debug.Log($"[Clean] UV: {currentUV}, triangleIndex: {hit.triangleIndex}, mesh: {meshCollider.sharedMesh?.name}, convex: {meshCollider.convex}");

                // 이전 위치가 있고 같은 표면이면 경로를 따라 청소
                if (_hasPreviousPosition && _lastSurface == surface)
                {
                    CleanAlongPath(surface, _lastUV, currentUV, hit.normal, viewDirection);
                }
                else
                {
                    // 첫 클릭이면 현재 위치만 청소
                    surface.CleanArea(
                        currentUV,
                        hit.normal,
                        viewDirection,
                        brushRadius,
                        cleanStrength,
                        normalAngleThreshold,
                        distanceFalloffCurve,
                        angleFalloffCurve,
                        brushTexture
                    );
                }

                }

                _lastUV = currentUV;
                _lastSurface = surface;
                _hasPreviousPosition = true;
                _lastHitPoint = hit.point;
                _lastHitNormal = hit.normal;
            }
        }

        /// <summary>
        /// 두 UV 좌표 사이의 경로를 따라 청소합니다.
        /// </summary>
        private void CleanAlongPath(
            DirtySurface surface,
            Vector2 fromUV,
            Vector2 toUV,
            Vector3 hitNormal,
            Vector3 viewDirection)
        {
            float distance = Vector2.Distance(fromUV, toUV);
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / brushRadius * pathSteps));

            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                Vector2 uv = Vector2.Lerp(fromUV, toUV, t);

                surface.CleanArea(
                    uv,
                    hitNormal,
                    viewDirection,
                    brushRadius,
                    cleanStrength,
                    normalAngleThreshold,
                    distanceFalloffCurve,
                    angleFalloffCurve,
                    brushTexture
                );
            }
        }

        #endregion
    }
}
