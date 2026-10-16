
using UnityEngine;
using UnityEngine.InputSystem;

public class BrushController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private AccumulationSurface surface;
    [SerializeField] private Transform zoneVisual;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float moveSpeed = 5f;

    [Header("Radius")]
    [SerializeField, Min(0.1f)] private float baseRadius = 2f;
    [SerializeField, Min(0f)] private float radiusAmplitude = 0.5f;
    [SerializeField, Min(0.01f)] private float radiusFrequency = 0.5f;

    [SerializeField]
    private AnimationCurve radiusCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Accumulation")]
    [SerializeField, Min(0f)] private float accumulationSpeed = 0.5f;

    private float elapsedTime;
    private float currentRadius;

    private Vector3 previousPosition;
    private float previousRadius;

    private void Start()
    {
        if (surface == null)
        {
            Debug.LogError(
                "BrushController: назначь AccumulationSurface!",
                this
            );

            enabled = false;
            return;
        }

        currentRadius = Mathf.Max(0.1f, baseRadius);

        transform.position =
            surface.ClampWorldPosition(transform.position);

        previousPosition = transform.position;
        previousRadius = currentRadius;

        UpdateVisual();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return;

        // R — сброс накопленного материала.
        if (keyboard.rKey.wasPressedThisFrame)
        {
            surface.ResetSurface();
        }

        // Сохраняем состояние до движения.
        previousPosition = transform.position;
        previousRadius = currentRadius;

        Move(keyboard);
        UpdateRadius();

        // Space — накопление материала.
        if (keyboard.spaceKey.isPressed)
        {
            surface.AccumulateAlongPath(
                previousPosition,
                transform.position,
                previousRadius,
                currentRadius,
                Time.deltaTime,
                accumulationSpeed
            );
        }

        UpdateVisual();
    }

    private void Move(Keyboard keyboard)
    {
        float x = 0f;
        float z = 0f;

        if (keyboard.aKey.isPressed)
            x -= 1f;

        if (keyboard.dKey.isPressed)
            x += 1f;

        if (keyboard.sKey.isPressed)
            z -= 1f;

        if (keyboard.wKey.isPressed)
            z += 1f;

        // Нормализация не позволяет двигаться быстрее
        // при одновременном нажатии двух клавиш.
        Vector3 direction = new Vector3(x, 0f, z).normalized;

        Vector3 nextPosition =
            transform.position +
            direction * moveSpeed * Time.deltaTime;

        transform.position =
            surface.ClampWorldPosition(nextPosition);
    }

    private void UpdateRadius()
    {
        elapsedTime += Time.deltaTime;

        // Значение движется по кривой туда и обратно.
        float phase = Mathf.PingPong(
            elapsedTime * radiusFrequency,
            1f
        );

        float curveValue = radiusCurve.Evaluate(phase);

        currentRadius = Mathf.Max(
            0.1f,
            baseRadius +
            radiusAmplitude * (2f * curveValue - 1f)
        );
    }

    private void UpdateVisual()
    {
        if (zoneVisual == null)
            return;

        // Масштабируем визуальный круг под текущий радиус.
        zoneVisual.localScale = new Vector3(
            currentRadius * 2f,
            0.02f,
            currentRadius * 2f
        );
    }

    public float CurrentRadius => currentRadius;
}