// Deep Water — Sea State: GPU evaluation of the simulated wave field for the HDRP Water Decal.
//
// This is the GPU twin of DeepWater.Sea.Core.SeaFieldMath.Evaluate. Same component layout
// (SeaFrameComponent: 2 × float4), same phase convention θ = k (d · x0) + (φ − ω t), same
// Lagrangian displacement. If you change one, change the other — physics and pixels must
// describe the same water.
//
// Called from a Custom Function node (File mode) in SeaWaveDecal.shadergraph.

#ifndef DEEPWATER_SEA_WAVES_INCLUDED
#define DEEPWATER_SEA_WAVES_INCLUDED

struct DWSeaComponent
{
    float4 a;   // x, y = direction (world X, Z); z = wavenumber k; w = amplitude
    float4 b;   // x = Q·amplitude; y = wrapped phase (φ − ω t); z = ω; w = render gain (blur pre-compensation)
};

StructuredBuffer<DWSeaComponent> _DWSeaComponents;
int    _DWSeaComponentCount;
float4 _DWSeaRegion;    // xy = region centre (world XZ, m); z = region size (m); w = 1 / amplitude bound
float4 _DWSeaFade;      // x = fade start, y = fade end (Chebyshev distance from centre, 0..1 of half-size)
float4 _DWSeaFoam;      // x = Jacobian whitecap threshold; y = surface foam gain; z = deep foam gain

// Weight of the simulated near field: 1 inside, smoothly 0 towards the region edge, where
// HDRP's own bands take over through the water mask (which is 1 − this weight).
float DWSeaNearFieldWeight(float2 uv)
{
    float2 c = abs(uv - 0.5) * 2.0;
    float r = max(c.x, c.y);
    return 1.0 - smoothstep(_DWSeaFade.x, _DWSeaFade.y, r);
}

void DWSeaWaveDecal_float(float2 UV,
    out float Deformation, out float2 HorizontalDeformation, out float SurfaceFoam, out float DeepFoam)
{
    float2 x0 = _DWSeaRegion.xy + (UV - 0.5) * _DWSeaRegion.z;

    float h = 0.0;
    float2 dxz = float2(0.0, 0.0);
    float dxdx = 0.0, dzdz = 0.0, dxdz = 0.0;

    for (int i = 0; i < _DWSeaComponentCount; i++)
    {
        DWSeaComponent c = _DWSeaComponents[i];
        float theta = c.a.z * dot(c.a.xy, x0) + c.b.y;
        float s, co;
        sincos(theta, s, co);

        // Displacement carries the render gain (undoes HDRP's deformation blur for this wave);
        // the Jacobian uses true amplitudes so whitecap coverage stays calibrated.
        h += c.b.w * c.a.w * co;
        dxz -= c.b.w * c.b.x * s * c.a.xy;

        float qak = c.b.x * c.a.z * co;
        dxdx += qak * c.a.x * c.a.x;
        dzdz += qak * c.a.y * c.a.y;
        dxdz += qak * c.a.x * c.a.y;
    }

    float jacobian = (1.0 - dxdx) * (1.0 - dzdz) - dxdz * dxdz;
    float w = DWSeaNearFieldWeight(UV);

    // Normalised to [-1, 1] for the SNORM decal atlas; HDRP multiplies back by the decal amplitude.
    Deformation = h * w * _DWSeaRegion.w;
    HorizontalDeformation = dxz * w * _DWSeaRegion.w;

    // Whitecaps where the surface is compressed beyond the calibrated threshold (foam per second).
    float breaking = saturate(_DWSeaFoam.x - jacobian);
    SurfaceFoam = saturate(breaking * _DWSeaFoam.y) * w;
    DeepFoam = saturate(breaking * _DWSeaFoam.z) * w;
}

#endif // DEEPWATER_SEA_WAVES_INCLUDED
