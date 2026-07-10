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
                //Gizmos.DrawSphere(transform.position + transform.forward * designCallingPos, 0.05f);

                Handles.color = new Color(1f, 0.9f, 0f, 0.25f);
                Handles.SphereHandleCap(
                    0,
                    transform.position,
                    Quaternion.identity,
                    lineOffset * 2f,
                    EventType.Repaint);
            }

            Handles.color = oldColor;
            Handles.zTest = oldZTest;
        }
#endif
    }
}
