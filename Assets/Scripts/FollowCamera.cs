using UnityEngine;
 
public class FollowCamera : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0f, 3f, -6f);
    public float smoothSpeed = 5f;
 
    [Tooltip("Altura del punto al que mira la camara, sobre el pivote del objetivo.")]
    public float lookHeight = 0.5f;
 
    void LateUpdate()
    {
        if (target == null) return;
 
        Vector3 desiredPosition = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        transform.LookAt(target.position + Vector3.up * lookHeight);
    }
}
 