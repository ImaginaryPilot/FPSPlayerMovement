using UnityEngine;

public class MoveCamera : MonoBehaviour
{
    // Script for camera to follow player
    public Transform Head;

    private void Update(){
        transform.position = Head.position;
    }
}
