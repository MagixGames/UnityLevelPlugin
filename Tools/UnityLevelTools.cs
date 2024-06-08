using Frosty.Core;
using FrostySdk;
using FrostySdk.Ebx;
using FrostySdk.Managers;
using MagixTools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityLevelPlugin.Export;

namespace UnityLevelPlugin.Tools
{
    internal static class ULTools
    {
        public static List<EbxAssetEntry> FindAllReferencesOfType(EbxAssetEntry entry, string typeName)
        {
            return (from refGuid in entry.EnumerateDependencies()
                    select App.AssetManager.GetEbxEntry(refGuid) into refEntry
                    where TypeLibrary.IsSubClassOf(refEntry.Type, typeName)
                    select refEntry).ToList();
        }


        public static List<ULTransform> GetPhysicsDataOfObjectBlueprint(DirectoryInfo outputFolder, string fullPath)
        {
            // Every object will have the same naming convention of just having "_Physics_Win32" at the end
            // so to speed it up we can just do that
            //return App.AssetManager.GetResAs<HavokPhysicsData>(App.AssetManager.GetResEntry(fullPath + ((fullPath.EndsWith("_physics_win32")) ? "" : "_Physics_Win32")));
            string outFile = ToFile(outputFolder, "havokinfo");
            HavokInterface.ExportTranslationInformation(outFile, App.AssetManager.GetResEntry(fullPath + ((fullPath.EndsWith("_physics_win32")) ? "" : "_Physics_Win32")));
            //Thread.Sleep(100);
            outFile = outFile + ".txt";
            string fileText = File.ReadAllText(outFile);
            string[] lines = fileText.Split('\n');
            lines[lines.Length - 1] = lines[0]; // fix crash
            List<ULTransform> result = new List<ULTransform>();
            foreach (string line in lines)
            {
                float[] m = line.Split(';').Select(s => Convert.ToSingle(s)).ToArray();
                ULTransform trns = ULTransform.FromMatrix4x4(
                                              new Matrix4x4(m[0], m[1], m[2], m[3],
                                                            m[4], m[5], m[6], m[7],
                                                            m[8], m[9], m[10], m[11],
                                                            //new Matrix4x4(m[0], m[9], m[8], m[3],
                                                            //              m[6], m[5], m[4], m[7],
                                                            //              m[2], m[1], m[10], m[11],
                                                            m[12], m[13], m[14], m[15]
                                              ));
                trns.Scale = new Vector3(m[16], m[17], m[18]); // scale after the matrix /shrug
                result.Add(trns);
                              //new Matrix4x4(m[0], m[1], m[2], m[3],
                              //              m[4], m[5], m[6], m[7],
                              //              m[8], m[9], m[10], m[11],
                              //              m[12], m[13], m[14], m[15]
                              //              )));
                              //new Matrix4x4(m[0], m[1], m[2], 0,
                              //              m[4], m[5], m[6], 0,
                              //              m[8], m[9], m[10], 0,
                              //              m[12], m[13], m[14], 1 
                              //              )));
                            //new Matrix4x4(m[0], m[1], m[2], 0,
                            //              m[4], m[5], m[6], 0,
                            //              m[8], m[9], m[10], 0,
                            //              m[3], m[7], m[11], 1
                            //              )));
            }
            return result;

            //StaticModelPhysicsComponentData componentData = (StaticModelPhysicsComponentData)App.AssetManager.GetEbx(
            //                                                (from refGuid in bp.Objects
            //                                                 select App.AssetManager.GetEbxEntry(refGuid.External.FileGuid) into refEntry
            //                                                 where TypeLibrary.IsSubClassOf(refEntry.Type, "StaticModelPhysicsComponentData")
            //                                                 select refEntry).First()).RootObject;
            //RigidBodyData bodyData = componentData.PhysicsBodies
        }

        public static string ToFile(DirectoryInfo outputFolder, string path)
        {
            string outPath = Path.Combine(outputFolder.FullName, path);

            try
            {
                Directory.CreateDirectory(new FileInfo(outPath).Directory.FullName); // ensure it exists
            }
            catch (Exception _) { }

            return outPath;
        }
    }
}
