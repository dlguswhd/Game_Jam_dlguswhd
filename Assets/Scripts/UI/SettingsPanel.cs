using System;
using GameJam.World;
using UnityEngine;

namespace GameJam.UI
{
    // Settings window shared by the title screen and the ESC menu: mouse sensitivity and master volume.
    public static class SettingsPanel
    {
        public static GameObject Create(Transform parent, Action onBack)
        {
            var window = UIKit.RoundedPanel("Settings", parent, UIKit.Panel);
            UIKit.Center(window.rectTransform, new Vector2(760f, 420f));
            var column = window.rectTransform;
            UIKit.Column(column, 18f, 44);

            var title = UIKit.Label("Title", column, "설정", 42, UIKit.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            title.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 64f;

            UIKit.SliderRow(column, "마우스 감도", GameSettings.MinSensitivity, GameSettings.MaxSensitivity,
                GameSettings.Sensitivity, v => v.ToString("0.00"), v => GameSettings.Sensitivity = v);
            UIKit.SliderRow(column, "사운드", 0f, 1f, GameSettings.Volume,
                v => Mathf.RoundToInt(v * 100f) + "%", v => GameSettings.Volume = v);

            var spacer = UIKit.Rect("Spacer", column);
            spacer.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 20f;

            UIKit.Button(column, "돌아가기", () =>
            {
                GameSettings.Save();
                onBack?.Invoke();
            });
            return window.gameObject;
        }
    }
}
