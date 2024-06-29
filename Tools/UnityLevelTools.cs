using Frosty.Controls;
using Frosty.Core;
using Frosty.Core.Controls;
using Frosty.Core.Controls.Editors;
using Frosty.Core.Screens;
using Frosty.Core.Viewport;
using Frosty.Core.Windows;
using FrostySdk;
using FrostySdk.Ebx;
using FrostySdk.IO;
using FrostySdk.Managers;
using FrostySdk.Resources;
using MagixTools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using TexturePlugin;
using UnityLevelPlugin.Export;
using TextureType = FrostySdk.Resources.TextureType;
using D3D11 = SharpDX.Direct3D11;
using Frosty.Hash;

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
            // return App.AssetManager.GetResAs<HavokPhysicsData>(App.AssetManager.GetResEntry(fullPath + ((fullPath.EndsWith("_physics_win32")) ? "" : "_Physics_Win32")));
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
                float[] m = line.Split(';').Select(Convert.ToSingle).ToArray();
                ULTransform trns = ULTransform.FromMatrix4x4(
                                                            new Matrix4x4(m[0], m[1], m[2], m[3],
                                                                          m[4], m[5], m[6], m[7],
                                                                          m[8], m[9], m[10], m[11],
                                                            //new Matrix4x4(m[0], m[9], m[8], m[3],
                                                            //              m[6], m[5], m[4], m[7],
                                                            //              m[2], m[1], m[10], m[11],
                                                                        m[12], m[13], m[14], m[15]
                                                            )); 
                //trns.Scale = new Vector3(m[16], m[17], m[18]); // scale after the matrix /shrug
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

        #region Import Texture

        public static void ImportTexture(EbxAssetEntry assetEntry, string fileName)
        {
            EbxAsset asset = App.AssetManager.GetEbx(assetEntry);
            ulong resRid = ((dynamic)asset.RootObject).Resource;
            var textureAsset = App.AssetManager.GetResAs<Texture>(App.AssetManager.GetResEntry(resRid));

            FrostyTextureImportSettings settings = null;

            bool bFailed = false;
            string errorMsg = "";

            //FrostyTaskWindow.Show("Importing Texture", "", (task) =>
            {
                ImageFormat fmt = ImageFormat.PNG;
                MemoryStream memStream = null;
                BlobData blob = new BlobData();

                // convert other image types
                TextureImportOptions options = new TextureImportOptions
                {
                    type = textureAsset.Type,
                    format = TextureUtils.ToShaderFormat(textureAsset.PixelFormat, (textureAsset.Flags & TextureFlags.SrgbGamma) != 0),
                    generateMipmaps = textureAsset.MipCount > 1,
                    mipmapsFilter = 0,
                    resizeTexture = false,
                    resizeFilter = 0,
                    resizeHeight = 0,
                    resizeWidth = 0
                };

                if (textureAsset.Type == TextureType.TT_2d)
                {
                    // one image to one DDS
                    byte[] buf = NativeReader.ReadInStream(new FileStream(fileName, FileMode.Open, FileAccess.Read));
                    FrostyTextureEditor.ConvertImageToDDS(buf, buf.Length, fmt, options, ref blob);
                }
                else
                {
                    string[] fileNames = new string[settings.Textures.Count];
                    for (int i = 0; i < settings.Textures.Count; i++)
                        fileNames[i] = settings.Textures[i].Filename;

                    // multiple images to one DDS
                    byte[] buf = new byte[0];
                    long[] sizes = new long[fileNames.Length];

                    for (int i = 0; i < fileNames.Length; i++)
                    {
                        byte[] tmpBuf = NativeReader.ReadInStream(new FileStream(fileNames[i], FileMode.Open, FileAccess.Read));
                        sizes[i] = tmpBuf.Length;

                        Array.Resize<byte>(ref buf, buf.Length + tmpBuf.Length);
                        Array.Copy(tmpBuf, 0, buf, buf.Length - tmpBuf.Length, tmpBuf.Length);
                    }

                    FrostyTextureEditor.ConvertImagesToDDS(buf, sizes, sizes.Length, fmt, options, ref blob);
                }
                memStream = new MemoryStream(blob.Data);


                using (NativeReader reader = new NativeReader(memStream))
                {
                    TextureUtils.DDSHeader header = new TextureUtils.DDSHeader();
                    if (header.Read(reader))
                    {
                        TextureType type = TextureType.TT_2d;
                        if (header.HasExtendedHeader)
                        {
                            if (header.ExtendedHeader.resourceDimension == D3D11.ResourceDimension.Texture2D)
                            {
                                if ((header.ExtendedHeader.miscFlag & 4) != 0)
                                    type = TextureType.TT_Cube;
                                else if (header.ExtendedHeader.arraySize > 1)
                                    type = TextureType.TT_2dArray;
                            }
                            else if (header.ExtendedHeader.resourceDimension == D3D11.ResourceDimension.Texture3D)
                                type = TextureType.TT_3d;
                        }
                        else
                        {
                            if ((header.dwCaps2 & TextureUtils.DDSCaps2.CubeMap) != 0)
                                type = TextureType.TT_Cube;
                            else if ((header.dwCaps2 & TextureUtils.DDSCaps2.Volume) != 0)
                                type = TextureType.TT_3d;
                        }

                        if (type != textureAsset.Type)
                        {
                            errorMsg = $"Imported texture must match original texture type. Original texture type is {textureAsset.Type}. Imported texture type is {type}";
                            bFailed = true;
                        }

                        GetPixelFormat(header, textureAsset, out string pixelFormat, out TextureFlags baseFlags);

                        // make sure texture mip maps can be generated
                        if (TextureUtils.IsCompressedFormat(pixelFormat) && textureAsset.MipCount > 1)
                        {
                            if (header.dwWidth % 4 != 0 || header.dwHeight % 4 != 0)
                            {
                                errorMsg = "Texture width/height must be divisible by 4 for compressed formats requiring mip maps";
                                bFailed = true;
                            }
                        }

                        if (!bFailed)
                        {
                            ResAssetEntry resEntry = App.AssetManager.GetResEntry(resRid);
                            ChunkAssetEntry chunkEntry = App.AssetManager.GetChunkEntry(textureAsset.ChunkId);

                            // revert any modifications
                            //App.AssetManager.RevertAsset(resEntry, dataOnly: true);

                            byte[] buffer = new byte[reader.Length - reader.Position];
                            reader.Read(buffer, 0, (int)(reader.Length - reader.Position));

                            ushort depth = (header.HasExtendedHeader && header.ExtendedHeader.resourceDimension == D3D11.ResourceDimension.Texture2D)
                                    ? (ushort)header.ExtendedHeader.arraySize
                                    : (ushort)1;

                            // cubemaps are just 6 slice arrays
                            if ((header.dwCaps2 & TextureUtils.DDSCaps2.CubeMap) != 0)
                                depth = 6;
                            if ((header.dwCaps2 & TextureUtils.DDSCaps2.Volume) != 0)
                                depth = (ushort)header.dwDepth;

                            Texture newTextureAsset = new Texture(textureAsset.Type, pixelFormat, (ushort)header.dwWidth, (ushort)header.dwHeight, depth) { FirstMip = textureAsset.FirstMip };
                            if (header.dwMipMapCount <= textureAsset.FirstMip)
                                newTextureAsset.FirstMip = 0;

                            newTextureAsset.TextureGroup = textureAsset.TextureGroup;
                            newTextureAsset.CalculateMipData((byte)header.dwMipMapCount, TextureUtils.GetFormatBlockSize(pixelFormat), TextureUtils.IsCompressedFormat(pixelFormat), (uint)buffer.Length);
                            newTextureAsset.Flags = baseFlags;

                            // just copy old flags (minus gamma) to new texture
                            TextureFlags oldFlags = textureAsset.Flags & ~(TextureFlags.SrgbGamma);
                            newTextureAsset.Flags |= oldFlags;

                            // rejig mips/slices
                            if (newTextureAsset.Type == TextureType.TT_Cube || newTextureAsset.Type == TextureType.TT_2dArray)
                            {
                                MemoryStream srcStream = new MemoryStream(buffer);
                                MemoryStream dstStream = new MemoryStream();

                                int sliceCount = 6;
                                if (newTextureAsset.Type == TextureType.TT_2dArray)
                                    sliceCount = newTextureAsset.Depth;

                                // Need to rejig order of faces and mips
                                uint[] mipOffsets = new uint[newTextureAsset.MipCount];
                                for (int i = 0; i < newTextureAsset.MipCount - 1; i++)
                                    mipOffsets[i + 1] = mipOffsets[i] + (uint)(newTextureAsset.MipSizes[i] * sliceCount);

                                byte[] tmpBuf = new byte[newTextureAsset.MipSizes[0]];

                                for (int slice = 0; slice < sliceCount; slice++)
                                {
                                    for (int mip = 0; mip < newTextureAsset.MipCount; mip++)
                                    {
                                        int mipSize = (int)newTextureAsset.MipSizes[mip];

                                        srcStream.Read(tmpBuf, 0, mipSize);
                                        dstStream.Position = mipOffsets[mip] + (mipSize * slice);
                                        dstStream.Write(tmpBuf, 0, mipSize);
                                    }
                                }

                                buffer = dstStream.ToArray();
                            }

                            // modify chunk
                            if (ProfilesLibrary.MustAddChunks && chunkEntry.Bundles.Count == 0 && !chunkEntry.IsAdded)
                            {
                                // DAI requires adding new chunks if in chunks bundle
                                textureAsset.ChunkId = App.AssetManager.AddChunk(buffer, null, (newTextureAsset.Flags & TextureFlags.OnDemandLoaded) != 0 ? null : newTextureAsset);
                                chunkEntry = App.AssetManager.GetChunkEntry(textureAsset.ChunkId);
                            }
                            else
                            {
                                // other games just modify
                                App.AssetManager.ModifyChunk(textureAsset.ChunkId, buffer, ((newTextureAsset.Flags & TextureFlags.OnDemandLoaded) != 0 || newTextureAsset.Type != TextureType.TT_2d) ? null : newTextureAsset);
                            }

                            for (int i = 0; i < 4; i++)
                                newTextureAsset.Unknown3[i] = textureAsset.Unknown3[i];
                            newTextureAsset.SetData(textureAsset.ChunkId, App.AssetManager);
                            newTextureAsset.AssetNameHash = (uint)Fnv1.HashString(resEntry.Name);

                            textureAsset.Dispose();
                            textureAsset = newTextureAsset;

                            // modify resource
                            App.AssetManager.ModifyRes(resRid, newTextureAsset);

                            // update linkage
                            resEntry.LinkAsset(chunkEntry);
                            assetEntry.LinkAsset(resEntry);
                        }
                    }
                    else
                    {
                        errorMsg = string.Format("Invalid DDS format");
                        bFailed = true;
                    }
                }

                FrostyTextureEditor.ReleaseBlob(blob);
            }
        }

        private static void GetPixelFormat(TextureUtils.DDSHeader header, Texture textureAsset, out string pixelFormat, out TextureFlags flags)
        {
            pixelFormat = "Unknown";
            flags = 0;

            if (ProfilesLibrary.DataVersion == (int)ProfileVersion.DragonAgeInquisition || ProfilesLibrary.DataVersion == (int)ProfileVersion.Battlefield4 || ProfilesLibrary.DataVersion == (int)ProfileVersion.PlantsVsZombiesGardenWarfare)
            {
                // DXT1
                if (header.ddspf.dwFourCC == 0x31545844)
                {
                    pixelFormat = "BC1_UNORM";
                    if (textureAsset.PixelFormat.Contains("Normal"))
                        pixelFormat = textureAsset.PixelFormat;
                    else if (textureAsset.PixelFormat.StartsWith("BC1A"))
                        pixelFormat = textureAsset.PixelFormat;
                }

                // ATI2 or BC5U
                else if (header.ddspf.dwFourCC == 0x32495441 || header.ddspf.dwFourCC == 0x55354342)
                    pixelFormat = "NormalDXN";

                // DXT3
                else if (header.ddspf.dwFourCC == 0x33545844)
                    pixelFormat = "BC2_UNORM";

                // DXT5
                else if (header.ddspf.dwFourCC == 0x35545844)
                    pixelFormat = "BC3_UNORM";

                // ATI1
                else if (header.ddspf.dwFourCC == 0x31495441)
                    pixelFormat = "BC3A_UNORM";

                // All others
                else if (header.HasExtendedHeader)
                {
                    switch (header.ExtendedHeader.dxgiFormat)
                    {
                        case SharpDX.DXGI.Format.R32G32B32A32_Float: pixelFormat = "ARGB32F"; break;
                        case SharpDX.DXGI.Format.R9G9B9E5_Sharedexp: pixelFormat = "R9G9B9E5F"; break;
                        case SharpDX.DXGI.Format.R8_UNorm: pixelFormat = "L8"; break;
                        case SharpDX.DXGI.Format.R16_UNorm: pixelFormat = "L16"; break;
                        case SharpDX.DXGI.Format.R8G8B8A8_UNorm: pixelFormat = "ARGB8888"; break;
                        case SharpDX.DXGI.Format.BC1_UNorm:
                            pixelFormat = "BC1_UNORM";
                            if (textureAsset.PixelFormat.Contains("Normal") || textureAsset.PixelFormat.StartsWith("BC1A"))
                                pixelFormat = textureAsset.PixelFormat;
                            break;
                        case SharpDX.DXGI.Format.BC2_UNorm: pixelFormat = "BC2_UNORM"; break;
                        case SharpDX.DXGI.Format.BC3_UNorm: pixelFormat = "BC3_UNORM"; break;
                        case SharpDX.DXGI.Format.BC5_UNorm: pixelFormat = "NormalDXN"; break;
                        case SharpDX.DXGI.Format.BC7_UNorm: pixelFormat = "BC7_UNORM"; break;
                        case SharpDX.DXGI.Format.BC1_UNorm_SRgb: pixelFormat = "BC1_UNORM"; flags = TextureFlags.SrgbGamma; break;
                        case SharpDX.DXGI.Format.BC2_UNorm_SRgb: pixelFormat = "BC2_UNORM"; flags = TextureFlags.SrgbGamma; break;
                        case SharpDX.DXGI.Format.BC3_UNorm_SRgb:
                            pixelFormat = (textureAsset.PixelFormat == "BC3A_UNORM") ? textureAsset.PixelFormat : "BC3_UNORM";
                            flags = TextureFlags.SrgbGamma;
                            break;
                        case SharpDX.DXGI.Format.BC7_UNorm_SRgb: pixelFormat = "BC7_UNORM"; flags = TextureFlags.SrgbGamma; break;
                    }
                }
            }
            else
            {
                // Newer format PixelFormats
                if (header.ddspf.dwFourCC == 0)
                {
                    if (header.ddspf.dwRBitMask == 0x000000FF && header.ddspf.dwGBitMask == 0x0000FF00 && header.ddspf.dwBBitMask == 0x00FF0000 && header.ddspf.dwABitMask == 0xFF000000)
                        pixelFormat = "R8G8B8A8_UNORM";
                }

                // DXT1
                else if (header.ddspf.dwFourCC == 0x31545844)
                {
                    pixelFormat = "BC1_UNORM";
                    if (textureAsset.PixelFormat == "BC1A_UNORM")
                        pixelFormat = "BC1A_UNORM";
                }

                // DXT5
                else if (header.ddspf.dwFourCC == 0x35545844)
                    pixelFormat = "BC3_UNORM";

                // ATI1
                else if (header.ddspf.dwFourCC == 0x31495441)
                    pixelFormat = "BC4_UNORM";

                // ATI2 or BC5U
                else if (header.ddspf.dwFourCC == 0x32495441 || header.ddspf.dwFourCC == 0x55354342)
                    pixelFormat = "BC5_UNORM";

                // All others
                else if (header.HasExtendedHeader)
                {
                    if (header.ExtendedHeader.dxgiFormat == SharpDX.DXGI.Format.BC1_UNorm)
                    {
                        pixelFormat = "BC1_UNORM";
                        if (textureAsset.PixelFormat == "BC1A_UNORM")
                            pixelFormat = "BC1A_UNORM";
                    }
                    else if (header.ExtendedHeader.dxgiFormat == SharpDX.DXGI.Format.BC3_UNorm)
                        pixelFormat = "BC3_UNORM";
                    else if (header.ExtendedHeader.dxgiFormat == SharpDX.DXGI.Format.BC4_UNorm)
                        pixelFormat = "BC4_UNORM";
                    else if (header.ExtendedHeader.dxgiFormat == SharpDX.DXGI.Format.BC5_UNorm)
                        pixelFormat = "BC5_UNORM";
                    else if (header.ExtendedHeader.dxgiFormat == SharpDX.DXGI.Format.BC1_UNorm_SRgb && textureAsset.PixelFormat == "BC1A_SRGB")
                        pixelFormat = "BC1A_SRGB";
                    else if (header.ExtendedHeader.dxgiFormat == SharpDX.DXGI.Format.BC1_UNorm_SRgb)
                        pixelFormat = "BC1_SRGB";
                    else if (header.ExtendedHeader.dxgiFormat == SharpDX.DXGI.Format.BC3_UNorm_SRgb)
                        pixelFormat = "BC3_SRGB";
                    else if (header.ExtendedHeader.dxgiFormat == SharpDX.DXGI.Format.BC6H_Uf16)
                        pixelFormat = "BC6U_FLOAT";
                    else if (header.ExtendedHeader.dxgiFormat == SharpDX.DXGI.Format.BC7_UNorm)
                        pixelFormat = "BC7_UNORM";
                    else if (header.ExtendedHeader.dxgiFormat == SharpDX.DXGI.Format.BC7_UNorm_SRgb)
                        pixelFormat = "BC7_SRGB";
                    else if (header.ExtendedHeader.dxgiFormat == SharpDX.DXGI.Format.R8_UNorm)
                        pixelFormat = "R8_UNORM";
                    else if (header.ExtendedHeader.dxgiFormat == SharpDX.DXGI.Format.R16G16B16A16_Float)
                        pixelFormat = "R16G16B16A16_FLOAT";
                    else if (header.ExtendedHeader.dxgiFormat == SharpDX.DXGI.Format.R32G32B32A32_Float)
                        pixelFormat = "R32G32B32A32_FLOAT";
                    else if (header.ExtendedHeader.dxgiFormat == SharpDX.DXGI.Format.R9G9B9E5_Sharedexp)
                        pixelFormat = "R9G9B9E5_FLOAT";
                    else if (header.ExtendedHeader.dxgiFormat == SharpDX.DXGI.Format.R8G8B8A8_UNorm)
                        pixelFormat = "R8G8B8A8_UNORM";
                    else if (header.ExtendedHeader.dxgiFormat == SharpDX.DXGI.Format.R8G8B8A8_UNorm_SRgb)
                        pixelFormat = "R8G8B8A8_SRGB";
                    else if (header.ExtendedHeader.dxgiFormat == SharpDX.DXGI.Format.R10G10B10A2_UNorm)
                        pixelFormat = "R10G10B10A2_UNORM";
                    else if (header.ExtendedHeader.dxgiFormat == SharpDX.DXGI.Format.R16_UNorm)
                    {
                        pixelFormat = "R16_UNORM";
                        if (textureAsset.PixelFormat == "D16_UNORM")
                            pixelFormat = "D16_UNORM";
                    }
                }
            }
        }

        #endregion

    }
}
