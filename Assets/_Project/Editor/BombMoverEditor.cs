using SubmarineLDJam.Presentation;
using UnityEditor;
using UnityEngine;

namespace SubmarineLDJam.EditorTools
{
    /// <summary>
    /// Draws the path of a BombMover in the Scene view.
    /// PointToPoint: a line from A to B with arrows. Radial: the circle around the center, with the direction.
    /// Always drawn when its points / center are set (even if the bomb is not selected).
    /// Selected: also drag the handles to change Travel (B) or Radius.
    /// </summary>
    [CustomEditor(typeof(BombMover))]
    public sealed class BombMoverEditor : Editor
    {
        private static readonly Color PathColor = new Color(1f, 0.35f, 0.25f, 0.95f);
        private static readonly Color HintColor = new Color(1f, 0.35f, 0.25f, 0.45f);

        // ---------- drawing (also when not selected) ----------

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected | GizmoType.Pickable)]
        private static void DrawPath(BombMover mover, GizmoType type)
        {
            var so = new SerializedObject(mover);
            var mode = (BombMover.MoveMode)so.FindProperty("mode").enumValueIndex;
            bool selected = (type & GizmoType.Selected) != 0;

            if (mode == BombMover.MoveMode.PointToPoint)
            {
                bool hasRefs = so.FindProperty("pointA").objectReferenceValue != null
                               || so.FindProperty("pointB").objectReferenceValue != null;
                if (selected || hasRefs) DrawLine(mover, so);
            }
            else if (mode == BombMover.MoveMode.Radial)
            {
                bool hasCenter = so.FindProperty("center").objectReferenceValue != null;
                if (selected || hasCenter) DrawCircle(mover, so);
            }
        }

        private static void GetEnds(BombMover mover, SerializedObject so, out Vector2 a, out Vector2 b)
        {
            var pointA = so.FindProperty("pointA").objectReferenceValue as Transform;
            var pointB = so.FindProperty("pointB").objectReferenceValue as Transform;
            a = pointA != null ? (Vector2)pointA.position : (Vector2)mover.transform.position;
            b = pointB != null ? (Vector2)pointB.position : a + so.FindProperty("travel").vector2Value;
        }

        private static void GetCircle(BombMover mover, SerializedObject so, out Vector2 center, out float radius, out float startDegrees)
        {
            var centerRef = so.FindProperty("center").objectReferenceValue as Transform;
            radius = so.FindProperty("radius").floatValue;
            Vector2 start = mover.transform.position;

            if (centerRef != null)
            {
                center = centerRef.position;
                Vector2 offset = start - center;
                startDegrees = offset.sqrMagnitude > 0.0001f
                    ? Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg
                    : so.FindProperty("startAngle").floatValue;
            }
            else
            {
                startDegrees = so.FindProperty("startAngle").floatValue;
                float rad = startDegrees * Mathf.Deg2Rad;
                center = start - new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;
            }
        }

        private static void DrawLine(BombMover mover, SerializedObject so)
        {
            GetEnds(mover, so, out var a, out var b);

            Handles.color = PathColor;
            Handles.DrawDottedLine(a, b, 6f);
            Handles.DrawWireDisc(a, Vector3.forward, 0.3f);
            Handles.DrawWireDisc(b, Vector3.forward, 0.3f);
            Handles.Label(a + Vector2.up * 0.5f, "A");
            Handles.Label(b + Vector2.up * 0.5f, "B");

            // Arrow heads both ways: it goes back and forth.
            Vector2 mid = (a + b) * 0.5f;
            Vector2 dir = (b - a).sqrMagnitude > 0.0001f ? (b - a).normalized : Vector2.right;
            Arrow(mid + dir * 0.4f, dir);
            Arrow(mid - dir * 0.4f, -dir);
        }

        private static void DrawCircle(BombMover mover, SerializedObject so)
        {
            GetCircle(mover, so, out var center, out float radius, out float startDegrees);
            float speed = so.FindProperty("angularSpeed").floatValue;

            Handles.color = PathColor;
            Handles.DrawWireDisc(center, Vector3.forward, radius);
            Handles.DrawWireDisc(center, Vector3.forward, 0.2f);

            Handles.color = HintColor;
            Handles.DrawDottedLine(center, mover.transform.position, 4f);

            // Arrow heads along the circle, pointing the way the bomb goes.
            float sign = speed >= 0f ? 1f : -1f;
            for (int i = 0; i < 4; i++)
            {
                float angle = (startDegrees + 45f + i * 90f) * Mathf.Deg2Rad;
                Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                Vector2 tangent = new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle)) * sign;
                Handles.color = PathColor;
                Arrow(point, tangent);
            }
        }

        private static void Arrow(Vector2 tip, Vector2 direction)
        {
            const float size = 0.5f;
            Vector2 side = new Vector2(-direction.y, direction.x);
            Vector2 back = tip - direction * size;
            Handles.DrawAAPolyLine(3f, back + side * size * 0.45f, tip, back - side * size * 0.45f);
        }

        // ---------- handles (selected only) ----------

        private void OnSceneGUI()
        {
            var mover = (BombMover)target;
            serializedObject.Update();
            var mode = (BombMover.MoveMode)serializedObject.FindProperty("mode").enumValueIndex;

            if (mode == BombMover.MoveMode.PointToPoint)
            {
                // B can be dragged when it is not a GameObject. Then the path is A + Travel.
                if (serializedObject.FindProperty("pointB").objectReferenceValue != null) return;

                GetEnds(mover, serializedObject, out var a, out var b);
                EditorGUI.BeginChangeCheck();
                var moved = Handles.FreeMoveHandle(b, HandleUtility.GetHandleSize(b) * 0.12f, Vector3.zero, Handles.SphereHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.FindProperty("travel").vector2Value = (Vector2)moved - a;
                    serializedObject.ApplyModifiedProperties();
                }
            }
            else if (mode == BombMover.MoveMode.Radial)
            {
                GetCircle(mover, serializedObject, out var center, out float radius, out float startDegrees);
                float rad = startDegrees * Mathf.Deg2Rad;
                Vector2 handlePoint = center + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;

                EditorGUI.BeginChangeCheck();
                var moved = Handles.FreeMoveHandle(handlePoint, HandleUtility.GetHandleSize(handlePoint) * 0.12f, Vector3.zero, Handles.SphereHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.FindProperty("radius").floatValue = Mathf.Max(0.1f, Vector2.Distance(center, moved));
                    serializedObject.ApplyModifiedProperties();
                }
            }
        }
    }
}
