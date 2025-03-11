using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using static UnityEngine.GraphicsBuffer;

[CustomEditor(typeof(ChatControl))]
public class ChatEditor : Editor
{
    string Msg = "Input Text";

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI(); // This draws the default inspector

        ChatControl script = (ChatControl)target; // Cast the target object to your script type

        // Create a text field for user input
        Msg = EditorGUILayout.TextField("Input Text",Msg);

        if (GUILayout.Button("Send Message")) // Adds a button with text
        {
            script.GetResponse(Msg); // Calls MyFunction when the button is clicked
        }
    }
}
