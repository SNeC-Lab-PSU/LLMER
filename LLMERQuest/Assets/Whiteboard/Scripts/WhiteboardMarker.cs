using Meta.WitAi;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using Unity.Collections;
using UnityEngine;

public class WhiteboardMarker : MonoBehaviour
{
    [SerializeField] private Transform _tip;
    [SerializeField] private int _penSize = 5;
    [SerializeField] private float offsetAbovePlane = 0.005f;

    private Renderer _renderer;
    private Color[] _colors;
    private float _tipHeight;
    // original color
    private Color _originalColor;

    private RaycastHit _touch;
    private Whiteboard _whiteboard;
    private Vector2 _touchPos, _lastTouchPos;
    private bool _touchedLastFrame;
    private Quaternion _lastTouchRot;
    private List<Vector2> drawPoints = new List<Vector2>();   
    int _layerMask = 1 << 7; // Layer 7 is the whiteboard layer
    private DrawStickManager drawStickManager;

    void Start()
    {
        _renderer = _tip.GetComponent<Renderer>();
        GameObject boardObj = GameObject.FindWithTag("Whiteboard");
        if (boardObj != null)
        {
            int layer = boardObj.layer;
            _layerMask = 1 << layer;
        }
        _colors = Enumerable.Repeat(_renderer.material.color, _penSize * _penSize).ToArray();
        _tipHeight = _tip.localScale.y;
        _originalColor = _renderer.material.color;
        drawStickManager = GetComponent<DrawStickManager>();
    }

    void Update()
    {
        // change the color once the marker color is changed
        if (_renderer.material.color != _originalColor)
        {
            _colors = Enumerable.Repeat(_renderer.material.color, _penSize * _penSize).ToArray();
            _originalColor = _renderer.material.color;
        }

        Draw();
    }

    private void Draw()
    {
        // need to exclude the marker itself from the raycast
        // instead of directly using the tip position, offset it by the tip height towards the tip direction
        Vector3 offsetPos = _tip.position - 1.5f * transform.up * _tipHeight;
        if (Physics.Raycast(offsetPos, transform.up, out _touch, _tipHeight*2, _layerMask))
        {
            // if hand is closer to the touch point, try to enable the constraint to stick the marker to the whiteboard
            drawStickManager.StickToParent(); 

            if (_touch.transform.CompareTag("Whiteboard"))
            {
                if (_whiteboard == null)
                {
                    _whiteboard = _touch.transform.GetComponent<Whiteboard>();
                }

                _touchPos = new Vector2(_touch.textureCoord.x, _touch.textureCoord.y);
                drawPoints.Add(_touchPos);

                var x = (int)(_touchPos.x * _whiteboard.textureSize.x - (_penSize / 2));
                var y = (int)(_touchPos.y * _whiteboard.textureSize.y - (_penSize / 2));

                if (y < 0 || y > _whiteboard.textureSize.y || x < 0 || x > _whiteboard.textureSize.x) return;

                if (_touchedLastFrame)
                {
                    _whiteboard.texture.SetPixels(x, y, _penSize, _penSize, _colors);

                    for (float f = 0.01f; f < 1.00f; f += 0.01f)
                    {
                        var lerpX = (int)Mathf.Lerp(_lastTouchPos.x, x, f);
                        var lerpY = (int)Mathf.Lerp(_lastTouchPos.y, y, f);
                        _whiteboard.texture.SetPixels(lerpX, lerpY, _penSize, _penSize, _colors);
                    }

                    transform.rotation = _lastTouchRot;
                    
                    _whiteboard.texture.Apply();
                }

                _lastTouchPos = new Vector2(x, y);
                _lastTouchRot = transform.rotation;
                _touchedLastFrame = true;
                return;
            }
        }
        _whiteboard = null;
        _touchedLastFrame = false;
    }
    
    public string GetDrawPoints()
    {
        string drawPointsString = "";
        foreach (Vector2 point in drawPoints)
        {
            drawPointsString += "(" + point.x + "," + point.y + ")"+",";
        }
        drawPointsString += "\n";
        return drawPointsString;
    }

}
