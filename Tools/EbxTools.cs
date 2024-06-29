using FrostySdk.Ebx;
using FrostySdk.IO;
using FrostySdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Frosty.Core;
using FrostySdk.Managers;

namespace UnityLevelPlugin.Tools;

internal class EbxTools
{
    public static dynamic CreateNewClass(string type, EbxAsset parentAsset)
    {
        dynamic KitList = TypeLibrary.CreateObject(type);
        AssetClassGuid guid = new AssetClassGuid(Utils.GenerateDeterministicGuid(parentAsset.Objects, (Type)parentAsset.GetType(), parentAsset.FileGuid), -1);
        KitList.SetInstanceGuid(guid);
        parentAsset.AddObject(KitList, true);
        return KitList;
    }

    public static EbxAsset CreateEbxAsset(string type)
    {
        EbxAsset asset = new EbxAsset(TypeLibrary.CreateObject(type));
        Guid fileGuid;
        do
        {
            fileGuid = Guid.NewGuid();
        } while (App.AssetManager.GetEbxEntry(fileGuid) is not null);
        asset.SetFileGuid(Guid.NewGuid());
        return asset;
    }

    public static PointerRef RefToFile(EbxAssetEntry entry) => RefToFile(App.AssetManager.GetEbx(entry));

    public static PointerRef DeepRef(EbxAssetEntry entry, Type type) =>
        new PointerRef(new EbxImportReference
        { 
            FileGuid = entry.Guid,
            ClassGuid = ((dynamic)App.AssetManager.GetEbx(entry)
                .ExportedObjects.First((o)=>o.GetType() == type)).GetInstanceGuid().ExportedGuid
        });

    public static PointerRef RefToFile(EbxAsset asset) =>
            new PointerRef(new EbxImportReference { FileGuid = asset.FileGuid, ClassGuid = asset.RootInstanceGuid });


    public static (GroupRigidBodyData, GroupRigidBodyData) CreateSMGPhysics
        (EbxAsset asset, ulong resId)
    {
        GroupHavokAsset havok = CreateNewClass(nameof(GroupHavokAsset), asset);
        havok.Resource = new ResourceRef(resId);

        GroupRigidBodyData group1 = CreateNewClass("GroupRigidBodyData", asset);
        group1.Flags = 0;
        group1.Realm = Realm.Realm_ClientAndServer;
        group1.RigidBodyType = RigidBodyType.RBTypeGroup;
        group1.CollisionLayer = RigidBodyCollisionLayer.RigidBodyCollisionLayer_StaticLayer;
        group1.MotionType = RigidBodyMotionType.RigidBodyMotionType_Invalid;
        group1.QualityType = RigidBodyQualityType.RigidBodyQualityType_Fixed;
        group1.RaycastRootShapeIndex = 1;
        group1.AddToSpatialQueryManager = true;
        group1.PhysicsCallbackHandler = CreateNewClass(nameof(DefaultPhysicsCallbackHandlerData), asset);
        group1.Material = new MaterialDecl() { Packed = 2048 };
        group1.DynamicFriction = -1;
        group1.StaticFriction = -1;
        group1.Restitution = -1;
        group1.ComputeCenterOfMass = true;
        group1.ComputeInertiaTensor = true;
        group1.InertiaModifier = new Vec3() { x = 1, y = 1, z = 1 };
        group1.AngularVelocityDamping = -1;
        group1.LinearVelocityDamping = -1;
        group1.IsRootController = true;
        group1.InverseInertiaTensor = new Vec3() { x = 1, y = 1, z = 1 };
        group1.PrincipalAxesOfInertia = new Quat() { x = 0, y = 0, z = 0, w = 1 };
        // Not dealing with this
        //group1.RaycastMaterialIndicesLookups

        GroupRigidBodyData group2 = CreateNewClass("GroupRigidBodyData", asset);
        group2.Flags = 0;
        group2.Realm = Realm.Realm_Client;
        group2.RigidBodyType = RigidBodyType.RBTypeProxy;
        group2.CollisionLayer = RigidBodyCollisionLayer.RigidBodyCollisionLayer_StaticLayer;
        group2.MotionType = RigidBodyMotionType.RigidBodyMotionType_Invalid;
        group2.QualityType = RigidBodyQualityType.RigidBodyQualityType_Fixed;
        group2.WorldIndex = 1;
        group2.RaycastRootShapeIndex = 1;
        group2.AddToSpatialQueryManager = true;
        group2.PhysicsCallbackHandler = CreateNewClass(nameof(DefaultPhysicsCallbackHandlerData), asset);
        group2.Material = new MaterialDecl() { Packed = 2048 };
        group2.DynamicFriction = -1;
        group2.StaticFriction = -1;
        group2.Restitution = -1;
        group2.ComputeCenterOfMass = true;
        group2.ComputeInertiaTensor = true;
        group2.InertiaModifier = new Vec3() { x = 1, y = 1, z = 1 };
        group2.AngularVelocityDamping = -1;
        group2.LinearVelocityDamping = -1;
        group2.IsRootController = true;
        group2.InverseInertiaTensor = new Vec3() { x = 1, y = 1, z = 1 };
        group2.PrincipalAxesOfInertia = new Quat() { x = 0, y = 0, z = 0, w = 1 };

        group1.Asset = new PointerRef(havok);
        group2.Asset = new PointerRef(havok);

        return (group1, group2);
    }
}
