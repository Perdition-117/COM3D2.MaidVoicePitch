using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace CM3D2.ExternalPreset.Patcher
{
    public static class ExternalPresetPatch
    { 
        public static readonly string[] TargetAssemblyNames = { "Assembly-CSharp.dll" };

        public static void Patch(AssemblyDefinition assembly)
        {
            var path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "COM3D2.ExternalPreset.Managed.dll");
            if (!File.Exists(path)) return;
            var typedef = AssemblyDefinition.ReadAssembly(path).MainModule.GetType("CM3D2.ExternalPreset.Managed.ExPreset");
            if (typedef == null) return;
            var type = assembly.MainModule.GetType("CharacterMgr");
            if (type == null) return;

            // CharacterMgr.PresetSetをフック
            // RETの前にExPreset.Load(f_maid, f_prest)を挿入
            var methPresetSet = type.Methods.FirstOrDefault((MethodDefinition meth) => meth.Name == "PresetSet" && meth.Parameters.Count == 2);
            if (methPresetSet == null) return;
            var il = methPresetSet.Body.GetILProcessor();
            var callLoad = il.Create(OpCodes.Call, methPresetSet.Module.ImportReference(PatcherHelper.GetMethod(typedef, "Load")));
            var ret = methPresetSet.Body.Instructions
                .Where(i => i.OpCode == OpCodes.Ret)
                .FirstOrDefault();
            if (ret == null) return;
            il.InsertBefore(ret, il.Create(OpCodes.Ldarg_1));
            il.InsertBefore(ret, il.Create(OpCodes.Ldarg_2));
            il.InsertBefore(ret, callLoad);

            // CharacterMgr.PresetSaveをフック
            // WriteAllBytes()直後にExPreset.Save(f_maid, text)を挿入
            var methPresetSave = type.Methods.FirstOrDefault((MethodDefinition meth) => meth.Name == "PresetSave");
            if (methPresetSave == null) return;
            il = methPresetSave.Body.GetILProcessor();
            var callSave = il.Create(OpCodes.Call, methPresetSave.Module.ImportReference(PatcherHelper.GetMethod(typedef, "Save")));
            var callWriteAllBytes = methPresetSave.Body.Instructions
                .Where(i => i.OpCode == OpCodes.Call && (i.Operand as MethodReference).Name == "WriteAllBytes")
                .FirstOrDefault();
            if (callWriteAllBytes == null) return;
            var stfldStrFileName = methPresetSave.Body.Instructions
                .Where(i => i.OpCode == OpCodes.Stfld && (i.Operand as FieldReference).Name == "strFileName")
                .FirstOrDefault();
            if (stfldStrFileName == null) return;
            il.InsertAfter(callWriteAllBytes, callSave);
            il.InsertAfter(callWriteAllBytes, il.Create(OpCodes.Ldarg_2));
            il.InsertAfter(callWriteAllBytes, il.Create(OpCodes.Ldloc_S, (stfldStrFileName.Previous.Operand as VariableDefinition)));
            il.InsertAfter(callWriteAllBytes, il.Create(OpCodes.Ldarg_1));

            // CharacterMgr.PresetDeleteをフック
            // File.Delete()直後にExPreset.Delete(f_prest)を挿入
            var methPresetDelete = type.Methods.FirstOrDefault((MethodDefinition meth) => meth.Name == "PresetDelete");
            if (methPresetDelete == null)
            {
                return;
            }
            
            il = methPresetDelete.Body.GetILProcessor();
            var callDelete = il.Create(OpCodes.Call, methPresetDelete.Module.ImportReference(PatcherHelper.GetMethod(typedef, "Delete")));
            var callFileDelete = methPresetDelete.Body.Instructions
                .Where(i => i.OpCode == OpCodes.Call && (i.Operand as MethodReference).Name == "Delete")
                .FirstOrDefault();
            if (callFileDelete == null)
            {
                return;
            }
            il.InsertAfter(callFileDelete, callDelete);
            il.InsertAfter(callFileDelete, il.Create(OpCodes.Ldarg_1));

            // 
            AssemblyDefinition ta = assembly;
            AssemblyDefinition da = PatcherHelper.GetAssemblyDefinition("COM3D2.ExternalPreset.Managed.dll");
            string m = "CM3D2.ExternalPreset.Managed.";

            PatcherHelper.SetHook(PatcherHelper.HookType.PostCall, ta, "CharacterMgr", "PresetSaveNotWriteFile", da, m + "ExPreset", "PostCharacterMgrPresetSaveNotWriteFile");
        }
    }
}
