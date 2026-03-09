using DG.Tweening;
using UnityEngine;

namespace Level
{
    [DisallowMultipleComponent]
    public sealed class LampFlicker : MonoBehaviour
    {
        public enum Pattern
        {
            SmoothPulse,
            Blink,
            DoubleBlink,
            FlickerRandom,
            CustomSequence
        }

        [System.Serializable]
        public struct Keyframe
        {
            [Range(0f, 1f)] public float to01;
            [Min(0.001f)] public float duration;
            public Ease ease;
        }

        [Header("Target")]
        [SerializeField] private Light targetLight;
        [SerializeField] private bool affectEmission = false;
        [SerializeField] private Renderer emissionRenderer;
        [SerializeField] private string emissionColorProperty = "_EmissionColor";

        [Header("Intensity")]
        [SerializeField, Min(0f)] private float minIntensity = 0.0f;
        [SerializeField, Min(0f)] private float maxIntensity = 4.0f;
        [SerializeField] private bool randomStartOffset = true;

        [Header("Timing")]
        [SerializeField, Min(0.05f)] private float period = 0.6f;
        [SerializeField] private Pattern pattern = Pattern.SmoothPulse;

        [Header("Easing (for SmoothPulse / segments)")]
        [SerializeField] private Ease easeUp = Ease.OutSine;
        [SerializeField] private Ease easeDown = Ease.InSine;

        [Header("Blink params")]
        [SerializeField, Range(0.01f, 0.99f)] private float duty = 0.25f;
        [SerializeField, Min(0.01f)] private float doubleBlinkGap = 0.06f;

        [Header("Random flicker")]
        [SerializeField, Min(1)] private int randomSteps = 6;
        [SerializeField, Range(0f, 1f)] private float randomMin01 = 0.15f;
        [SerializeField, Range(0f, 1f)] private float randomMax01 = 1.0f;
        [SerializeField] private int randomSeed = 12345;

        [Header("Custom keyframes")]
        [SerializeField] private Keyframe[] custom = new Keyframe[]
        {
            new Keyframe{ to01 = 1f, duration = 0.08f, ease = Ease.OutQuad },
            new Keyframe{ to01 = 0f, duration = 0.18f, ease = Ease.InQuad },
            new Keyframe{ to01 = 0.6f, duration = 0.06f, ease = Ease.OutQuad },
            new Keyframe{ to01 = 0f, duration = 0.28f, ease = Ease.InQuad },
        };

        [Header("Spawn On Lit (optional)")]
        [SerializeField] private bool spawnOnLit = false;
        [SerializeField, Range(0f, 1f)] private float spawnChance = 0.25f;

        [Tooltip("Если задано — будет инстанциться при загорании (если шанс сработал).")]
        [SerializeField] private GameObject spawnPrefab;

        [Tooltip("Если задано — вместо Instantiate будет просто активироваться/деактивироваться этот объект.")]
        [SerializeField] private GameObject spawnExisting;

        [SerializeField] private Transform spawnParent;
        [SerializeField] private bool destroySpawnedOnOff = false;

        [Tooltip("С какого значения 0..1 считаем, что лампа 'горит'. Для Blink ставь 0.5, для SmoothPulse обычно 0.2..0.4.")]
        [SerializeField, Range(0f, 1f)] private float litThreshold01 = 0.35f;

        private MaterialPropertyBlock _mpb;
        private int _emissionId;
        private Sequence _seq;

        private float _value01;         // 0..1
        private bool _isLit;            // текущее состояние "горит"
        private bool _rolledThisLit;    // чтобы шанс роллить один раз на lit-период
        private GameObject _spawnedInstance;

        private void Reset()
        {
            targetLight = GetComponentInChildren<Light>();
            emissionRenderer = GetComponentInChildren<Renderer>();
            spawnParent = transform;
        }

        private void Awake()
        {
            if (!targetLight) targetLight = GetComponentInChildren<Light>();

            if (affectEmission)
            {
                if (!emissionRenderer) emissionRenderer = GetComponentInChildren<Renderer>();
                _mpb ??= new MaterialPropertyBlock();
                _emissionId = Shader.PropertyToID(emissionColorProperty);
            }

            if (!spawnParent) spawnParent = transform;
        }

        private void OnEnable() => BuildAndPlay();

        private void OnDisable()
        {
            KillSequence();
            // на всякий случай выключим/уберём объект
            HandleLitState(false);
        }

        private void OnValidate()
        {
            period = Mathf.Max(0.05f, period);
            maxIntensity = Mathf.Max(0f, maxIntensity);
            minIntensity = Mathf.Max(0f, minIntensity);
            if (maxIntensity < minIntensity) maxIntensity = minIntensity;

            randomSteps = Mathf.Max(1, randomSteps);
            randomMin01 = Mathf.Clamp01(randomMin01);
            randomMax01 = Mathf.Clamp01(randomMax01);
            if (randomMax01 < randomMin01) randomMax01 = randomMin01;

            litThreshold01 = Mathf.Clamp01(litThreshold01);
            spawnChance = Mathf.Clamp01(spawnChance);
        }

        public void Restart() => BuildAndPlay();

        public void StopAndSetOff()
        {
            KillSequence();
            Set01(0f);
            HandleLitState(false);
        }

        public void StopAndSetOn()
        {
            KillSequence();
            Set01(1f);
            HandleLitState(true);
        }

        private void BuildAndPlay()
        {
            KillSequence();

            _value01 = randomStartOffset ? Random.value : 0f;
            Set01(_value01);

            _seq = DOTween.Sequence();
            _seq.SetLink(gameObject, LinkBehaviour.KillOnDisable);
            _seq.SetLoops(-1, LoopType.Restart);

            switch (pattern)
            {
                case Pattern.SmoothPulse:    AppendSmoothPulse(_seq); break;
                case Pattern.Blink:         AppendBlink(_seq); break;
                case Pattern.DoubleBlink:   AppendDoubleBlink(_seq); break;
                case Pattern.FlickerRandom: AppendRandomFlicker(_seq); break;
                case Pattern.CustomSequence:AppendCustom(_seq); break;
            }

            if (randomStartOffset)
            {
                float offset = Random.Range(0f, Mathf.Max(0.001f, period));
                _seq.Goto(offset, true);
            }
        }

        private void KillSequence()
        {
            if (_seq != null)
            {
                _seq.Kill(false);
                _seq = null;
            }
            DOTween.Kill(this);
        }

        private void AppendSmoothPulse(Sequence seq)
        {
            float up = period * 0.5f;
            float down = period - up;

            seq.Append(DOVirtual.Float(0f, 1f, up, Set01).SetEase(easeUp).SetTarget(this));
            seq.Append(DOVirtual.Float(1f, 0f, down, Set01).SetEase(easeDown).SetTarget(this));
        }

        private void AppendBlink(Sequence seq)
        {
            float onT = Mathf.Clamp(period * duty, 0.01f, period);
            float offT = Mathf.Max(0.01f, period - onT);

            seq.AppendCallback(() => Set01(1f));
            seq.AppendInterval(onT);
            seq.AppendCallback(() => Set01(0f));
            seq.AppendInterval(offT);
        }

        private void AppendDoubleBlink(Sequence seq)
        {
            float blinkT = Mathf.Clamp(period * 0.12f, 0.03f, 0.2f);
            float gap = Mathf.Clamp(doubleBlinkGap, 0.01f, 0.3f);

            float used = blinkT + gap + blinkT;
            float rest = Mathf.Max(0.01f, period - used);

            seq.AppendCallback(() => Set01(1f));
            seq.AppendInterval(blinkT);
            seq.AppendCallback(() => Set01(0f));
            seq.AppendInterval(gap);
            seq.AppendCallback(() => Set01(1f));
            seq.AppendInterval(blinkT);
            seq.AppendCallback(() => Set01(0f));
            seq.AppendInterval(rest);
        }

        private void AppendRandomFlicker(Sequence seq)
        {
            var rng = new System.Random(randomSeed);

            float stepT = period / Mathf.Max(1, randomSteps);
            for (int i = 0; i < randomSteps; i++)
            {
                float to = Mathf.Lerp(randomMin01, randomMax01, (float)rng.NextDouble());
                seq.Append(DOVirtual.Float(_value01, to, stepT, Set01).SetEase(Ease.OutQuad).SetTarget(this));
            }
            seq.Append(DOVirtual.Float(_value01, 0f, stepT, Set01).SetEase(Ease.InQuad).SetTarget(this));
        }

        private void AppendCustom(Sequence seq)
        {
            if (custom == null || custom.Length == 0)
            {
                AppendSmoothPulse(seq);
                return;
            }

            float total = 0f;
            for (int i = 0; i < custom.Length; i++)
                total += Mathf.Max(0.001f, custom[i].duration);

            float scale = period / Mathf.Max(0.001f, total);

            for (int i = 0; i < custom.Length; i++)
            {
                var k = custom[i];
                float dur = Mathf.Max(0.001f, k.duration) * scale;
                float to = Mathf.Clamp01(k.to01);
                seq.Append(DOVirtual.Float(_value01, to, dur, Set01).SetEase(k.ease).SetTarget(this));
            }
        }

        private void Set01(float v)
        {
            _value01 = Mathf.Clamp01(v);

            float intensity = Mathf.Lerp(minIntensity, maxIntensity, _value01);
            if (targetLight) targetLight.intensity = intensity;

            if (affectEmission && emissionRenderer)
                ApplyEmission(intensity);

            // управление состоянием "горит/не горит" по порогу
            bool shouldBeLit = _value01 >= litThreshold01;
            if (shouldBeLit != _isLit)
                HandleLitState(shouldBeLit);
        }

        private void HandleLitState(bool lit)
        {
            _isLit = lit;

            if (_isLit)
            {
                _rolledThisLit = false;

                if (!spawnOnLit)
                    return;

                // Роллим шанс ровно один раз при входе в lit
                _rolledThisLit = true;

                if (Random.value > spawnChance)
                    return;

                SpawnOrEnable();
            }
            else
            {
                _rolledThisLit = false;
                HideOrDestroy();
            }
        }

        private void SpawnOrEnable()
        {
            // если указан существующий объект — просто включаем
            if (spawnExisting != null)
            {
                spawnExisting.SetActive(true);
                _spawnedInstance = spawnExisting;
                return;
            }

            // иначе — инстансим префаб
            if (spawnPrefab != null)
            {
                _spawnedInstance = Instantiate(spawnPrefab, spawnParent ? spawnParent : transform);
                _spawnedInstance.SetActive(true);
            }
        }

        private void HideOrDestroy()
        {
            if (_spawnedInstance == null)
            {
                // если управляем существующим объектом, но его не сохранили — выключим напрямую
                if (spawnExisting != null) spawnExisting.SetActive(false);
                return;
            }

            if (_spawnedInstance == spawnExisting)
            {
                _spawnedInstance.SetActive(false);
                _spawnedInstance = null;
                return;
            }

            if (destroySpawnedOnOff)
                Destroy(_spawnedInstance);
            else
                _spawnedInstance.SetActive(false);

            _spawnedInstance = null;
        }

        private void ApplyEmission(float intensity)
        {
            emissionRenderer.GetPropertyBlock(_mpb);

            Color baseCol = Color.white;
            var mat = emissionRenderer.sharedMaterial;
            if (mat && mat.HasProperty(_emissionId))
                baseCol = mat.GetColor(_emissionId);

            _mpb.SetColor(_emissionId, baseCol * Mathf.Max(0f, intensity));
            emissionRenderer.SetPropertyBlock(_mpb);
        }
    }
}