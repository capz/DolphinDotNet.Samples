// Original single-pass edge antialiasing for the native IPL preview.
// Preserve bright one-pixel glyph/highlight cores; smooth the staircase along
// the inferred edge, rather than blurring across thin text strokes.
void main()
{
    float2 uv = GetCoordinates();
    float2 pixel = GetInvResolution();
    float4 center = SampleLocation(uv);
    float4 north = SampleLocation(uv + float2(0.0, -pixel.y));
    float4 south = SampleLocation(uv + float2(0.0, pixel.y));
    float4 west = SampleLocation(uv + float2(-pixel.x, 0.0));
    float4 east = SampleLocation(uv + float2(pixel.x, 0.0));
    float3 weights = float3(0.299, 0.587, 0.114);
    float c = dot(center.rgb, weights);
    float n = dot(north.rgb, weights), s = dot(south.rgb, weights);
    float w = dot(west.rgb, weights), e = dot(east.rgb, weights);
    float brightest = max(c, max(max(n, s), max(w, e)));
    float darkest = min(c, min(min(n, s), min(w, e)));
    float contrast = brightest - darkest;
    if (contrast < max(0.04, brightest * 0.20) || c >= brightest - contrast * 0.10)
    {
        SetOutput(float4(center.rgb, 1.0));
        return;
    }
    float4 along = abs(e - w) > abs(n - s) ? (north + south) * 0.5 : (west + east) * 0.5;
    // Final presentation is opaque; EFB alpha is not window transparency.
    SetOutput(float4(mix(center.rgb, along.rgb, 0.50), 1.0));
}
