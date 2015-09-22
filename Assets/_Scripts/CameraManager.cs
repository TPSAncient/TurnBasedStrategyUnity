using UnityEngine;
using System.Collections;
using UnityEngine.UI;

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
                //print("Hit something");
                SelectedObject selected = hit.transform.GetComponent<SelectedObject>();
                SelectionType(selected);
                
                //print(selected.Id);
                //print(selected.Name);
                //print(hit.point);
            }
        }
    }

    private void SelectionType(SelectedObject selected)
    {
        switch (selected.SelectedObjectType)
        {
                case SelectedObjectType.City:
            {
                    GameObject cityNameDes = GameObject.Find("CityName");
                    GameObject cityName = GameObject.Find("CityName");
                    cityName.GetComponent<Text>().text = selected.Name;
                    break;
            }case SelectedObjectType.Farm:
            {
                    GameObject farmNameDes = GameObject.Find("CityName");
                    GameObject cityName = GameObject.Find("CityName");
                    cityName.GetComponent<Text>().text = selected.Name;
                    break;
            }case SelectedObjectType.Terrain:
            {
                break;
            }
            default:
            {
                break;
            }
        }
    }
}
