using UnityEngine;
using System.Collections;

public class CameraManager : MonoBehaviour {

	// Use this for initialization
	void Start () {
	
	}
	
	// Update is called once per frame
	void Update () {
        SelectObject();
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
