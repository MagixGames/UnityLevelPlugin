using FrostySdk;
using FrostySdk.Ebx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using UnityLevelPlugin.Export;

namespace UnityLevelPlugin.Import;

public partial class Importer
{
    public void ImportComponents()
    {
        task.Update("Importing components");
        for (int i = 0, componentCount = context.plugin.importedSpatial.componentGroup.components.Count; i < componentCount; i++)
        {
            ULComponent inst = context.plugin.importedSpatial.componentGroup.components[i];
            task.Progress = (double)i / componentCount;
            switch (inst.type)
            {
                case "OBBCollision":
                    {
                        ULTransform trans = inst.transform;
                        Vec3 halfExtent = new Vec3() { x = trans.Scale.X / 2, y = trans.Scale.Y / 2, z = trans.Scale.Z / 2 };
                        trans.Scale = new System.Numerics.Vector3(1);
                        LinearTransform lt = trans.ToLinearTransform();

                        OBBCollisionEntityData obb = CreateRootClass(nameof(OBBCollisionEntityData));
                        obb.Transform = lt;
                        obb.HalfExtents = halfExtent;
                        // const
                        obb.Enabled = true;
                        obb.Material = new MaterialDecl() { Packed = 2048 };
                        obb.CollisionLayer = RigidBodyCollisionLayer.RigidBodyCollisionLayer_StaticLayer;

                        RigidBodyData rbd = CreateClass(nameof(RigidBodyData));
                        #region RigidBodyData creation

                        rbd.Realm = (dynamic)Enum.Parse(TypeLibrary.GetType("Realm"), "Realm_ClientAndServer");
                        rbd.RigidBodyType = (dynamic)Enum.Parse(TypeLibrary.GetType("RigidBodyType"), "RBTypeCollision");
                        rbd.CollisionLayer = (dynamic)Enum.Parse(TypeLibrary.GetType("RigidBodyCollisionLayer"), "RigidBodyCollisionLayer_StaticLayer");
                        rbd.MotionType = (dynamic)Enum.Parse(TypeLibrary.GetType("RigidBodyMotionType"), "RigidBodyMotionType_Fixed");
                        rbd.QualityType = (dynamic)Enum.Parse(TypeLibrary.GetType("RigidBodyQualityType"), "RigidBodyQualityType_Fixed");
                        rbd.AddToSpatialQueryManager = true;
                        rbd.ComputeInertiaTensor = true;
                        rbd.ComputeCenterOfMass = true;
                        rbd.IsRootController = true;
                        rbd.DynamicFriction = -1;
                        rbd.StaticFriction = -1;
                        rbd.Restitution = -1;
                        rbd.AngularVelocityDamping = -1;
                        rbd.LinearVelocityDamping = -1;
                        rbd.InverseInertiaTensor.x = 1f;
                        rbd.InverseInertiaTensor.y = 1f;
                        rbd.InverseInertiaTensor.z = 1f;
                        rbd.InertiaModifier.x = 1f;
                        rbd.InertiaModifier.y = 1f;
                        rbd.InertiaModifier.z = 1f;
                        rbd.PrincipalAxesOfInertia.w = 1f;
                        rbd.PartIndices = new List<uint>() { 0 };
                        rbd.Material = TypeLibrary.CreateObject("MaterialDecl");
                        rbd.Material.Packed = 2048;
                        rbd.PhysicsCallbackHandler = new PointerRef(CreateClass("DefaultPhysicsCallbackHandlerData"));
                        rbd.WorldIndex = 0;// (byte)i;

                        #endregion
                        obb.PhysicsBodies.Add(new PointerRef(rbd));
                    }
                    break;
                case "OccluderVolume":
                    {
                        OccluderVolumeEntityData occ = CreateRootClass(nameof(OccluderVolumeEntityData));

                        occ.Transform = inst.transform.ToLinearTransform();

                        // const
                        occ.Visible = true; // always true? idk what "visible" means here though
                        occ.OccluderIsConservative = true;
                    }
                    break;
                default: { } break; // Older version or unsupported component type
            }
        }
    }
}
