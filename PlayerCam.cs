using UnityEngine;
using DG.Tweening;
public class PlayerCam : MonoBehaviour
{
    public float sensitivity;
    public float smoothTime = 0.05f;
    public float nearClipPlane = 0.01f;

    public Transform orientation;
    public Transform camHolder;

    float xRotation;
    float yRotation;

    private Vector2 currentMouseDelta;
    private Vector2 currentMouseDeltaVelocity;

    void Start()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        // Set the near clip plane
        Camera.main.nearClipPlane = nearClipPlane;
    }

    void Update()
    {
        float mouseX = Input.GetAxisRaw("Mouse X");
        float mouseY = Input.GetAxisRaw("Mouse Y");

        // Smooth the mouse input
        Vector2 targetMouseDelta = new Vector2(mouseX, mouseY) * sensitivity * Time.deltaTime;
        currentMouseDelta = Vector2.SmoothDamp(currentMouseDelta, targetMouseDelta, ref currentMouseDeltaVelocity, smoothTime);

        // Apply the smoothed input to the camera rotation
        yRotation += currentMouseDelta.x;
        xRotation -= currentMouseDelta.y;

        yRotation += mouseX;
        xRotation -= mouseY;

        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        camHolder.rotation = Quaternion.Euler(xRotation, yRotation, 0);
        orientation.rotation = Quaternion.Euler(0, yRotation, 0);
    }

    public void ChangeFOV(float endValue){
        GetComponent<Camera>().DOFieldOfView(endValue, 0.25f);
    }

    public void TiltCamera(float zTilt){
        transform.DOLocalRotate(new Vector3 (0,0,zTilt), 0.25f);
    }
}
