using UnityEngine;

namespace CameraHandling
{
  
    public class CameraMovement : MonoBehaviour
    {
        public Transform target; // The target object to rotate around

        public float rotationSpeed = 3.0f; // The speed of camera rotation

        private float mouseX, mouseY;

        void Start()
        {
            Cursor.lockState = CursorLockMode.Locked; 
        }

        void Update()
        {
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");
            Vector3 direction = new Vector3(horizontal, 0, vertical);
            transform.Translate(direction * Time.deltaTime * 5f);
            
            // Get mouse input
            mouseX += Input.GetAxis("Mouse X") * rotationSpeed;
            mouseY -= Input.GetAxis("Mouse Y") * rotationSpeed;
            mouseY = Mathf.Clamp(mouseY, -80f, 80f); // Limit the vertical rotation angle
            mouseX = Mathf.Clamp(mouseY, -10f, 10f); // Limit the vertical rotation angle

            // Rotate the camera based on mouse input
            transform.LookAt(target);
            target.rotation = Quaternion.Euler(mouseY, mouseX, 0);
        }
    }
}
