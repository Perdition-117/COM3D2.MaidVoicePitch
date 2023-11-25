using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Linq;
using System.Reflection;

namespace CM3D2.MaidVoicePitch.Patcher {
    public static class MaidVoicePitchPatcher {
        public static readonly string[] TargetAssemblyNames = { "Assembly-CSharp.dll" };

        public static void Patch(AssemblyDefinition assembly) {
            try {
                AssemblyDefinition ta = assembly;
                AssemblyDefinition da = PatcherHelper.GetAssemblyDefinition("COM3D2.MaidVoicePitch.Managed.dll");
                string m = "CM3D2.MaidVoicePitch.Managed.";

                // TBody.LateUpdateの処理終了後にCM3D2.MaidVoicePitch.Managed.Callbacks.TBody.LateUpdate.Invokeを呼び出す
                PatcherHelper.SetHook(PatcherHelper.HookType.PostCall, ta, "TBody", "LateUpdate", da, m + "Callbacks.TBody.LateUpdate", "Invoke");

                // TBody.MoveHeadAndEyeの処理を完全に乗っ取り、CM3D2.MaidVoicePitch.Managed.Callbacks.TBody.MoveHeadAndEye.Invokeの呼び出しに置き換える
                PatcherHelper.SetHook(PatcherHelper.HookType.PreJump, ta, "TBody", "MoveHeadAndEye", da, m + "Callbacks.TBody.MoveHeadAndEye", "Invoke");

                // BoneMorph_.Blendの処理終了後にCM3D2.MaidVoicePitch.Managed.Callbacks.BoneMorph_.Blend.Invokeを呼び出す
                PatcherHelper.SetHook(PatcherHelper.HookType.PostCall, ta, "BoneMorph_", "Blend", da, m + "Callbacks.BoneMorph_.Blend", "Invoke");

                // AudioSourceMgr.Playの処理終了後にCM3D2.MaidVoicePitch.Managed.Callbacks.AudioSourceMgr.Play.Invokeを呼び出す
                PatcherHelper.SetHook(PatcherHelper.HookType.PostCall, ta, "AudioSourceMgr", "Play", da, m + "Callbacks.AudioSourceMgr.Play", "Invoke");

                // AudioSourceMgr.PlayOneShotの処理終了後にCM3D2.MaidVoicePitch.Managed.Callbacks.AudioSourceMgr.PlayOneShot.Invokeを呼び出す
                PatcherHelper.SetHook(PatcherHelper.HookType.PostCall, ta, "AudioSourceMgr", "PlayOneShot", da, m + "Callbacks.AudioSourceMgr.PlayOneShot", "Invoke");

                PatcherHelper.SetHook(PatcherHelper.HookType.PreCall, ta, "CharacterMgr", "PresetSet", da, "CM3D2.MaidVoicePitch.Managed.Callbacks.CharacterMgr.PresetSet", "Invoke");

                Patch_SceneEdit_SlideCallback(assembly);

                // スカート調整
                PatcherHelper.SetHook(
                    PatcherHelper.HookType.PreCall,
                    ta, "DynamicSkirtBone", "UpdateSelf",
                    da, "CM3D2.MaidVoicePitch.Managed.Callbacks.DynamicSkirtBone.PreUpdateSelf", "Invoke");

                PatcherHelper.SetHook(
                    PatcherHelper.HookType.PostCall,
                    ta, "DynamicSkirtBone", "UpdateSelf",
                    da, "CM3D2.MaidVoicePitch.Managed.Callbacks.DynamicSkirtBone.PostUpdateSelf", "Invoke");

                // 胸ボーンサイズ用
                PatcherHelper.SetHook(
                    PatcherHelper.HookType.PreCall,
                    ta, "jiggleBone", "LateUpdateSelf",
                    da, "CM3D2.MaidVoicePitch.Managed.Callbacks.jiggleBone.PreLateUpdateSelf", "Invoke");

                // 胸ボーンサイズ用
                PatcherHelper.SetHook(
                    PatcherHelper.HookType.PostCall,
                    ta, "jiggleBone", "LateUpdateSelf",
                    da, "CM3D2.MaidVoicePitch.Managed.Callbacks.jiggleBone.PostLateUpdateSelf", "Invoke");

            } catch (Exception e) {
                Helper.ShowException(e);
                throw;
            }
        }

        private static void Patch_SceneEdit_SlideCallback(AssemblyDefinition assembly) {
            // SceneEdit.SlideCallback の補間式を変更し、
            // タブ等を変更してスライダーがアクティブになる度に
            // 負の値が 0 に近づくのを抑制する
            //
            // 元の補間式は以下のようになっている
            //
            //      (int) (prop1.min + (prop1.max - prop1.min) * UIProgressBar.current.value + 0.5)
            //
            // 例えば prop1.min = -100, prop1.max = 100, UIProgressBar.current.value = 0 の場合、
            // 以下のようになる
            //
            //        (int) (-100 + (100+100) * 0 + 0.5)
            //		= (int) (-99.5)
            //		= -99
            //
            //      double -> int のキャストについては右記を参照 : https://msdn.microsoft.com/en-us/library/yht2cx7b.aspx
            //
            // この値は期待する値 -100 になっていないので、これを以下のように修正したい
            //
            //      (int) Math.Round(prop1.min + (prop1.max - prop1.min) * UIProgressBar.current.value)
            //
            // ILレベルでは、該当部分は以下のようになっているので
            //
            //      IL_004a: callvirt instance float32 UIProgressBar::get_value()
            //      IL_004f: mul
            //      IL_0050: add
            //  --> IL_0051: ldc.r4 0.5
            //  --> IL_0056: add
            //      IL_0057: conv.i4
            //
            // これを以下のように改変する
            //
            //      IL_004a: callvirt instance float32 UIProgressBar::get_value()
            //      IL_004f: mul
            //      IL_0050: add
            //  --> IL_0051: call float64 [mscorlib]System.Math::Round(float64)
            //  --> IL_0056: nop
            //      IL_0057: conv.i4

            // [Assembly-CSharp] SceneEdit.SlideCallback() を取得
            var method = GetMethod(
                assembly,                      // Assembly-CSharp.dll
                "SceneEdit",                        // クラス名
                "SlideCallback",                    // メソッド名
                new string[] { }                        // メソッドの引数の型 (void)
            );

            // [mscorlib.dll] System.Math.Round(System.Double) を取得
            var methodReference = typeof(System.Math).GetMethod("Round", new Type[] { typeof(System.Double) });

            Mono.Cecil.Rocks.MethodBodyRocks.SimplifyMacros(method.Body);

            // SlideCallback内で ldc.r4 0.5 (IL_0051の命令) を探す
            var inst = method.Body.Instructions.Last(
                i => i.OpCode == OpCodes.Ldc_R4 &&
                (float)i.Operand == 0.5f);

            // 見つけた場所の命令を Call に書き換え
            inst.OpCode = OpCodes.Call;
            inst.Operand = assembly.MainModule.ImportReference(methodReference);

            // 次の命令を Nop に書き換え
            inst.Next.OpCode = OpCodes.Nop;

            Mono.Cecil.Rocks.MethodBodyRocks.OptimizeMacros(method.Body);
        }

        private static MethodDefinition GetMethod(
            AssemblyDefinition assemblyDefinition, string typeName,
            string methodName, params string[] args) {
            TypeDefinition td = assemblyDefinition.MainModule.GetType(typeName);
            return PatcherHelper.GetMethod(td, methodName, args);
        }
    }
}
