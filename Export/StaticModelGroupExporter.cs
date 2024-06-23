using Frosty.Core;
using FrostySdk.Ebx;
using FrostySdk.IO;
using FrostySdk.Managers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityLevelPlugin.Tools;

namespace UnityLevelPlugin.Export
{
    public class StaticModelGroupExporter : Exporter
    {
        public ULEStaticModelGroup group;

        // @TODO: Change to dynamic
        public override void Export(EbxAssetEntry entry)
        {
            EbxAsset asset = App.AssetManager.GetEbx(entry);
            // only one static model group per
            StaticModelGroupEntityData modelGroup = (StaticModelGroupEntityData)asset.Objects.FirstOrDefault((item) => item is StaticModelGroupEntityData);

            group = new ULEStaticModelGroup();
            if (modelGroup == null || modelGroup == default(StaticModelGroupEntityData))
            {
                return;
            }
            group = new ULEStaticModelGroup();
            ResAssetEntry resEntry = App.AssetManager.GetResEntry(entry.Name + "/staticmodelgroup_physics_win32");
            if (resEntry == null)
            {
                App.Logger.Log("Static model group not found: " + entry.Name + "/staticmodelgroup_physics_win32");
                //return;
            }
            List<ULTransform> physics = ((resEntry == null) ? new List<ULTransform>() : ULTools.GetPhysicsDataOfObjectBlueprint(context.plugin.outputFolder, resEntry.Name));
            App.Logger.Log(physics.Count.ToString());
            //context.PushTransform(ULTransform.FromLinearTransform(modelGroup.Transform));
            //if (resEntry == null)
            //{
            //    return;
            //}
            int index = 0;
            int memberindex = 0;
            foreach (StaticModelGroupMemberData member in modelGroup.MemberDatas)
            {
                ULObjectInstance instance = new ULObjectInstance();
                EbxAssetEntry memberType = App.AssetManager.GetEbxEntry(member.MemberType.External.FileGuid);
                EbxAssetEntry mesh = App.AssetManager.GetEbxEntry(member.MeshAsset.External.FileGuid);
                context.plugin.AddMesh(mesh);
                ULTools.FindAllReferencesOfType(memberType, "MeshAsset").ForEach(context.plugin.AddMesh);

                member.InstanceObjectVariation.ForEach((value) => instance.objectVariations.Add((value == 0) ? "Default" : (context.plugin.ObjectVariablesTable.TryGetValue(value, out var outEntry)) ? outEntry : "Default"));

                instance.objectBlueprint = new ULObjectBlueprint(mesh.Filename + ".fbx", mesh.Name);
                //if (member.PhysicsPartCountPerInstance > 0)
                if (member.PhysicsPartRange.First != 4294967295)
                {
                    // PhysicsPartRange
                    //for (int i = 0; i < ((member.PhysicsPartRange.Last - member.PhysicsPartRange.First + 1) / member.PhysicsPartCountPerInstance); i++)
                    for (int i = 0; i < member.InstanceCount; i++)
                    {
                        //if (index >= physics.Count)
                        //{
                        //    index = index;
                        //    App.Logger.Log(i.ToString());
                        //    App.Logger.Log(member.InstanceCount.ToString());
                        //    App.Logger.Log(entry.Name);
                        //    App.Logger.Log(memberindex.ToString());
                        //}
                        instance.transforms.Add(/*context.currentOffset.Peek() + */physics[index++]);
                    }
                }
                //else
                {
                    List<LinearTransform> transforms = member.InstanceTransforms;
                    foreach (var trans in transforms)
                    {
                        instance.transforms.Add(/*context.currentOffset.Peek() + */ ULTransform.FromLinearTransform(trans));
                    }
                }

                group.members.Add(instance);
                memberindex++;
            }
            //context.currentOffset.Pop();
        }

        public StaticModelGroupExporter(UnityLevelPlugin plugin) : base(plugin) { }
        public StaticModelGroupExporter(ExporterContext context) : base(context) { }
    }
}
