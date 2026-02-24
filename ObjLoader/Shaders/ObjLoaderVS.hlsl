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

struct VS_IN
{
    float3 pos : POSITION;
    float3 norm : NORMAL;
    float2 uv : TEXCOORD0;
};

struct VS_OUT
{
    float4 pos : SV_POSITION;
    float3 wPos : TEXCOORD1;
    float3 norm : NORMAL;
    float2 uv : TEXCOORD0;
    float4 screenPos : TEXCOORD3;
};

VS_OUT VS(VS_IN input)
{
    VS_OUT output;

    output.pos = mul(float4(input.pos, 1.0f), WorldViewProj);
    output.wPos = mul(float4(input.pos, 1.0f), World).xyz;
    output.norm = normalize(mul(float4(input.norm, 0.0f), World).xyz);
    output.uv = input.uv;
    output.screenPos = output.pos;

    return output;
}