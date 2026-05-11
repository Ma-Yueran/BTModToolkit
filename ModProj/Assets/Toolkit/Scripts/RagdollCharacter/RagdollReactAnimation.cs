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

                GameObject attachGO = Instantiate(prefab, bone);
                attachGO.name = prefab.name;
                attachGO.transform.localPosition = Vector3.zero;
                attachGO.transform.localRotation = Quaternion.identity;
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
    }
}


