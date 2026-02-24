cbuffer CBPerFrame : register(b0)
{
    matrix ViewProj;
    matrix InverseViewProj;
    float4 CameraPos;
    float4 LightPos;
    float4 AmbientColor;
    float4 LightColor;
    float4 GridColor;
    float4 GridAxisColor;
    matrix LightViewProj0;
    matrix LightViewProj1;
    matrix LightViewProj2;
    float4 LightTypeParams;
    float4 ShadowParams;
    float4 CascadeSplits;
    float4 EnvironmentParam;
    float4 PcssParams;
}

cbuffer CBPerObject : register(b1)
{
    matrix WorldViewProj;
    matrix World;
}

struct PS_IN
{
    float4 pos : SV_POSITION;
    float3 wPos : TEXCOORD0;
};
float4 PS(PS_IN input) : SV_Target
{
    float3 pos = input.wPos;
    float2 coord = pos.xz;
    
    float2 derivative = fwidth(coord);
    float2 grid = abs(frac(coord - 0.5) - 0.5) / derivative;
    float lineVal = min(grid.x, grid.y);
    float gridAlpha = 1.0 - min(lineVal, 1.0);
    
    float4 color = float4(GridColor.rgb, GridColor.a * gridAlpha);
    
    float2 axis = abs(coord);
    float2 axisDist = axis / derivative;
    float axisLine = min(axisDist.x, axisDist.y);
    float axisAlpha = saturate(1.5 - axisLine);
    color = lerp(color, GridAxisColor, axisAlpha);

    float dist = length(CameraPos.xz - pos.xz);
    if (EnvironmentParam.w > 0.5)
    {
        color.a *= max(0.0, 1.0 - dist / 100.0);
    }

    return color;
}