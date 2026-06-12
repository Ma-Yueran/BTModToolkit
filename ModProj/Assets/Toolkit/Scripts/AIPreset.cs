using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
#endif
using UnityEngine;

namespace CrossLink
{
    [System.Serializable]
    public class CooldownSlot
    {
        public float skillsetInterval = 5;
        public int performNumInterval = 0;
        public string specialCondition;
        public ActionData[] actions;
    }

    [CreateAssetMenu(fileName = "AIPreset", menuName = "AIPreset")]
    [System.Serializable]
    public class AIPreset : ScriptableObject
    {
        public RuntimeAnimatorController controller;
        public ActionData[] aiActions;
        public ActionData[] dodgeActions;
        public ActionData[] wakeupActions;
        public CooldownSlot[] cooldownSlots;
        public string originalController;
        public AnimLayoutDataItem[] animLayoutDatas;
#if UNITY_EDITOR

        [EasyButtons.Button]
        public void AutoAddState(string folderPath)
        {
            var ac = controller as AnimatorController;
            AnimatorControllerLayer layer = ac.layers[0];
            var targetSubMachine = FindSubStateMachine(layer.stateMachine, "Actions");
            if (!targetSubMachine)
                return;

            if (aiActions != null)
                SetupAnimatorState(targetSubMachine, aiActions, folderPath, "Ai Actions");
            if (dodgeActions != null)
                SetupAnimatorState(targetSubMachine, dodgeActions, folderPath, "Dodge Actions");
            if (wakeupActions != null)
                SetupAnimatorState(targetSubMachine, wakeupActions, folderPath, "Wakeup Actions");
        }

        void SetupAnimatorState(AnimatorStateMachine machine, ActionData[] actionDatas, string path, string desc = "")
        {
            if (actionDatas == null)
                return;

            var prefix = AddressableConfig.GetConfig().GetPrefix();
            Dictionary<string, string> logDic = new Dictionary<string, string>();
            for (int i = 0; i < actionDatas.Length; i++)
            {
                if (actionDatas[i].timelines != null)
                {
                    for (int j = 0; j < actionDatas[i].timelines.Length; j++)
                    {
                        if (actionDatas[i].timelines[j].actionDatas != null)
                        {
                            for (int k = 0; k < actionDatas[i].timelines[j].actionDatas.Length; k++)
                            {
                                if (actionDatas[i].timelines[j].actionDatas[k] is ActionAnimData animData)
                                {
                                    if (!string.IsNullOrEmpty(animData.animName))
                                    {
                                        var name = animData.animName.Replace(prefix, string.Empty);
                                        var clipPath = System.IO.Path.Combine(path, name);
                                        var clip = ResourceMgr.Load(clipPath) as AnimationClip;
                                        if (clip != null)
                                        {
                                            var existingState = machine.states.FirstOrDefault(childState => childState.state.name == animData.animName);

                                            if (!existingState.Equals(default(ChildAnimatorState)))
                                            {
                                                machine.RemoveState(existingState.state);
                                            }

                                            var state = machine.AddState(animData.animName);
                                            state.motion = clip;
                                            state.speedParameterActive = true;
                                            state.speedParameter = "Speed";
                                            if (!logDic.ContainsKey(animData.animName))
                                                logDic.Add(animData.animName, clipPath);
                                        }
                                        else
                                        {
                                            if (!logDic.ContainsKey(animData.animName))
                                                logDic.Add(animData.animName, string.Empty);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            Debug.Log($"========== Animator State Setup " + desc + " ==========");
            Debug.Log($"Total processed: {logDic.Count}");

            var successList = logDic.Where(kv => !string.IsNullOrEmpty(kv.Value)).ToList();
            if (successList.Count > 0)
            {
                Debug.Log($"Successfully loaded ({successList.Count}):");
                foreach (var kv in successList)
                {
                    Debug.Log($"  ✓ {kv.Key} -> {kv.Value}");
                }
            }

            var failedList = logDic.Where(kv => string.IsNullOrEmpty(kv.Value)).ToList();
            if (failedList.Count > 0)
            {
                Debug.LogWarning($"Failed to load ({failedList.Count}):");
                foreach (var kv in failedList)
                {
                    Debug.LogWarning($"  ✗ {kv.Key} (Clip not found at path: {System.IO.Path.Combine(path, kv.Key.Replace(prefix, string.Empty))})");
                }
            }

            Debug.Log($"==================================================");
        }

#if false
        [EasyButtons.Button]
        public void AutoSetPlaceholder()
        {
            var ac = controller as AnimatorController;
            AnimatorControllerLayer layer = ac.layers[0];
            var targetSubMachine = FindSubStateMachine(layer.stateMachine, "Actions");
            if (!targetSubMachine)
                return;

            string clipFolderPath = "Assets/Tool/CharacterAnimator/AnimationClip";
            if (!AssetDatabase.IsValidFolder(clipFolderPath))
            {
                System.IO.Directory.CreateDirectory(Application.dataPath + "/Tool/CharacterAnimator/AnimationClip");
                AssetDatabase.Refresh();
            }

            for (int i = 0; i < targetSubMachine.states.Length; i++)
            {
                if (targetSubMachine.states[i].state.motion == null)
                {
                    string stateName = targetSubMachine.states[i].state.name;
                    string clipPath = $"{clipFolderPath}/{stateName}.anim";
                    AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);

                    if (clip == null)
                    {
                        clip = new AnimationClip();
                        clip.name = stateName;

                        AssetDatabase.CreateAsset(clip, clipPath);
                        AssetDatabase.SaveAssets();
                        AssetDatabase.Refresh();

                        clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                    }
                    targetSubMachine.states[i].state.motion = clip;
                    EditorUtility.SetDirty(ac);
                }
            }
            AssetDatabase.SaveAssets();
        }

#endif



        private AnimatorStateMachine FindSubStateMachine(AnimatorStateMachine root, string name)
        {
            if (root.name == name)
                return root;

            foreach (ChildAnimatorStateMachine child in root.stateMachines)
            {
                var result = FindSubStateMachine(child.stateMachine, name);
                if (result != null)
                    return result;
            }

            return null;
        }
        [EasyButtons.Button]
        public void ExportRootMotions(AnimationClip animationClip)
        {
            if (animationClip == null)
            {
                Debug.LogError("ExportRootMotions failed: AnimationClip is null");
                return;
            }

            var item = AnimTool.ExportRootMotion(animationClip);

            if (item == null)
            {
                Debug.LogError($"ExportRootMotions failed: Failed to export root motion for {animationClip.name}");
                return;
            }

            if (animLayoutDatas == null)
            {
                animLayoutDatas = new AnimLayoutDataItem[1];
                animLayoutDatas[0] = item;
                Debug.Log($"ExportRootMotions success: Added {animationClip.name} (First item)");
            }
            else
            {
                var array = new AnimLayoutDataItem[animLayoutDatas.Length + 1];
                System.Array.Copy(animLayoutDatas, array, animLayoutDatas.Length);
                array[animLayoutDatas.Length] = item;
                animLayoutDatas = array;
                Debug.Log($"ExportRootMotions success: Added {animationClip.name} (Total: {animLayoutDatas.Length})");
            }
        }

        [EasyButtons.Button]
        public void ExportAllRootMotions(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath))
            {
                Debug.LogError("ExportAllRootMotions failed: Folder path is null or empty");
                return;
            }

            Debug.Log($"ExportAllRootMotions: Started exporting from folder: {folderPath}");

            var items = AnimTool.ExportRootMotions(folderPath);

            if (items == null || items.Length == 0)
            {
                Debug.LogWarning($"ExportAllRootMotions: No animations found or exported from {folderPath}");
                return;
            }

            var validItems = items.Where(item => item != null).ToArray();
            var invalidCount = items.Length - validItems.Length;

            if (validItems.Length == 0)
            {
                Debug.LogWarning($"ExportAllRootMotions: All {items.Length} items failed to export from {folderPath}");
                return;
            }

            Debug.Log($"ExportAllRootMotions: Successfully exported {validItems.Length} items from {folderPath}" +
                      (invalidCount > 0 ? $" ({invalidCount} failed)" : ""));

            var successNames = validItems.Select(item => item.Name).Where(name => !string.IsNullOrEmpty(name)).ToArray();
            if (successNames.Length > 0)
            {
                Debug.Log($"Exported animations: {string.Join(", ", successNames)}");
            }

            if (animLayoutDatas == null)
            {
                animLayoutDatas = validItems;
                Debug.Log($"ExportAllRootMotions: Created new array with {validItems.Length} items");
            }
            else
            {
                var array = new AnimLayoutDataItem[animLayoutDatas.Length + validItems.Length];
                System.Array.Copy(animLayoutDatas, array, animLayoutDatas.Length);
                System.Array.Copy(validItems, 0, array, animLayoutDatas.Length, validItems.Length);
                animLayoutDatas = array;
                Debug.Log($"ExportAllRootMotions: Merged {validItems.Length} items to existing {animLayoutDatas.Length - validItems.Length} items (Total: {animLayoutDatas.Length})");
            }
        }
#endif
    }
}