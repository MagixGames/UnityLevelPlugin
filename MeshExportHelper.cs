using Frosty.Core;
using Frosty.Core.Viewport;
using Frosty.Hash;
using FrostySdk.Ebx;
using FrostySdk.IO;
using FrostySdk.Managers;
using MeshSetPlugin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnityLevelPlugin
{
    public static class MeshExportHelper
    {
        // I could link this to Main.cs's mappings but im lazy and it adds like maybe 6s of computation
        public static Dictionary<uint, EbxAssetEntry> objectVariationMapping;

        public static void LoadInfo()
        {

        }

        // this code is a copy and paste from MeshSetPlugin because for some reason the function is private
        private static List<MeshSetVariationDetails> LoadVariations(EbxAssetEntry entry)
        {
            EbxAsset meshAsset = App.AssetManager.GetEbx(entry);
            //if (ProfilesLibrary.DataVersion != (int)ProfileVersion.Fifa19)
            dynamic RootObject = meshAsset.RootObject;
            dynamic ebxData = RootObject;
            MeshVariationDbEntry mvEntry = MeshVariationDb.GetVariations((entry as EbxAssetEntry).Guid);

            if (mvEntry != null)
            {
                if (objectVariationMapping.Count == 0)
                {
                    foreach (EbxAssetEntry varEntry in App.AssetManager.EnumerateEbx(type: "ObjectVariation"))
                        objectVariationMapping.Add((uint)Fnv1.HashString(varEntry.Name.ToLower()), varEntry);
                }

                List<MeshSetVariationDetails> detailsList = new List<MeshSetVariationDetails>();

                List<MeshVariation> mVariations = mvEntry.Variations.Values.ToList();
                mVariations.Sort((MeshVariation a, MeshVariation b) => { return a.AssetNameHash.CompareTo(b.AssetNameHash); });

                foreach (MeshVariation mv in mVariations)
                {
                    MeshSetVariationDetails variationDetails = new MeshSetVariationDetails { Name = "Default" };

                    if (objectVariationMapping.ContainsKey(mv.AssetNameHash))
                    {
                        EbxAsset asset = App.AssetManager.GetEbx(objectVariationMapping[mv.AssetNameHash]);
                        AssetClassGuid guid = ((dynamic)asset.RootObject).GetInstanceGuid();

                        variationDetails.Name = objectVariationMapping[mv.AssetNameHash].Filename;
                        variationDetails.Variation = new PointerRef(new EbxImportReference()
                        {
                            FileGuid = objectVariationMapping[mv.AssetNameHash].Guid,
                            ClassGuid = guid.ExportedGuid
                        });
                    }
                    else if (mv.AssetNameHash != 0)
                        continue;

                    for (int i = 0; i < ebxData.Materials.Count; i++)
                    {
                        dynamic material = ebxData.Materials[i].Internal;
                        AssetClassGuid guid = material.GetInstanceGuid();

                        MeshVariationMaterial varMaterial = mv.GetMaterial(guid.ExportedGuid);
                        if (varMaterial == null)
                        {
                            continue;
                        }
                        MeshSetMaterialDetails details = new MeshSetMaterialDetails();
                        variationDetails.MaterialCollection = new MeshMaterialCollection.Container(new MeshMaterialCollection(meshAsset, new PointerRef(varMaterial.MaterialVariationAssetGuid)));
                    }

                    foreach (Tuple<EbxImportReference, int> dbEntries in mv.DbLocations)
                        variationDetails.MeshVariationDbs.Add(new MeshSetVariationEntryDetails()
                        {
                            VariationDb = new PointerRef(dbEntries.Item1),
                            Index = dbEntries.Item2
                        });
                    detailsList.Add(variationDetails);
                    if (detailsList.Count == 1)
                        variationDetails.Preview = true;
                }

                return detailsList;
            }
            else
            {
                List<MeshSetVariationDetails> detailsList = new List<MeshSetVariationDetails>();
                MeshSetVariationDetails variationDetails = new MeshSetVariationDetails { Name = "Default" };

                for (int i = 0; i < ebxData.Materials.Count; i++)
                {
                    dynamic material = ebxData.Materials[i].Internal;
                    if (material == null)
                        continue;
                    MeshSetMaterialDetails details = new MeshSetMaterialDetails();
                    variationDetails.MaterialCollection = new MeshMaterialCollection.Container(new MeshMaterialCollection(meshAsset, new PointerRef()));
                }

                detailsList.Add(variationDetails);
                variationDetails.Preview = true;

                return detailsList;
            }
        }
    }
}
