using UnityEngine;
using UnityEngine.UI;

namespace CleanUp
{
    /// <summary>
    /// 오브젝트 표면의 더러움을 관리하고 청소 로직을 처리하는 컴포넌트입니다.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshCollider))]
    public class DirtySurface : MonoBehaviour
    {
        #region SerializeField 필드

        [SerializeField, Range(0f, 1f), Tooltip("초기 더러움 정도 (0=깨끗, 1=완전히 더러움)")]
        private float initialDirtAmount = 1.0f;

        [SerializeField, Tooltip("리셋 버튼")]
        private Button resetButton;

        #endregion

        #region Private 필드
        private static int MaskResolution = 512; // 녹 텍스쳐 해상도
        private Texture2D _dirtMask;
        private Color[] _maskPixels;
        private MeshRenderer _meshRenderer;

        private float _totalDirt;
        private float _currentDirt;

        private bool _isDirty;

        // 최적화: 브러시 알파 캐시
        private float[] _brushAlphaCache;
        private int _cachedBrushSize;
        private Texture2D _cachedBrushTexture;

        // 최적화: 더티 영역 추적 (부분 업데이트용)
        private int _dirtyMinX;
        private int _dirtyMinY;
        private int _dirtyMaxX;
        private int _dirtyMaxY;
        private bool _hasDirtyRect;

        #endregion

        #region Public 필드 및 프로퍼티

        /// <summary>
        /// 청소 진행도를 반환합니다. (0=더러움, 1=깨끗함)
        /// </summary>
        public float CleanProgress
        {
            get
            {
                if (_totalDirt <= 0)
                {
                    return 1f;
                }
                return Mathf.Clamp01(1f - (_currentDirt / _totalDirt));
            }
        }

        #endregion

        #region Unity 메소드

        private void Awake()
        {
            _meshRenderer = GetComponent<MeshRenderer>();
            InitializeDirtMask();
            SetupMaterial();
            if (resetButton == null) return;
            resetButton.onClick.AddListener(ResetDirt);
        }

        private void LateUpdate()
        {   
            // 전체영역 업데이트
            if (_isDirty)
            {
                _dirtMask.SetPixels(_maskPixels);  // 전체
                _dirtMask.Apply();
                _isDirty = false;
            }
            // 최적화용 - 프레임당 한 번만 텍스처 업데이트 (더티 영역만)
            // if (_isDirty && _hasDirtyRect)
            // {
            //     int width = _dirtyMaxX - _dirtyMinX + 1;
            //     int height = _dirtyMaxY - _dirtyMinY + 1;

            //     // 더티 영역의 픽셀만 추출
            //     Color[] dirtyPixels = new Color[width * height];
            //     for (int y = 0; y < height; y++)
            //     {
            //         for (int x = 0; x < width; x++)
            //         {
            //             int srcIdx = (_dirtyMinY + y) * MaskResolution + (_dirtyMinX + x);
            //             int dstIdx = y * width + x;
            //             dirtyPixels[dstIdx] = _maskPixels[srcIdx];
            //         }
            //     }

            //     // 부분 영역만 업데이트
            //     _dirtMask.SetPixels(_dirtyMinX, _dirtyMinY, width, height, dirtyPixels);
            //     _dirtMask.Apply(false, false);

            //     // 더티 상태 초기화
            //     _isDirty = false;
            //     _hasDirtyRect = false;
            //     _dirtyMinX = MaskResolution;
            //     _dirtyMinY = MaskResolution;
            //     _dirtyMaxX = 0;
            //     _dirtyMaxY = 0;
            // }
        }

        private void OnDestroy()
        {
            if (_dirtMask != null)
            {
                Destroy(_dirtMask);
            }
        }

        #endregion

        #region 초기화

        /// <summary>
        /// 더러움 마스크 텍스처를 초기화합니다.
        /// </summary>
        private void InitializeDirtMask()
        {
            _dirtMask = new Texture2D(MaskResolution, MaskResolution, TextureFormat.RGBA32, false);
            _dirtMask.wrapMode = TextureWrapMode.Clamp;
            _dirtMask.filterMode = FilterMode.Bilinear;

            _maskPixels = new Color[MaskResolution * MaskResolution];
            for (int i = 0; i < _maskPixels.Length; i++)
            {
                _maskPixels[i] = new Color(initialDirtAmount, 0, 0, 1);
            }

            _dirtMask.SetPixels(_maskPixels);
            _dirtMask.Apply();

            _totalDirt = initialDirtAmount * _maskPixels.Length;
            _currentDirt = _totalDirt;

            // 더티 영역 초기화
            _dirtyMinX = MaskResolution;
            _dirtyMinY = MaskResolution;
            _dirtyMaxX = 0;
            _dirtyMaxY = 0;
            _hasDirtyRect = false;
        }

        /// <summary>
        /// 머티리얼에 더러움 마스크 텍스처를 연결합니다.
        /// </summary>
        private void SetupMaterial()
        {
            Material instanceMaterial = _meshRenderer.material;
            instanceMaterial.SetTexture("_DirtMask", _dirtMask);
        }

        #endregion

        #region 청소 관련

        /// <summary>
        /// UV 좌표 기반으로 지정된 영역을 청소합니다.
        /// </summary>
        public void CleanArea(
            Vector2 centerUV, Vector3 hitNormal, Vector3 viewDirection,
            float radius, float strength, float angleThreshold,
            AnimationCurve distanceCurve, AnimationCurve angleCurve, Texture2D brushTexture
            )
        {
            int centerX = Mathf.RoundToInt(centerUV.x * MaskResolution);
            int centerY = Mathf.RoundToInt(centerUV.y * MaskResolution);
            int radiusPixels = Mathf.CeilToInt(radius * MaskResolution);

            if (radiusPixels < 1)
            {
                radiusPixels = 1;
            }

            // 브러시 캐시 업데이트
            CacheBrushAlpha(brushTexture, radiusPixels);

            float cosThreshold = Mathf.Cos(angleThreshold * Mathf.Deg2Rad);

            // 법선과 시선 방향의 각도 계산 (시선 반전)
            float dotProduct = Vector3.Dot(hitNormal, -viewDirection);

            if (dotProduct <= 0)
            {
                return; // 뒷면은 청소하지 않음
            }

            float angleEfficiency;
            if (dotProduct < cosThreshold)
            {
                float normalizedAngle = dotProduct / cosThreshold;
                angleEfficiency = normalizedAngle * normalizedAngle * 0.2f;
            }
            else
            {
                float normalizedAngle = (dotProduct - cosThreshold) / (1f - cosThreshold);
                angleEfficiency = normalizedAngle * normalizedAngle * (3f - 2f * normalizedAngle);
            }

            int radiusSq = radiusPixels * radiusPixels;
            int diameter = radiusPixels * 2 + 1;
            float dirtRemoved = 0f;

            for (int dy = -radiusPixels; dy <= radiusPixels; dy++)
            {
                int py = centerY + dy;
                if (py < 0 || py >= MaskResolution)
                {
                    continue;
                }

                for (int dx = -radiusPixels; dx <= radiusPixels; dx++)
                {
                    int px = centerX + dx;
                    if (px < 0 || px >= MaskResolution)
                    {
                        continue;
                    }

                    // 제곱 거리로 비교 (Sqrt 제거)
                    int distSq = dx * dx + dy * dy;
                    if (distSq > radiusSq)
                    {
                        continue;
                    }

                    // 캐시된 브러시 알파
                    int brushIdx = (dy + radiusPixels) * diameter + (dx + radiusPixels);
                    float brushAlpha = _brushAlphaCache[brushIdx];

                    if (brushAlpha <= 0.01f)
                    {
                        continue;
                    }

                    // 거리 감쇠
                    float normalizedDist = 1f - Mathf.Sqrt((float)distSq / radiusSq);
                    float distanceFalloff = normalizedDist * normalizedDist * (3f - 2f * normalizedDist);

                    // 최종 강도
                    float finalStrength = strength * distanceFalloff * angleEfficiency * brushAlpha;

                    if (finalStrength <= 0.001f)
                    {
                        continue;
                    }

                    int index = py * MaskResolution + px;
                    float oldValue = _maskPixels[index].r;
                    float newValue = oldValue - finalStrength;

                    if (newValue < 0f)
                    {
                        newValue = 0f;
                    }

                    if (oldValue != newValue)
                    {
                        _maskPixels[index].r = newValue;
                        dirtRemoved += (oldValue - newValue);
                        _isDirty = true;
                    }
                }
            }

            _currentDirt = Mathf.Max(0f, _currentDirt - dirtRemoved);
        }

        /// <summary>
        /// 브러시 알파 캐시를 생성합니다.
        /// </summary>
        private void CacheBrushAlpha(Texture2D brushTexture, int radiusPixels)
        {
            if (brushTexture == _cachedBrushTexture && radiusPixels == _cachedBrushSize && _brushAlphaCache != null)
            {
                return;
            }

            int diameter = radiusPixels * 2 + 1;
            _brushAlphaCache = new float[diameter * diameter];
            _cachedBrushSize = radiusPixels;
            _cachedBrushTexture = brushTexture;

            if (brushTexture == null)
            {
                for (int dy = -radiusPixels; dy <= radiusPixels; dy++)
                {
                    for (int dx = -radiusPixels; dx <= radiusPixels; dx++)
                    {
                        int idx = (dy + radiusPixels) * diameter + (dx + radiusPixels);
                        float distSq = (dx * dx + dy * dy) / (float)(radiusPixels * radiusPixels);
                        _brushAlphaCache[idx] = distSq <= 1f ? 1f : 0f;
                    }
                }
            }
            else
            {
                for (int dy = -radiusPixels; dy <= radiusPixels; dy++)
                {
                    for (int dx = -radiusPixels; dx <= radiusPixels; dx++)
                    {
                        int idx = (dy + radiusPixels) * diameter + (dx + radiusPixels);
                        float brushU = (dx + radiusPixels) / (float)(radiusPixels * 2);
                        float brushV = (dy + radiusPixels) / (float)(radiusPixels * 2);
                        _brushAlphaCache[idx] = brushTexture.GetPixelBilinear(brushU, brushV).a;
                    }
                }
            }
        }

        /// <summary>
        /// 더러움을 초기 상태로 복원합니다.
        /// </summary>
        public void ResetDirt()
        {
            for (int i = 0; i < _maskPixels.Length; i++)
            {
                _maskPixels[i].r = initialDirtAmount;
            }

            _dirtMask.SetPixels(_maskPixels);
            _dirtMask.Apply();

            _currentDirt = _totalDirt;
        }

        /// <summary>
        /// 모든 더러움을 제거합니다.
        /// </summary>
        public void CleanAll()
        {
            for (int i = 0; i < _maskPixels.Length; i++)
            {
                _maskPixels[i].r = 0f;
            }

            _dirtMask.SetPixels(_maskPixels);
            _dirtMask.Apply();

            _currentDirt = 0f;
        }

        #endregion
    }
}
