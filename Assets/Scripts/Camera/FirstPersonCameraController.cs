using UnityEngine;

/// <summary>
/// Simple mouse-look controller for first-person games. Rotates the player root on the yaw axis
/// while tilting the attached camera on the pitch axis.
/// </summary>
public class FirstPersonCameraController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Transform that represents the player's body. Yaw rotation is applied to this object.")]
    public Transform playerRoot;

    [Header("Look")]
    [Tooltip("Mouse sensitivity multiplier.")]
    public float sensitivity = 2.5f;
    [Tooltip("Higher values result in slower but smoother camera movement. Set to 0 for instant response.")]
    public float smoothing = 12f;
    [Tooltip("Minimum pitch (looking down).")]
    public float minPitch = -80f;
    [Tooltip("Maximum pitch (looking up).")]
    public float maxPitch = 80f;
    [Tooltip("Lock and hide the cursor while this component is enabled.")]
    public bool lockCursor = true;

    private float targetYaw;
    private float targetPitch;
    private float smoothYaw;
    private float smoothPitch;

    private void Awake()
    {
        Vector3 localEuler = transform.localEulerAngles;
        targetPitch = smoothPitch = NormalizePitch(localEuler.x);
        targetYaw = smoothYaw = playerRoot != null ? playerRoot.eulerAngles.y : transform.eulerAngles.y;
        ApplyCursorState(true);
    }

    private void OnEnable()
    {
        ApplyCursorState(true);
    }

    private void OnDisable()
    {
        ApplyCursorState(false);
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            ApplyCursorState(true);
        }
        else
        {
            ApplyCursorState(false);
        }
    }

    private void Update()
    {
        float mouseX = Input.GetAxisRaw("Mouse X");
        float mouseY = Input.GetAxisRaw("Mouse Y");

        targetYaw += mouseX * sensitivity;
        targetPitch -= mouseY * sensitivity;
        targetPitch = Mathf.Clamp(targetPitch, minPitch, maxPitch);

        float lerpFactor = smoothing > 0f ? 1f - Mathf.Exp(-smoothing * Time.deltaTime) : 1f;
        smoothYaw = Mathf.LerpAngle(smoothYaw, targetYaw, lerpFactor);
        smoothPitch = Mathf.Lerp(smoothPitch, targetPitch, lerpFactor);

        if (playerRoot != null)
        {
            playerRoot.rotation = Quaternion.Euler(0f, smoothYaw, 0f);
            transform.localRotation = Quaternion.Euler(smoothPitch, 0f, 0f);
        }
        else
        {
            transform.rotation = Quaternion.Euler(smoothPitch, smoothYaw, 0f);
        }
    }

    private float NormalizePitch(float eulerX)
    {
        eulerX %= 360f;
        if (eulerX > 180f)
        {
            eulerX -= 360f;
        }
        return eulerX;
    }

    private void ApplyCursorState(bool shouldLock)
    {
        if (!lockCursor)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }

        if (shouldLock)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
