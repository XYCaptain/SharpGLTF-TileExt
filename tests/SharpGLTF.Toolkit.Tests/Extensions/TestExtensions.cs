﻿using Newtonsoft.Json;
using NUnit.Framework;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.Parametric;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Scenes;
using SharpGLTF.Schema2;
using SharpGLTF.Transforms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SharpGLTF.Extensions
{
    internal class TestExtensions
    {
        [Test]
        public void TestGltf()
        {
            var scene = new SharpGLTF.Scenes.SceneBuilder();

            var material = new MaterialBuilder("material1")
                .WithDoubleSide(false)
                .WithMetallicRoughnessShader()
                .WithBaseColor(new Vector4(0.5f, 0.3f, 0.4f, 0.8f));

            var material1 = new MaterialBuilder("material1")
                .WithDoubleSide(false)
                .WithMetallicRoughnessShader()
                .WithBaseColor(new Vector4(0.6f, 0.8f, 0.7f, 1f));

            var mesh = new MeshBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty>();
            mesh.AddCube(material, Matrix4x4.Identity);
            mesh.AddCube(material1, Matrix4x4.CreateTranslation(0.5f, 0.5f, 0.5f));

            var root = new NodeBuilder("root");
            scene.AddRigidMesh(mesh, root);
            var node1 = new NodeBuilder("instance");
            scene.AddRigidMesh(mesh, node1, AffineTransform.CreateFromAny(null, new Vector3(5, 5, 5), null, new Vector3(20, 20, 20)));
            var model = scene.ToGltf2();

            List<AffineTransform> ass = new List<AffineTransform>();
            var rand = new Random();
            List<object> fids = new List<object>();
            List<building> bs = new List<building>();

            for (int i = 0; i < 1; i++)
            {
                fids.Add(i);
                bs.Add(new building() { height = i });
                ass.Add(AffineTransform.CreateFromAny(null, new Vector3(1 * (float)rand.NextDouble() + 0.5f, 1 * (float)rand.NextDouble() + 0.5f, 1 * (float)rand.NextDouble() + 0.5f), null, new Vector3(10 * (float)rand.NextDouble(), 10 * (float)rand.NextDouble(), 10 * (float)rand.NextDouble())));
            }

            model.LogicalNodes.Where(x => x.Name == "instance").FirstOrDefault()
                .UseGpuInstancing().WithInstanceAccessors(ass)
                .WithInstanceCustomAccessor("FEATURE_ID_0", fids);
            model.LogicalNodes.Where(x => x.Name == "instance").FirstOrDefault()
                .UseFeatureMetadata().SetFeatureData(0, new featureid() { attribute = 0 });

            model.LogicalNodes.Where(x => x.Name == "root").FirstOrDefault()
                 .UseFeatureMetadata().SetFeatureData(0, new featureid() { attribute = 0 }).SetFeatureData(0, new featureid() { offset = 0, repeat = 2 });


            var f = model.UseFeatureMetadata();
            f.WithFeatureAccessors(bs);
            var text = "{\"classes\":{\"building\":{\"name\":\"building\",\"properties\":{\"height\":{\"name\":\"height\",\"type\":\"INT8\",\"componentType\":0,\"componentCount\":0,\"normalized\":false,\"max\":0.0,\"min\":0.0,\"default\":0.0,\"optional\":false}}}}}";
            f.SetShcema(text);
            model.SetExtension(f);

            model.SaveGLTF(@$"preview.gltf", new WriteSettings() { MergeBuffers = false });
            model.SaveGLB(@$"preview.glb", new WriteSettings() { MergeBuffers = false });
        }
    }

    public class building
    {
        [JsonProperty("height")]
        public float height { get; set; }
        [JsonProperty("height")]
        public long id { get; set; }
    }
}
