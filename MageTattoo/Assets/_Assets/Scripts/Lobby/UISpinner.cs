using UnityEngine;

public class LoadingSpinner : MonoBehaviour
{
    public float rotationSpeed = -200f; // Negativo gira a la derecha

    void Update()
    {
        transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);
    }
}