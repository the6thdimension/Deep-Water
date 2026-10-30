using System;
using System.Linq;
using DeepWater.Metoc.Hdrp;
using UnityEditor;
using UnityEditor.VFX;
using UnityEditor.VFX.Block;
using UnityEngine;
using UnityEngine.VFX;

namespace DeepWater.Metoc.Editor.RainVfx
{
    /// <summary>
    /// Builds METOC's rain graph, <c>MetocRain.vfx</c> (slice 3, E53), from code: Deep Water → METOC → Build Rain VFX.
    /// The graph is then an ordinary asset, reviewable as this C# and rebuilt the same way every time, rather than
    /// wired by hand in the graph window.
    /// <para>
    /// Why this assembly carries Unity's name: VFX Graph's graph model (VFXGraph, contexts, blocks, slots) is internal
    /// to <c>Unity.VisualEffectGraph.Editor</c>, which grants its internals to a list of Unity's own test assemblies
    /// (the package's Editor/PackageInfo.cs). No package in this project uses the name
    /// <c>Unity.Testing.VisualEffectGraph.Editor</c>, so this editor-only assembly takes it and builds the graph
    /// without a vendor edit. Nothing at run time depends on it: <see cref="MetocRainVfx"/> drives the built asset
    /// through its exposed properties. If <c>com.unity.testing.visualeffectgraph</c> is ever added to the project,
    /// rename this assembly to another granted name.
    /// </para>
    /// <para>
    /// The graph: a constant-rate spawner; world-space drops born in a box centred on the effect's GameObject, with a
    /// velocity, size, stretch, colour and opacity; an update that moves them and wraps them inside the box (so the box
    /// can follow the camera without emptying); a soft quad per drop, stretched along its velocity and faded near the
    /// camera.
    /// </para>
    /// </summary>
    static class MetocRainVfxBuilder
    {
        [MenuItem("Deep Water/METOC/Build Rain VFX")]
        public static void Build()
        {
            string path = MetocRainVfx.AssetPath;
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            VisualEffectAssetEditorUtility.CreateNewAsset(path);   // writes an empty graph, replacing any old one
            VisualEffectResource resource = VisualEffectResource.GetResourceAtPath(path);
            if (resource == null) throw new InvalidOperationException($"[METOC] no VisualEffectResource at {path}");
            VFXGraph graph = resource.GetOrCreateGraph();

            // Contexts, linked spawner → initialize → update → output.
            var spawner = Add<VFXBasicSpawner>(graph, new Vector2(0, -400));
            var init = Add<VFXBasicInitialize>(graph, new Vector2(0, 0));
            var update = Add<VFXBasicUpdate>(graph, new Vector2(0, 700));
            var output = Add<VFXPlanarPrimitiveOutput>(graph, new Vector2(0, 1100));
            LinkContexts(spawner, init);
            LinkContexts(init, update);
            LinkContexts(update, output);

            // The particle data the three share: world space, a fixed capacity, bounds set from the box.
            init.space = VFXSpace.World;
            VFXData data = init.GetData();
            data.SetSettingValue("capacity", MetocRainVfx.Capacity);
            data.SetSettingValue("boundsMode", BoundsSettingMode.Manual);

            // Exposed properties (the driver writes them every frame).
            var rate = Parameter(graph, MetocRainVfx.Rate, 0f, -800, -400);
            var lifetime = Parameter(graph, MetocRainVfx.Lifetime, 1f, -800, -300);
            var boxSize = Parameter(graph, MetocRainVfx.BoxSize, new Vector3(40f, 24f, 40f), -800, -100);
            var velocity = Parameter(graph, MetocRainVfx.Velocity, new Vector3(0f, -7f, 0f), -800, 0);
            var dropWidth = Parameter(graph, MetocRainVfx.DropWidth, 0.004f, -800, 100);
            var stretch = Parameter(graph, MetocRainVfx.Stretch, 60f, -800, 200);
            var color = Parameter(graph, MetocRainVfx.Color, new Vector3(1f, 1f, 1f), -800, 300);
            var alpha = Parameter(graph, MetocRainVfx.Alpha, 0.2f, -800, 400);
            var fadedDistance = Parameter(graph, MetocRainVfx.FadedDistance, 1f, -800, 1200);
            var visibleDistance = Parameter(graph, MetocRainVfx.VisibleDistance, 4f, -800, 1300);

            // Spawner.
            var constantRate = Block<VFXSpawnerConstantRate>(spawner);
            Link(rate, Input(constantRate, "Rate"));

            // Initialize: bounds, then the drop's attributes. The box's slots are in the effect's own space, so every box
            // is centred on the GameObject and follows it (the driver moves the object to the camera). A world centre
            // linked here would count the object's move twice.
            VFXSlot bounds = Input(init, "bounds");
            Centre(bounds);
            Link(boxSize, Child(bounds, "size"));

            var birthPlace = Block<PositionShape>(init);
            birthPlace.SetSettingValue("shape", PositionShapeBase.Type.OrientedBox);
            birthPlace.SetSettingValue("positionMode", PositionBase.PositionMode.Volume);
            VFXSlot box = Input(birthPlace, "Box");
            Centre(box);
            Link(boxSize, Child(box, "size"));

            Link(lifetime, Input(SetAttribute(init, "lifetime"), 0));
            Link(velocity, Input(SetAttribute(init, "velocity"), 0));
            Link(dropWidth, Input(SetAttribute(init, "size"), 0));
            Link(stretch, Input(SetAttribute(init, "scale", VariadicChannelOptions.Y), 0));
            Link(color, Input(SetAttribute(init, "color"), 0));
            Link(alpha, Input(SetAttribute(init, "alpha"), 0));

            // Update: wrap drops inside the box (Euler integration moves them by their velocity).
            var wrap = Block<TileWarp>(update);
            VFXSlot volume = Input(wrap, "Volume");
            Centre(volume);
            Link(boxSize, Child(volume, "size"));

            // Output: a soft quad along the velocity, alpha blended, faded out near the camera. The exposure weight
            // makes the output apply the camera's exposure (weight 1, its default): HDRP's unlit outputs otherwise skip
            // it, so a colour in nits drew white (the spike's first build, kept as Assets/_VFX/Thermite).
            output.SetSettingValue("primitiveType", VFXPrimitiveType.Quad);
            output.SetSettingValue("blendMode", VFXAbstractRenderedOutput.BlendMode.Alpha);
            output.SetSettingValue("useExposureWeight", true);
            var orient = Block<Orient>(output);
            orient.SetSettingValue("mode", Orient.Mode.AlongVelocity);
            var fade = Block<CameraFade>(output);
            Link(fadedDistance, Input(fade, "FadedDistance"));
            Link(visibleDistance, Input(fade, "VisibleDistance"));

            foreach (var p in graph.children.OfType<VFXParameter>()) p.ValidateNodes();

            resource.WriteAssetWithSubAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var asset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(path);
            Debug.Log($"[METOC] built {path}: {graph.children.OfType<VFXContext>().Count()} contexts, " +
                      $"{graph.children.OfType<VFXParameter>().Count()} exposed properties, capacity {MetocRainVfx.Capacity}; " +
                      (asset != null ? "imported." : "NOT imported."));
        }

        static T Add<T>(VFXGraph graph, Vector2 position) where T : VFXContext
        {
            var context = ScriptableObject.CreateInstance<T>();
            context.position = position;
            graph.AddChild(context);
            return context;
        }

        static T Block<T>(VFXContext context) where T : VFXBlock
        {
            var block = ScriptableObject.CreateInstance<T>();
            context.AddChild(block);
            return block;
        }

        /// <summary>A Set Attribute block (overwrite, from its slot, not random).</summary>
        static SetAttribute SetAttribute(VFXContext context, string attribute,
                                         VariadicChannelOptions channels = VariadicChannelOptions.XYZ)
        {
            var block = ScriptableObject.CreateInstance<SetAttribute>();
            block.SetSettingValue("attribute", attribute);
            if (channels != VariadicChannelOptions.XYZ) block.SetSettingValue("channels", channels);
            context.AddChild(block);
            return block;
        }

        static VFXParameter Parameter<T>(VFXGraph graph, string name, T value, float x, float y)
        {
            var descriptor = VFXLibrary.GetParameters().FirstOrDefault(d => d.modelType == typeof(T))
                             ?? throw new InvalidOperationException($"[METOC] VFX Graph has no {typeof(T).Name} parameter");
            var parameter = (VFXParameter)descriptor.variant.CreateInstance();
            parameter.SetSettingValue("m_ExposedName", name);
            parameter.SetSettingValue("m_Exposed", true);
            parameter.value = value;
            graph.AddChild(parameter);
            parameter.AddNode(new Vector2(x, y));
            return parameter;
        }

        static void LinkContexts(VFXContext from, VFXContext to)
        {
            if (!VFXContext.CanLink(from, to))
                throw new InvalidOperationException($"[METOC] cannot link {from.GetType().Name} to {to.GetType().Name}");
            from.LinkTo(to);
        }

        static VFXSlot Input(VFXContext context, string name) =>
            context.inputSlots.FirstOrDefault(s => s.name == name)
            ?? throw new InvalidOperationException(
                $"[METOC] {context.GetType().Name} has no input '{name}' (it has: {string.Join(", ", context.inputSlots.Select(s => s.name))})");

        static VFXSlot Input(VFXBlock block, int index) =>
            index < block.inputSlots.Count ? block.inputSlots[index]
                : throw new InvalidOperationException($"[METOC] {block.name} has {block.inputSlots.Count} inputs, not {index + 1}");

        static VFXSlot Input(VFXBlock block, string name) =>
            block.inputSlots.FirstOrDefault(s => s.name == name)
            ?? throw new InvalidOperationException(
                $"[METOC] {block.name} has no input '{name}' (it has: {string.Join(", ", block.inputSlots.Select(s => s.name))})");

        static VFXSlot Child(VFXSlot slot, string name) =>
            slot.children.FirstOrDefault(s => s.name == name)
            ?? throw new InvalidOperationException(
                $"[METOC] slot '{slot.name}' has no child '{name}' (it has: {string.Join(", ", slot.children.Select(s => s.name))})");

        /// <summary>Puts a box slot's centre on the effect's origin.</summary>
        static void Centre(VFXSlot box)
        {
            VFXSlot centre = Child(box, "center");
            if (centre.property.type != typeof(Vector3))
                throw new InvalidOperationException($"[METOC] '{box.name}.center' is a {centre.property.type.Name}, not a Vector3");
            centre.value = Vector3.zero;
        }

        static void Link(VFXParameter parameter, VFXSlot to)
        {
            VFXSlot from = parameter.outputSlots[0];
            if (!from.CanLink(to) || !from.Link(to))
                throw new InvalidOperationException(
                    $"[METOC] cannot link {parameter.exposedName} ({from.property.type.Name}) to '{to.name}' ({to.property.type.Name})");
        }
    }
}
