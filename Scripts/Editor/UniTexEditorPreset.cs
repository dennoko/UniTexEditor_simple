using System;
using System.Collections.Generic;
using UnityEngine;

namespace UniTexEditor
{
    [Serializable]
    public class PresetKeyframe
    {
        public float time;
        public float value;
        public float inTangent;
        public float outTangent;

        public static PresetKeyframe FromKeyframe(Keyframe k) => new PresetKeyframe
        {
            time = k.time,
            value = k.value,
            inTangent = k.inTangent,
            outTangent = k.outTangent,
        };

        public Keyframe ToKeyframe() => new Keyframe(time, value, inTangent, outTangent);
    }

    [Serializable]
    public class PresetCurve
    {
        public List<PresetKeyframe> keys = new List<PresetKeyframe>();

        public static PresetCurve FromCurve(AnimationCurve curve)
        {
            var pc = new PresetCurve();
            if (curve == null) return pc;
            foreach (var k in curve.keys)
                pc.keys.Add(PresetKeyframe.FromKeyframe(k));
            return pc;
        }

        public AnimationCurve ToCurve()
        {
            var curve = new AnimationCurve();
            foreach (var k in keys)
                curve.AddKey(k.ToKeyframe());
            return curve;
        }
    }

    [Serializable]
    public class UniTexPresetData
    {
        public string presetName;
        public string presetType; // "params" or "full"
        public int version = 1;
        public string createdAt;

        // Section enabled states
        public bool showColorCorrection;
        public bool showBlend;
        public bool showLevels;
        public bool showChannelMixer;
        public bool showSharpen;
        public bool showToneCurve;

        // Color Correction
        public float hueShift;
        public float saturation;
        public float brightness;
        public float gamma;
        public float ccTargetColorR;
        public float ccTargetColorG;
        public float ccTargetColorB;
        public float ccTargetColorA;
        public int ccBlendMode;
        public float ccBlendOpacity;

        // Tone Curve
        public PresetCurve rgbCurve;
        public PresetCurve redCurve;
        public PresetCurve greenCurve;
        public PresetCurve blueCurve;
        public bool useRGBCurve;
        public bool useRedCurve;
        public bool useGreenCurve;
        public bool useBlueCurve;

        // Blend
        public int blendMode;
        public float blendStrength;
        public bool blendTiling;
        public float blendScaleX;
        public float blendScaleY;
        public float blendOffsetX;
        public float blendOffsetY;

        // Levels
        public float lvlMinInput;
        public float lvlMaxInput;
        public float lvlMinOutput;
        public float lvlMaxOutput;
        public float lvlMidGamma;

        // Sharpen
        public int sharpenMode;
        public float sharpenStrength;
        public int sharpenKernelSize;
        public int sharpenIterations;

        // Channel Mixer
        public int cmOutRed;
        public int cmOutGreen;
        public int cmOutBlue;
        public int cmOutAlpha;

        // Mask
        public bool invertMask;
        public float maskStrength;

        // Texture GUIDs (fullプリセットのみ使用。移動・リネームに耐性あり)
        // ※ sourceTexture はプリセット対象外（適用時に現在セットされているものを維持）
        public string maskTextureGUID;
        public string blendTextureGUID;
        public string blendMaskTextureGUID;
    }
}
