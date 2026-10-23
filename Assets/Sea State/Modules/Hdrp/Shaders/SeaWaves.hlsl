// Deep Water — Sea State: GPU evaluation of the simulated wave field for the HDRP Water Decal.
//
// This is the GPU twin of DeepWater.Sea.Core.SeaFieldMath.Evaluate. Same component layout
// (SeaFrameComponent: 2 × float4), same phase convention θ = k (d · x0) + (φ − ω t), same
// Lagrangian displacement. If you change one, change the other — physics and pixels must
// describe the same water.
//
// Called from a Custom Function node (File mode) in SeaWaveDecal.shadergraph; also included
// by SeaFoam.compute for the breaking indicator.

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
float4 _DWSeaFade;      // x = fade start, y = fade end (Chebyshev distance from centre, 0..1 of half-size); z = 1 energy-preserving
float4 _DWSeaFoam;      // x = Jacobian whitecap threshold; y = surface foam gain; z = deep foam gain; w = 1 HDRP channel workaround

// Residual foam (SeaFoam.compute): same size and resolution as the decal, but its grid drifts with
// the water, so it sits up to one texel off the decal grid.
Texture2D<float> _DWSeaFoamResidual;
SamplerState dw_linear_clamp_sampler;
float4 _DWSeaFoamResidualDisplay;   // x = foam rate per unit residual (1 / HDRP foam lifetime), 0 = off;
                                    // yz = (decal centre − foam grid centre) / size: decal UV → foam UV offset

// Weight of the simulated near field: 1 inside, smoothly 0 towards the region edge, where
// HDRP's own bands take over through the water mask (which is 1 − this weight).
float DWSeaNearFieldWeight(float2 uv)
{
    float2 c = abs(uv - 0.5) * 2.0;
    float r = max(c.x, c.y);
    return 1.0 - smoothstep(_DWSeaFade.x, _DWSeaFade.y, r);
}

// Horizontal Jacobian of the Lagrangian map at rest position x0 (< 1 where the surface is
// compressed). Twin of ResidualFoamMath.Jacobian; true amplitudes, no render gain.
float DWSeaJacobian(float2 x0)
{
    float dxdx = 0.0, dzdz = 0.0, dxdz = 0.0;
    for (int i = 0; i < _DWSeaComponentCount; i++)
    {
        DWSeaComponent c = _DWSeaComponents[i];
        float qak = c.b.x * c.a.z * cos(c.a.z * dot(c.a.xy, x0) + c.b.y);
        dxdx += qak * c.a.x * c.a.x;
        dzdz += qak * c.a.y * c.a.y;
        dxdz += qak * c.a.x * c.a.y;
    }
    return (1.0 - dxdx) * (1.0 - dzdz) - dxdz * dxdz;
}

// Breaking indicator 0–1: compression past the calibrated whitecap threshold, sharpened.
float DWSeaBreaking(float jacobian)
{
    return saturate((_DWSeaFoam.x - jacobian) * _DWSeaFoam.y);
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
    // Energy-preserving handover: amplitude √w here, √(1 − w) on HDRP's bands (the water mask),
    // so the summed variance of the two independent fields is constant across the band.
    float amplitudeWeight = _DWSeaFade.z > 0.5 ? sqrt(w) : w;

    // Normalised to [-1, 1] for the SNORM decal atlas; HDRP multiplies back by the decal amplitude.
    Deformation = h * amplitudeWeight * _DWSeaRegion.w;
    HorizontalDeformation = dxz * amplitudeWeight * _DWSeaRegion.w;

    // Foam per second into HDRP's foam buffer. Active whitecaps where the surface is compressed
    // beyond the calibrated threshold; plus the residual field at the rate that holds HDRP's
    // buffer (which fades within its own ≤ 2 s lifetime) at the residual level.
    float active = DWSeaBreaking(jacobian);
    float residual = _DWSeaFoamResidualDisplay.x > 0.0
        ? _DWSeaFoamResidual.SampleLevel(dw_linear_clamp_sampler, UV + _DWSeaFoamResidualDisplay.yz, 0) * _DWSeaFoamResidualDisplay.x
        : 0.0;
    float surface = saturate(active + residual) * w;
    float deep = saturate((_DWSeaFoam.x - jacobian) * _DWSeaFoam.z) * w;

    // HDRP 17.4 channel mismatch (verified in source and on screen): the Water Decal graph template
    // writes the foam pass as (R = SurfaceFoam, G = DeepFoam), but the FoamDecal pass that feeds
    // HDRP's foam buffer reads the region's (G, B) as (surface, deep) — the older packed layout.
    // So DeepFoam is what HDRP shows as surface foam and SurfaceFoam never arrives. With
    // _DWSeaFoam.w = 1 our surface foam goes out through DeepFoam; HDRP's deep (subsurface) foam
    // then gets nothing from the decal. Turn it off in the look once HDRP reads (R, G).
    if (_DWSeaFoam.w > 0.5)
    {
        SurfaceFoam = 0.0;
        DeepFoam = surface;
    }
    else
    {
        SurfaceFoam = surface;
        DeepFoam = deep;
    }
}

#endif // DEEPWATER_SEA_WAVES_INCLUDED
