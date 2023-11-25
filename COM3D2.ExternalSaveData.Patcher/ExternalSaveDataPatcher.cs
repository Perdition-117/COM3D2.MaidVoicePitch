using Mono.Cecil;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace CM3D2.ExternalSaveData.Patcher {
    public static class Patcher {
        public static readonly string[] TargetAssemblyNames = { "Assembly-CSharp.dll" };

        public static void Patch(AssemblyDefinition assembly) {
            AssemblyDefinition ta = assembly;
            AssemblyDefinition da = PatcherHelper.GetAssemblyDefinition("COM3D2.ExternalSaveData.Managed.dll");

            // GameMain.OnInitializeの処理後、CM3D2.略.ExSaveData.DummyInitializeを呼ぶ
            PatcherHelper.SetHook(
                PatcherHelper.HookType.PreCall,
                ta, "GameMain.OnInitialize",
                da, "CM3D2.ExternalSaveData.Managed.ExSaveData.DummyInitialize");

            // GameMain.Deserializeの処理後、CM3D2.略.GameMainCallbacks.Deserialize.Invokeを呼ぶ
            PatcherHelper.SetHook(
                PatcherHelper.HookType.PostCall,
                ta, "GameMain.Deserialize",
                da, "CM3D2.ExternalSaveData.Managed.GameMainCallbacks.Deserialize.Invoke");

            // GameMain.Deserializeの処理後、CM3D2.略.GameMainCallbacks.Serialize.Invokeを呼ぶ
            PatcherHelper.SetHook(
                PatcherHelper.HookType.PostCall,
                ta, "GameMain.Serialize",
                da, "CM3D2.ExternalSaveData.Managed.GameMainCallbacks.Serialize.Invoke");

            // GameMain.DeleteSerializeDataの処理後、CM3D2.略.GameMainCallbacks.DeleteSerializeData.Invokeを呼ぶ
            PatcherHelper.SetHook(
                PatcherHelper.HookType.PostCall,
                ta, "GameMain.DeleteSerializeData",
                da, "CM3D2.ExternalSaveData.Managed.GameMainCallbacks.DeleteSerializeData.Invoke");
        }
    }
}
