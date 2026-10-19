using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SubmarineLDJam.Presentation
{
    public enum TouchMode
    {
        /// <summary>On when the device has a touch screen (phone, tablet, mobile browser).</summary>
        Auto,
        /// <summary>Always on. Use it to test with the mouse in the editor.</summary>
        Always,
        Never
    }

    /// <summary>Touch helpers: is this a touch device, and an invisible tap area for any UI rect.</summary>
    public static class TouchInput
    {
        public static bool IsEnabled(TouchMode mode)
        {
            switch (mode)
            {
                case TouchMode.Always: return true;
                case TouchMode.Never: return false;
                default: return Application.isMobilePlatform || Touchscreen.current != null;
            }
        }

        /// <summary>
        /// Puts an invisible full-size child on <paramref name="parent"/>. Pressing it (mouse or finger) calls <paramref name="onTap"/>.
        /// Made once. A second call with the same name only returns the existing one.
        /// </summary>
        public static RectTransform AddTapArea(RectTransform parent, string name, Action onTap)
        {
            if (parent == null) return null;

            var existing = parent.Find(name);
            if (existing != null)
            {
                existing.GetComponent<TapArea>().OnTap = onTap;
                return (RectTransform)existing;
            }

            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(TapArea));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            var image = go.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f); // invisible, but it still catches presses
            image.raycastTarget = true;

            // A layout group on the parent must not move or resize it.
            go.AddComponent<LayoutElement>().ignoreLayout = true;

            go.GetComponent<TapArea>().OnTap = onTap;
            return rect;
        }

        public static void RemoveTapArea(RectTransform parent, string name)
        {
            if (parent == null) return;
            var existing = parent.Find(name);
            if (existing != null) UnityEngine.Object.Destroy(existing.gameObject);
        }
    }

    /// <summary>Calls a method when pressed. Made by <see cref="TouchInput.AddTapArea"/>.</summary>
    public sealed class TapArea : MonoBehaviour, IPointerDownHandler
    {
        public Action OnTap;

        public void OnPointerDown(PointerEventData eventData) => OnTap?.Invoke();
    }
}
