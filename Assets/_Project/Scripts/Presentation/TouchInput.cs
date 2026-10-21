using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace SubmarineLDJam.Presentation
{
    public enum TouchMode
    {
        /// <summary>Only a finger counts. Mouse clicks are ignored. Works the same in a phone browser and on a PC with a touch screen.</summary>
        Auto,
        /// <summary>Finger and mouse both count. Use it to test with the mouse in the editor.</summary>
        Always,
        Never
    }

    /// <summary>Touch helpers: an invisible tap area for any UI rect, that can accept only fingers.</summary>
    public static class TouchInput
    {
        public static bool IsEnabled(TouchMode mode)
        {
            return mode != TouchMode.Never;
        }

        /// <summary>
        /// Puts an invisible full-size child on <paramref name="parent"/>. Pressing it (mouse or finger) calls <paramref name="onTap"/>.
        /// Made once. A second call with the same name only returns the existing one.
        /// </summary>
        public static RectTransform AddTapArea(RectTransform parent, string name, Action onTap, bool touchOnly = false)
        {
            if (parent == null) return null;

            var existing = parent.Find(name);
            if (existing != null)
            {
                var old = existing.GetComponent<TapArea>();
                old.OnTap = onTap;
                old.TouchOnly = touchOnly;
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

            var tap = go.GetComponent<TapArea>();
            tap.OnTap = onTap;
            tap.TouchOnly = touchOnly;
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

        /// <summary>true = mouse presses are ignored, only a finger counts.</summary>
        public bool TouchOnly;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (TouchOnly && !IsFinger(eventData)) return;
            OnTap?.Invoke();
        }

        private static bool IsFinger(PointerEventData eventData)
            => eventData is ExtendedPointerEventData extended && extended.pointerType == UIPointerType.Touch;
    }
}
