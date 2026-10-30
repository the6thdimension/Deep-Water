namespace DeepWater.Metoc.Hdrp
{
    /// <summary>
    /// The exposed properties of <c>MetocRain.vfx</c> (E53): one spelling shared by its builder
    /// (Deep Water → METOC → Build Rain VFX) and its driver (<c>MetocRain</c>). The graph spawns drops at
    /// <see cref="Rate"/> in a box, moves them at <see cref="Velocity"/>, wraps them inside the box and draws each as a
    /// soft quad stretched along its velocity.
    /// <para>
    /// The box, its bounds and the velocity are in the effect's own space (VFX Graph's slots are local), centred on its
    /// GameObject; the drops themselves live in world space. So the box follows the object: move the object, not a
    /// centre. (A world centre on a moved object counts the move twice and the effect is culled: slice 3 spike.)
    /// </para>
    /// </summary>
    public static class MetocRainVfx
    {
        /// <summary>Path of the built graph.</summary>
        public const string AssetPath = "Assets/METOC/Modules/Hdrp/Effects/MetocRain.vfx";

        /// <summary>Drops spawned per second (float).</summary>
        public const string Rate = "Rate";
        /// <summary>A drop's life, s (float); the steady count is Rate × Lifetime.</summary>
        public const string Lifetime = "Lifetime";
        /// <summary>The box's size, metres (Vector3), centred on the effect's GameObject.</summary>
        public const string BoxSize = "BoxSize";
        /// <summary>A drop's velocity, m/s (Vector3), in the effect's space (the driver keeps the object unrotated).</summary>
        public const string Velocity = "Velocity";
        /// <summary>A drawn drop's width, metres (float).</summary>
        public const string DropWidth = "DropWidth";
        /// <summary>A drawn drop's length over its width (float): the streak.</summary>
        public const string Stretch = "Stretch";
        /// <summary>A drop's luminance, linear RGB in nits (Vector3); the output applies the camera's exposure, as HDRP does
        /// to the sky. (HDRP's unlit VFX outputs skip exposure by default: a colour of 0.6 then drew white. Slice 3 spike.)</summary>
        public const string Color = "Color";
        /// <summary>A drop's opacity, 0–1 (float).</summary>
        public const string Alpha = "Alpha";
        /// <summary>Drops nearer the camera than this are fully faded, metres (float).</summary>
        public const string FadedDistance = "FadedDistance";
        /// <summary>Drops farther than this are fully drawn, metres (float).</summary>
        public const string VisibleDistance = "VisibleDistance";

        /// <summary>The most drops alive at once (the graph's capacity); the driver keeps Rate × Lifetime under it.</summary>
        public const uint Capacity = 131072;
    }
}
