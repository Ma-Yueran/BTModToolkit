using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CrossLink
{

    [System.Serializable]
    public class BlendValue
    {
        public int index;
        public float value;
        public float valueOffset = -1;
    }

    [System.Serializable]
    public class BlendSet
    {
        public string setName;
        public float blendTime = 0.25f;
        public BlendValue[] blends;


        public void Copy(BlendSet other)
        {
            blendTime = other.blendTime;
            blends = new BlendValue[other.blends.Length];
            for (int i=0; i<blends.Length; ++i)
            {
                blends[i] = new BlendValue();
                blends[i].index = other.blends[i].index;
                blends[i].value = other.blends[i].value;
                blends[i].valueOffset = other.blends[i].valueOffset;
            }
        }


        public void InitValueOffset()
        {
            for (int i = 0; i < blends.Length; ++i)
            {
                if (blends[i].valueOffset > 0)
                {
                    blends[i].value += Random.Range(-blends[i].valueOffset, blends[i].valueOffset);
                }
            }
        }

        public float GetIndexValue(int index)
        {
            for (int i=0; i<blends.Length; ++i)
            {
                if (blends[i].index == index)
                {
                    //return blends[i].valueOffset <= 0 ? blends[i].value : blends[i].value + Random.Range(-blends[i].valueOffset, blends[i].valueOffset);
                    return blends[i].value;
                }
            }
            return -1;
        }
        public void AddIndexValue(int index, float value, float offset = 0)
        {
            BlendValue blend = null;
            for (int i = 0; i < blends.Length; ++i)
            {
                if (blends[i].index == index)
                {
                    blend = blends[i];
                    break;
                }
            }
            if (blend == null)
            {
                var list = new List<BlendValue>(blends);
                blend = new BlendValue();
                list.Add(blend);
                blends = list.ToArray();
            }
            blend.index = index;
            blend.value = value;
            blend.valueOffset = offset;
        }
    }

    public class BlendShapeController : MonoBehaviour
    {
        public SkinnedMeshRenderer animMesh;
        [SerializeField]
        private bool faceStartAtZero = true;
        public BlendSet[] blends;
        public const string DefaultFacial = "Default";
        public const string FearFacial = "Fear";
        public const string AngerFacial = "Anger";
        public const string AtkFacial = "Atk";
        public const string DeadFacial = "Dead";
        public const string HurtFacial = "Hurt";

        private void Reset()
        {
            animMesh = GetComponent<SkinnedMeshRenderer>();
            blends = new BlendSet[6] {
                new BlendSet(){ setName = DefaultFacial, },
                new BlendSet(){ setName = FearFacial, },
                new BlendSet(){ setName = AngerFacial, },
                new BlendSet(){ setName = AtkFacial, },
                new BlendSet(){ setName = DeadFacial, },
                new BlendSet(){ setName = HurtFacial, },
            };
        }

#if UNITY_EDITOR
        [EasyButtons.Button]
        void PullBlendShapeToLastIndex()
        {
            var blend = blends[blends.Length - 1];
            for (int i = 0; i < animMesh.sharedMesh.blendShapeCount; ++i)
            {
                var weight = animMesh.GetBlendShapeWeight(i);
                if (weight > 0)
                {
                    blend.AddIndexValue(i, weight);
                }
            }

            EditorUtility.SetDirty(this);
        }

        [EasyButtons.Button]
        void PullBlendShapeToIndex(int idx)
        {
            if (idx >= blends.Length)
                return;

            var blend = blends[idx];
            if (blend.blends == null)
                blend.blends = new BlendValue[0];
            for (int i = 0; i < animMesh.sharedMesh.blendShapeCount; ++i)
            {
                var weight = animMesh.GetBlendShapeWeight(i);
                if (weight > 0)
                {
                    blend.AddIndexValue(i, weight);
                }
            }

            EditorUtility.SetDirty(this);
        }

        [EasyButtons.Button]
        void ApplyBlendShape(int idx)
        {
            if (animMesh == null || animMesh.sharedMesh == null || blends == null || idx < 0 || idx >= blends.Length)
                return;

            BlendSet blend = blends[idx];
            if (blend == null || blend.blends == null)
                return;

            int blendShapeCount = animMesh.sharedMesh.blendShapeCount;
            for (int i = 0; i < blendShapeCount; ++i)
            {
                animMesh.SetBlendShapeWeight(i, 0);
            }

            for (int i = 0; i < blend.blends.Length; ++i)
            {
                BlendValue blendValue = blend.blends[i];
                if (blendValue == null || blendValue.index < 0 || blendValue.index >= blendShapeCount)
                    continue;

                animMesh.SetBlendShapeWeight(blendValue.index, blendValue.value);
            }

            EditorUtility.SetDirty(animMesh);
            EditorUtility.SetDirty(this);
        }

#endif
    }
}
