sampler TextureSampler : register(s0);

float2 pixelDimension;

float4 PixelShaderFunction(float4 color : COLOR0, float2 texCoord : TEXCOORD0) : COLOR0
{
    float4 Color;
    Color = tex2D(TextureSampler, texCoord.xy);
    // naming sucksssssssssssss
    //Color += tex2D(TextureSampler, texCoord.xy + pixelDimension);
    //Color += tex2D(TextureSampler, texCoord.xy - pixelDimension);

    //Color += tex2D(TextureSampler, texCoord.xy + float2(-pixelDimension.x, pixelDimension.y));
    //Color += tex2D(TextureSampler, texCoord.xy + float2(pixelDimension.x, -pixelDimension.y));

    Color += tex2D(TextureSampler, texCoord.xy + float2(pixelDimension.x, 0));
    Color += tex2D(TextureSampler, texCoord.xy + float2(0, pixelDimension.y));
    Color += tex2D(TextureSampler, texCoord.xy + pixelDimension);

    Color = Color / 4;

    return Color*color;
}

technique Blur
{
    pass Pass0
    {
        PixelShader = compile ps_2_0 PixelShaderFunction();
    }
}