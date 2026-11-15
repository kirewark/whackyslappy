using UnityEngine;

namespace WhackySlappy.Core
{
    /// <summary>
    /// Draws a simple dot crosshair in the center of the screen using OnGUI.
    /// </summary>
    public class Crosshair : MonoBehaviour
    {
        [SerializeField] private float m_dotSize = 4f;
        [SerializeField] private Color m_dotColor = Color.white;
        [SerializeField] private float m_alpha = 0.9f;

        private void OnGUI()
        {
            var cx = Screen.width * 0.5f;
            var cy = Screen.height * 0.5f;
            var size = m_dotSize;

            var color = m_dotColor;
            color.a = m_alpha;
            GUI.color = color;

            // Draw a small square dot.
            GUI.DrawTexture(new Rect(cx - size * 0.5f, cy - size * 0.5f, size, size), Texture2D.whiteTexture);

            GUI.color = Color.white; // Reset.
        }
    }
}
