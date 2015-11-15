using UnityEngine;
using System.Collections;
using Core.Data.World.Location;

public class WorldObject : MonoBehaviour {
    public int Id = 0;
    public string Name = "";
    public WorldObjectEnum SelectedObjectType = WorldObjectEnum.None;
    public bool IsWorldObjectSelected = false;

    public ILocation Location { get; set; }

    public void SetUpDataForWorldObject()
    {
        
    }

}
