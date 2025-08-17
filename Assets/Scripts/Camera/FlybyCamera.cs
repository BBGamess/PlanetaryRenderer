using UnityEngine;
using UnityEngine.InputSystem;

public class FlybyCamera : MonoBehaviour
{
    [SerializeField] private Camera camera;
    [SerializeField] private PlanetRenderer planet;

    [SerializeField] private float lambdaMax = 15f;
    [SerializeField] private float lambdaMin = 8f;

    private InputAction moveLateral;
    private InputAction moveVertical;
    private InputAction look;
    private InputAction rollIn;
    private InputAction enableRotation;

    private Quaternion target = Quaternion.identity;
    Vector3 posTarget;

    public float sensitivity = 1f;

    private void Start()
    {
        moveLateral  = InputSystem.actions.FindAction("Movement");
        moveVertical = InputSystem.actions.FindAction("UpDown");
        look = InputSystem.actions.FindAction("Look");
        rollIn = InputSystem.actions.FindAction("Roll");
        enableRotation = InputSystem.actions.FindAction("EnableRotation");

        moveVertical?.Enable();
        moveLateral?.Enable();
        look?.Enable();
        rollIn?.Enable();
        enableRotation?.Enable();

        posTarget = transform.position;
    }

    private void OnEnable()
    {
        moveVertical?.Enable();
        moveLateral?.Enable();
        look?.Enable();
        rollIn?.Enable();
        enableRotation?.Enable();
    }

    private void OnDisable()
    {
        moveVertical?.Disable();
        moveLateral?.Disable();
        look?.Disable();
        rollIn?.Disable();
        enableRotation?.Disable();
    }

    private Quaternion Damp(Quaternion q, Quaternion w, float lambda, float deltaTime)
    {
        return Quaternion.Slerp(q, w, 1 - Mathf.Exp(- lambda * deltaTime));
    }

    private Vector3 Damp(Vector3 a, Vector3 b, float lambda, float deltaTime)
    {
        return Vector3.Lerp(a, b, 1 - Mathf.Exp(- lambda * deltaTime));
    }

    private void UpdatePosition()
    {
        Vector2 lateral = moveLateral.ReadValue<Vector2>();
        float vertical = moveVertical.ReadValue<float>();

        Vector3 translate = new Vector3(lateral.x, vertical, lateral.y) * 100f;
        translate = transform.rotation * translate * Time.deltaTime;
        posTarget += translate;
        
        transform.position = Damp(transform.position, posTarget, 10f, Time.deltaTime);
    }

    private float NormalizeAngle(float angle)
    {
        return Mathf.Repeat(angle + 180f, 360f) - 180f;
    }

    private void UpdateRotation()
    {
        Vector2 lookInput = Vector2.zero;
        float rollInputVal = rollIn.ReadValue<float>() * sensitivity;

        if (enableRotation.IsPressed())
        {
            lookInput = look.ReadValue<Vector2>() * sensitivity;
        }

        Quaternion qYaw = Quaternion.AngleAxis(lookInput.x, target * Vector3.up);
        Quaternion qPitch = Quaternion.AngleAxis(-lookInput.y, target * Vector3.right);
        Quaternion qRoll = Quaternion.AngleAxis(-rollInputVal, target * Vector3.forward);

        target = qYaw * qPitch * qRoll * target;

        float lambda = Mathf.Lerp(lambdaMin, lambdaMax, look.ReadValue<Vector2>().magnitude);

        transform.rotation = Damp(transform.rotation, target, lambda, Time.deltaTime);
    }

    private void LateUpdate()
    {
        UpdatePosition();
        UpdateRotation();
    }
}
