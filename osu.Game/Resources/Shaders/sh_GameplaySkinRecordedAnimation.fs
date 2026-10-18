#define HIGH_PRECISION_VERTEX
#include "sh_Utils.h"
#include "sh_Masking.h"
#include "sh_TextureWrapping.h"

layout(location = 2) in highp vec3 v_ProjectiveTexCoord;
layout(set = 0, binding = 0) uniform lowp texture2D m_Texture;
layout(set = 0, binding = 1) uniform lowp sampler m_Sampler;
layout(std140, set = 1, binding = 0) uniform m_RecordedColour
{
    highp vec4 m_Multiply;
    highp vec4 m_Add;
    highp vec4 m_Hsl;
    highp vec4 m_Row0;
    highp vec4 m_Row1;
    highp vec4 m_Row2;
    highp vec4 m_Row3;
    highp vec4 m_Offset;
};
layout(location = 0) out vec4 o_Colour;

highp vec3 rgbToHsl(highp vec3 rgb)
{
    highp float hi = max(max(rgb.r, rgb.g), rgb.b);
    highp float lo = min(min(rgb.r, rgb.g), rgb.b);
    highp float delta = hi - lo;
    highp float lightness = (hi + lo) * 0.5;
    if (delta < 0.000001)
        return vec3(0.0, 0.0, lightness);
    highp float hue = hi == rgb.r ? (rgb.g - rgb.b) / delta : hi == rgb.g ? 2.0 + (rgb.b - rgb.r) / delta : 4.0 + (rgb.r - rgb.g) / delta;
    return vec3(fract(hue / 6.0), delta / max(0.000001, 1.0 - abs(2.0 * lightness - 1.0)), lightness);
}

highp vec3 hslToRgb(highp vec3 hsl)
{
    highp vec3 hue = clamp(abs(mod(hsl.x * 6.0 + vec3(0.0, 4.0, 2.0), 6.0) - 3.0) - 1.0, 0.0, 1.0);
    return hsl.z + (hue - 0.5) * (1.0 - abs(2.0 * hsl.z - 1.0)) * hsl.y;
}

void main(void)
{
    highp vec2 coord = v_ProjectiveTexCoord.xy / v_ProjectiveTexCoord.z;
    highp vec2 wrapped = wrap(coord, v_TexRect);
    highp vec4 colour = wrappedSampler(wrapped, v_TexRect, m_Texture, m_Sampler, -0.9) * m_Multiply + m_Add;
    colour = clamp(vec4(dot(m_Row0, colour), dot(m_Row1, colour), dot(m_Row2, colour), dot(m_Row3, colour)) + m_Offset, 0.0, 1.0);
    if (any(notEqual(m_Hsl.xyz, vec3(0.0))))
    {
        highp vec3 hsl = rgbToHsl(colour.rgb) + m_Hsl.xyz;
        hsl = vec3(fract(hsl.x), clamp(hsl.yz, 0.0, 1.0));
        colour.rgb = hslToRgb(hsl);
    }
    o_Colour = getRoundedColor(colour, wrapped);
}
