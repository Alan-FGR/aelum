float4x4 Projection;
float3 Origin;

float3 projCorner0;
float3 projCorner1;
float3 projCorner2;
float3 projCorner3;

texture Texture;
sampler2D textureSampler = sampler_state
{
    Texture = (Texture);
    MagFilter = Linear;
    MinFilter = Linear;
    AddressU = Clamp;
    AddressV = Clamp;
};

struct VertexOutput
{
    float4 Position     : POSITION;
    float2 UV : TEXCOORD0;
};

struct VertexInput
{
    float3 Position : POSITION0;
    float Extrusion : BLENDWEIGHT0;
    float IsProjector : BLENDWEIGHT1;
};

VertexOutput ExtrudeShadows(VertexInput input)
{
    VertexOutput Output = (VertexOutput)0;

    //position projector
    if (input.IsProjector > 0.05)
    {
        if (input.IsProjector > 0.4)
        {
            input.Position = projCorner3;
            Output.UV = float2(0, 1);
        }
        else if (input.IsProjector > 0.3)
        {
            input.Position = projCorner2;
            Output.UV = float2(1, 1);
        }
        else if (input.IsProjector > 0.2)
        {
            input.Position = projCorner1;
            Output.UV = float2(1, 0);
        }
        else
        {
            input.Position = projCorner0;
        }
    }

    //extrude shadows
    input.Position += normalize(input.Position - Origin) * input.Extrusion * 10000; //we have shadow biases! :D
    
    Output.Position = mul(float4(input.Position,1), Projection);
    return Output;
}

struct PixelOutput
{
    float4 Color : COLOR0;
};

PixelOutput OurFirstPixelShader(VertexOutput input)
{
    PixelOutput Output = (PixelOutput) 0;
    Output.Color = tex2D(textureSampler, input.UV);
    return Output;
}

technique Shadows
{
    pass Pass0
    {
        VertexShader = compile vs_2_0 ExtrudeShadows();
        PixelShader = compile ps_2_0 OurFirstPixelShader();
    }
}