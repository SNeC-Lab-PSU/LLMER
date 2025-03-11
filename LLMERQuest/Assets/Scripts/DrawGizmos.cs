using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class DrawGizmos : MonoBehaviour
{
    private LineRenderer[] lineRenderers;

    private void OnDrawGizmos()
    {
        BoxCollider boxCollider = GetComponent<BoxCollider>();

        if (lineRenderers == null)
        {
            // Initialize LineRenderers for each edge
            lineRenderers = new LineRenderer[12];
            for (int i = 0; i < lineRenderers.Length; i++)
            {
                GameObject lineObject = new GameObject("LineRenderer" + i);
                lineObject.transform.SetParent(transform);
                lineRenderers[i] = lineObject.AddComponent<LineRenderer>();
                lineRenderers[i].startWidth = 0.02f;
                lineRenderers[i].endWidth = 0.02f;
                lineRenderers[i].material = new Material(Shader.Find("Sprites/Default"));
                lineRenderers[i].startColor = Color.green;
                lineRenderers[i].endColor = Color.green;
                lineRenderers[i].positionCount = 2;
            }
        }
        if (boxCollider != null)
        {
            Gizmos.color = Color.green; // Set the color of the gizmo lines

            // Get the size and center of the Box Collider
            Vector3 size = boxCollider.size;
            Vector3 center = boxCollider.center;

            // Calculate the 8 corners of the Box Collider
            Vector3[] corners = new Vector3[8];
            corners[0] = transform.TransformPoint(center + new Vector3(size.x, size.y, size.z) * 0.5f);
            corners[1] = transform.TransformPoint(center + new Vector3(size.x, size.y, -size.z) * 0.5f);
            corners[2] = transform.TransformPoint(center + new Vector3(size.x, -size.y, size.z) * 0.5f);
            corners[3] = transform.TransformPoint(center + new Vector3(size.x, -size.y, -size.z) * 0.5f);
            corners[4] = transform.TransformPoint(center + new Vector3(-size.x, size.y, size.z) * 0.5f);
            corners[5] = transform.TransformPoint(center + new Vector3(-size.x, size.y, -size.z) * 0.5f);
            corners[6] = transform.TransformPoint(center + new Vector3(-size.x, -size.y, size.z) * 0.5f);
            corners[7] = transform.TransformPoint(center + new Vector3(-size.x, -size.y, -size.z) * 0.5f);

            // Gizmo can only be seen in Scene view
            DrawEdge(0, corners[0], corners[1]);
            DrawEdge(1, corners[0], corners[2]);
            DrawEdge(2, corners[0], corners[4]);
            DrawEdge(3, corners[1], corners[3]);
            DrawEdge(4, corners[1], corners[5]);
            DrawEdge(5, corners[2], corners[3]);
            DrawEdge(6, corners[2], corners[6]);
            DrawEdge(7, corners[3], corners[7]);
            DrawEdge(8, corners[4], corners[5]);
            DrawEdge(9, corners[4], corners[6]);
            DrawEdge(10, corners[5], corners[7]);
            DrawEdge(11, corners[6], corners[7]);
        }
    }
    private void DrawEdge(int index, Vector3 start, Vector3 end)
    {
        lineRenderers[index].SetPosition(0, start);
        lineRenderers[index].SetPosition(1, end);
    }
}
