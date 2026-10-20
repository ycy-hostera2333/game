// 程序化噪声：墨渗、溶解、暗角边缘用。不依赖噪声贴图。
#ifndef DEEPTIDE_NOISE_INCLUDED
#define DEEPTIDE_NOISE_INCLUDED

float DeeptideHash(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float DeeptideNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float a = DeeptideHash(i);
    float b = DeeptideHash(i + float2(1, 0));
    float c = DeeptideHash(i + float2(0, 1));
    float d = DeeptideHash(i + float2(1, 1));
    float2 u = f * f * (3 - 2 * f);
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

float DeeptideFbm(float2 p)
{
    return DeeptideNoise(p) * 0.6 + DeeptideNoise(p * 2.3) * 0.3 + DeeptideNoise(p * 5.1) * 0.1;
}

#endif
