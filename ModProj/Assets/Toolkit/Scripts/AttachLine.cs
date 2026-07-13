using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.Rendering;
#endif


namespace CrossLink
{
    public class AttachLine : AttachObj
    {
        [Tooltip("define the start position of the line, should be bigger than end point")]
        public float lineStartPoint;
        [Tooltip("define the end position of the line, should be smaller than end point")]
        public float lineEndPoint;
        [Tooltip("override the callback pos")]
        public float designCallingPos;
        [Tooltip("give this line thickness")]
        public float lineOffset = 0;

        public enum GrabRotLimitType
        {
            None,
            Right,
            Left,
            Both,
        }

        [Tooltip("define where hand's index pointing to, None means 360, Both means left&right side")]
        public GrabRotLimitType indexLimit;

        public enum GrabThumbLimitType
        {
            None,
            Forward,
            Backward,
        }

        [Tooltip("define where hand's thumb pointing to, None means both side")]
        public GrabThumbLimitType thumbLimit;

        public enum GrabPalmLimitType
        {
            None,
            ToY,
            FromY,
        }
        [Tooltip("define where hand's plam pointing to, None means both side, ToY means to Y axis")]
        public GrabPalmLimitType palmLimit;


        public RelativeHandPose[] handRelativePosition;

        public void Reset()
        {
            base.Reset();
            if (handRelativePosition.Length == 0 && transform.childCount == 2)
            {
                handRelativePosition = new RelativeHandPose[2];
                for (int i = 0; i < 2; ++i)
                {
                    var child = transform.GetChild(i);
                    if (child != null)
                    {
                        handRelativePosition[i] = child.GetComponent<RelativeHandPose>();
                    }
                }
            }
        }
#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Vector3 start = transform.forward * lineStartPoint + transform.position;
            Vector3 end = transform.forward * lineEndPoint + transform.position;

            Color oldColor = Handles.color;
            CompareFunction oldZTest = Handles.zTest;

            Handles.zTest = CompareFunction.Always;

            Handles.color = new Color(0f, 0f, 0f, 0.9f);
            Handles.DrawAAPolyLine(6f, start, end);

            Handles.color = Color.yellow;
            Handles.DrawAAPolyLine(3f, start, end);

            if (lineOffset > 0)
            {
                DrawLineOffsetCylinder(start, end, lineOffset);
            }

            Handles.color = oldColor;
            Handles.zTest = oldZTest;
        }

        private void DrawLineOffsetCylinder(Vector3 start, Vector3 end, float radius)
        {
            Vector3 axis = end - start;
            if (axis.sqrMagnitude < 0.000001f)
                return;

            Vector3 normal = axis.normalized;
            Vector3 right = transform.right * radius;
            Vector3 up = transform.up * radius;

            Handles.color = new Color(1f, 0.9f, 0f, 0.16f);
            Handles.DrawSolidDisc(start, normal, radius);
            Handles.DrawSolidDisc(end, normal, radius);

            Handles.color = new Color(0f, 0f, 0f, 0.65f);
            Handles.DrawWireDisc(start, normal, radius);
            Handles.DrawWireDisc(end, normal, radius);
            Handles.DrawAAPolyLine(3f, start + right, end + right);
            Handles.DrawAAPolyLine(3f, start - right, end - right);
            Handles.DrawAAPolyLine(3f, start + up, end + up);
            Handles.DrawAAPolyLine(3f, start - up, end - up);

            Handles.color = new Color(1f, 0.9f, 0f, 0.85f);
            Handles.DrawWireDisc(start, normal, radius);
            Handles.DrawWireDisc(end, normal, radius);
            Handles.DrawAAPolyLine(1.5f, start + right, end + right);
            Handles.DrawAAPolyLine(1.5f, start - right, end - right);
            Handles.DrawAAPolyLine(1.5f, start + up, end + up);
            Handles.DrawAAPolyLine(1.5f, start - up, end - up);
        }
#endif
    }
}
