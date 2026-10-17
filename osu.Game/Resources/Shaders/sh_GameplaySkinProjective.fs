#define HIGH_PRECISION_VERTEX
#include "sh_Utils.h"
#include "sh_Masking.h"
#include "sh_TextureWrapping.h"

layout(location = 2) in highp vec3 v_ProjectiveTexCoord;
layout(set = 0, binding = 0) uniform lowp texture2D m_Texture;
layout(set = 0, binding = 1) uniform lowp sampler m_Sampler;
layout(location = 0) out vec4 o_Colour;

void main(void)
{
    highp vec2 texCoord = v_ProjectiveTexCoord.xy / v_ProjectiveTexCoord.z;
    highp vec2 wrappedCoord = wrap(texCoord, v_TexRect);
    o_Colour = getRoundedColor(wrappedSampler(wrappedCoord, v_TexRect, m_Texture, m_Sampler, -0.9), wrappedCoord);
}
