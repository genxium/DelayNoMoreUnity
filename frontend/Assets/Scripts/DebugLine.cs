using UnityEngine;

public class DebugLine : MonoBehaviour {
    public int score;
    public LineRenderer lineRenderer;
    // Start is called before the first frame update

    public void SetWidth(float w) {
        lineRenderer.startWidth = w;
        lineRenderer.endWidth = w;
    }

    public void SetColor(Color c) {
        lineRenderer.startColor = c;
        lineRenderer.endColor = c;
    }

    public int GetPositions(Vector3[] holder) {
        return lineRenderer.GetPositions(holder);
    }

    public void SetPositions(Vector3[] holder) {
        lineRenderer.SetPositions(holder);
    }
}
