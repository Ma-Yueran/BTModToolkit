using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CrossLink
{

    public class RagdollReactAnimation : MonoBehaviour
    {
        public InteractBase[] dizzyGrabPoses;

        public void FindAllInteractBaseAsGrabPoses()
        {
            // Find all InteractBase components in this GameObject and its children
            InteractBase[] foundInteracts = GetComponentsInChildren<InteractBase>(true);

            // Assign to the array
            dizzyGrabPoses = foundInteracts;

            // Optional: Log the result
            Debug.Log($"Found {foundInteracts.Length} InteractBase components in '{gameObject.name}' and its children");

            // Optional: Mark the object as dirty so changes are saved in the editor
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        [EasyButtons.Button]
        public void ConfigureMemeNpcGripPoint()
        {
#if !UNITY_EDITOR
            Debug.LogError("AttachLinesToAnimatorBones failed: this method is only available in Unity Editor.", this);
            return;
#else
            Animator animator = gameObject.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                Debug.LogError("AttachLinesToAnimatorBones failed: Animator not found.", this);
                return;
            }
            if (animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
            {
                Debug.LogError("AttachLinesToAnimatorBones failed: Animator avatar is not a valid humanoid avatar.", animator);
                return;
            }

            const string attachLinePrefabPath = "Assets/Toolkit/Prefabs/AttachLine.prefab";
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(attachLinePrefabPath);
            if (prefab == null)
            {
                Debug.LogError("AttachLinesToAnimatorBones failed: AttachLine prefab not found at " + attachLinePrefabPath + ".", this);
                return;
            }

            HumanBodyBones[] bones =
            {
                HumanBodyBones.Head,
                HumanBodyBones.RightUpperArm,
                HumanBodyBones.RightLowerArm,
                HumanBodyBones.LeftUpperArm,
                HumanBodyBones.LeftLowerArm,
                HumanBodyBones.Chest,
                HumanBodyBones.RightUpperLeg,
                HumanBodyBones.RightLowerLeg,
                HumanBodyBones.LeftUpperLeg,
                HumanBodyBones.LeftLowerLeg,
            };

            for (int i = 0; i < bones.Length; i++)
            {
                Transform bone = animator.GetBoneTransform(bones[i]);
                if (bone == null)
                {
                    Debug.LogWarning("AttachLinesToAnimatorBones skipped missing bone: " + bones[i], this);
                    continue;
                }

                var ib = bone.gameObject.GetComponent<InteractBase>();
                if (ib == null)
                    ib = bone.gameObject.AddComponent<InteractBase>();
                ib.grabDistanceLimit = true;

                GameObject attachGO = null;
                if (ib.attachList != null && ib.attachList.Length > 0 && ib.attachList[0] != null)
                {
                    attachGO = ib.attachList[0].gameObject;
                }
                else
                {
                    attachGO = Instantiate(prefab, bone);
                }
                
                AttachLine attachLine = attachGO.GetComponent<AttachLine>();
                if (attachLine == null)
                {
                    Debug.LogError("AttachLinesToAnimatorBones failed: AttachLine component not found on prefab instance.", attachGO);
                    continue;
                }

                if (IsLimbBone(bones[i]) && TryGetBoneConnection(animator, bones[i], bone, out Transform targetBone, out Vector3 attachLineDirection, out float boneDistance))
                {
                    ConfigureLimbAttachLine(attachLine, bone, targetBone, attachLineDirection, boneDistance);
                }
                else if (TryGetBoneConnectionDirection(animator, bones[i], bone, out attachLineDirection))
                {
                    attachGO.transform.localPosition = Vector3.zero;
                    attachGO.transform.rotation = Quaternion.LookRotation(attachLineDirection, bone.up);
                }
                else
                {
                    Debug.LogWarning("AttachLinesToAnimatorBones could not determine bone direction for: " + bones[i], bone);
                    attachGO.transform.localPosition = Vector3.zero;
                    attachGO.transform.localRotation = Quaternion.identity;
                }
                attachGO.transform.localScale = Vector3.one;

                AttachObj ao = attachGO.GetComponent<AttachObj>();
                ao.allowTrigger = false;

                ib.SetAsBodyPart();

                var rb = ib.gameObject.GetComponent<Rigidbody>();
                if (rb == null)
                    rb = ib.gameObject.AddComponent<Rigidbody>();
                ao.selfRB = rb;
                ao.interact = ib;

                ib.attachList = new AttachObj[] { ao };
            }

            FindAllInteractBaseAsGrabPoses();
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        private static readonly Dictionary<HumanBodyBones, HumanBodyBones> AttachLineLookAtBones = new Dictionary<HumanBodyBones, HumanBodyBones>
        {
            { HumanBodyBones.Head, HumanBodyBones.Chest },
            { HumanBodyBones.Chest, HumanBodyBones.Head },
            { HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm },
            { HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand },
            { HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm },
            { HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand },
            { HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg },
            { HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot },
            { HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg },
            { HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot },
        };

        private static bool IsLimbBone(HumanBodyBones bone)
        {
            return bone == HumanBodyBones.RightUpperArm
                || bone == HumanBodyBones.RightLowerArm
                || bone == HumanBodyBones.LeftUpperArm
                || bone == HumanBodyBones.LeftLowerArm
                || bone == HumanBodyBones.RightUpperLeg
                || bone == HumanBodyBones.RightLowerLeg
                || bone == HumanBodyBones.LeftUpperLeg
                || bone == HumanBodyBones.LeftLowerLeg;
        }

        private static void ConfigureLimbAttachLine(AttachLine attachLine, Transform sourceBone, Transform targetBone, Vector3 direction, float distance)
        {
            Transform attachTransform = attachLine.transform;
            attachTransform.SetParent(sourceBone, true);
            attachTransform.position = Vector3.Lerp(sourceBone.position, targetBone.position, 0.5f);
            attachTransform.rotation = Quaternion.LookRotation(direction, sourceBone.up);

            attachLine.lineStartPoint = distance * 0.4f;
            attachLine.lineEndPoint = -distance * 0.4f;
        }

        private static bool TryGetBoneConnection(Animator animator, HumanBodyBones sourceBone, Transform sourceTransform,
            out Transform targetTransform, out Vector3 direction, out float distance)
        {
            targetTransform = null;
            direction = Vector3.forward;
            distance = 0f;

            if (!AttachLineLookAtBones.TryGetValue(sourceBone, out HumanBodyBones targetBone))
            {
                return false;
            }

            targetTransform = animator.GetBoneTransform(targetBone);
            if (targetTransform == null)
            {
                return false;
            }

            direction = targetTransform.position - sourceTransform.position;
            distance = direction.magnitude;
            if (distance < 0.000001f)
            {
                direction = Vector3.forward;
                distance = 0f;
                return false;
            }

            direction /= distance;
            return true;
        }

        private static bool TryGetBoneConnectionDirection(Animator animator, HumanBodyBones sourceBone, Transform sourceTransform, out Vector3 direction)
        {
            if (!AttachLineLookAtBones.TryGetValue(sourceBone, out HumanBodyBones targetBone))
            {
                direction = Vector3.forward;
                return false;
            }

            Transform targetTransform = animator.GetBoneTransform(targetBone);
            if (targetTransform == null)
            {
                direction = Vector3.forward;
                return false;
            }

            return TryGetDirection(sourceTransform.position, targetTransform.position, out direction);
        }

        private static bool TryGetDirection(Vector3 from, Vector3 to, out Vector3 direction)
        {
            direction = to - from;
            if (direction.sqrMagnitude < 0.000001f)
            {
                direction = Vector3.forward;
                return false;
            }

            direction.Normalize();
            return true;
        }
    }
}


