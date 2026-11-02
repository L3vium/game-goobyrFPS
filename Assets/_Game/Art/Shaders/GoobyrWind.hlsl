// Author : Baptiste Vu Liem
// The world's one wind, shared by the grass (Goobyr/Grass) and the trees and bushes (Goobyr/Foliage).
// World/Wind.cs sets the globals once per frame, before any camera renders.
#ifndef GOOBYR_WIND_INCLUDED
#define GOOBYR_WIND_INCLUDED

// xy: direction the wind blows toward (unit, world xz), z: strength 0..1, w: gustiness 0..1.
float4 _GoobyrWind;
// x: time (s), y: last frame's time (for motion vectors), z: gust drift speed (m/s), w: gust patch size (m).
float4 _GoobyrWindTime;

float GoobyrHash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

// Smooth value noise in [0, 1].
float GoobyrNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float a = GoobyrHash21(i);
    float b = GoobyrHash21(i + float2(1.0, 0.0));
    float c = GoobyrHash21(i + float2(0.0, 1.0));
    float d = GoobyrHash21(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

// 0..1 gust field at a world point: patches of stronger wind drifting downwind, so a meadow shows
// rolling waves and a tree shakes when one reaches it.
float GoobyrGust(float2 xz, float t)
{
    float size = max(_GoobyrWindTime.w, 1.0);
    float2 p = xz / size - _GoobyrWind.xy * (t * _GoobyrWindTime.z / size);
    float n = GoobyrNoise(p) * 0.65 + GoobyrNoise(p * 2.13 + 17.1) * 0.35;
    return saturate((n - 0.3) * 2.0);
}

// Horizontal push of the wind at a point (world xz, about 0..1): a steady lean plus the gusts.
float2 GoobyrWindPush(float2 xz, float t)
{
    float g = GoobyrGust(xz, t);
    return _GoobyrWind.xy * (_GoobyrWind.z * (0.4 + 0.6 * lerp(0.55, g, _GoobyrWind.w) * 1.6));
}

#endif
