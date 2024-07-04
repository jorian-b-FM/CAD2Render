using System;
using UnityEngine;

public struct DialogButtonData
{
    public string Text;
    public Action<Dialog> Action;
}

[AddComponentMenu("")]
public class Dialog : MonoBehaviour
{
    public Rect Rect { get; private set; }
    public string Text { get; set; }
    public string Title { get; set; }

    private DialogButtonData[] _buttons;

    public bool IsActive => _active;

    // Only show it if needed.
    private bool _active = false;
    
    public GUIStyle Style { get; set; }
    public GUIStyle TextStyle { get; set; }
    private static GUIStyle _defaultStyle;


    public static Dialog Show(string title, string text, params DialogButtonData[] buttons)
        => Show(title, text, Screen.width, Screen.height, buttons);

    public static Dialog Show(string title, string text, float width, float height, params DialogButtonData[] buttons)
    {
        if (_defaultStyle == null)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, new Color(0.2f, 0.2f, 0.2f));
            tex.Apply();

            _defaultStyle = new GUIStyle("window");
            _defaultStyle.onNormal.background = _defaultStyle.normal.background = tex;
        }
        
        var go = new GameObject("[GENERATED] Dialog");
        var dialog = go.AddComponent<Dialog>();
        dialog.Rect = new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);
        dialog.Title = title;
        dialog.Text = text;

        if (buttons == null || buttons.Length == 0)
        {
            dialog._buttons = new[]
            {
                new DialogButtonData
                {
                    Text = "OK",
                    Action = x => x.Close()
                }
            };
        }
        else
            dialog._buttons = buttons;

        dialog.Style = _defaultStyle;
        dialog.TextStyle = new GUIStyle("label")
        {
            alignment = TextAnchor.MiddleCenter
        };

        dialog._active = true;
        return dialog;
    }

    public void Close()
    {
        _active = false;
        Destroy(gameObject);
    }

    public void QuitApplication()
    {
        Close();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
        Application.Quit();
    }

    void OnGUI()
    {
        if (_active)
        {
            Rect = GUI.Window(0, Rect, DialogWindow, Title, Style);
        }
    }

    // This is the actual window.
    void DialogWindow(int windowID)
    {
        GUILayout.Label(Text, TextStyle);

        GUILayout.BeginHorizontal();
        foreach (var button in _buttons)
        {
            if (GUILayout.Button(button.Text))
                button.Action?.Invoke(this);
        }

        GUILayout.EndHorizontal();
    }
}