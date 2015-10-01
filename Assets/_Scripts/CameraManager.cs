using UnityEngine;
using System.Collections;

public class CameraManager : MonoBehaviour
{

    public float XSensitivity = 2;
    public float YSensitivity = 2;

    private Camera mainCamera;
    
    void Awake()
    {
        mainCamera = Camera.main;
    }

	// Use this for initialization
	void Start () {
	
	}
	
	// Update is called once per frame
	void Update () {
        Move();
        Look();
        SelectObject();
    }

   

    private void Look()
    {
        if (Input.GetKeyDown(KeyCode.Mouse1))
        {

        }
        
    }

    private void Move() {
        if (Input.GetKey(KeyCode.A)) {
            // Move left
            mainCamera.transform.Translate(Vector3.left);
        }
        if (Input.GetKey(KeyCode.D)) {
            // Move Right
            mainCamera.transform.Translate(Vector3.right);
        }
        if (Input.GetKey(KeyCode.W))
        {
            // Move Forward
            mainCamera.transform.Translate(Vector3.forward);
        }
        if (Input.GetKey(KeyCode.S))
        {
            // Move Backward
            mainCamera.transform.Translate(Vector3.back);
        }

    }


    private void SelectObject()
    {
        if (Input.GetKey(KeyCode.Mouse0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 100))
            {
                print("Hit something");
                SelectedObject selected = hit.transform.GetComponent<SelectedObject>();
                print(selected.Id);
                print(selected.Name);
                print(hit.point);
            }
        }
    }
}
