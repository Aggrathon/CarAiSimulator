
using UnityEngine;

[ExecuteInEditMode]
public class MeshSortingLayer : MonoBehaviour
{
	public string sortingLayer;
	public int sortingOrder;
	
	void Start()
	{
		foreach (var r in GetComponentsInChildren<Renderer>())
		{
			r.sortingLayerName = sortingLayer;
			r.sortingOrder = sortingOrder;
		}
	}

	
}
