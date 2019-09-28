﻿uniform vec2 uvScale0;
uniform vec2 uvRotate0;
uniform vec2 uvTranslate0;
uniform int flipTexture;
uniform mat4 rotationMatrix;
uniform int texCoords0GenType;
uniform int texCoords0Source;

vec2 rotateUV(vec2 uv, float rotation)
{
    float mid = 0.5;
    return vec2(
        cos(rotation) * (uv.x - mid) + sin(rotation) * (uv.y - mid) + mid,
        cos(rotation) * (uv.y - mid) - sin(rotation) * (uv.x - mid) + mid
    );
}

vec2 SetFlip(vec2 tex)
{
     vec2 outTexCoord = tex;

	if (flipTexture == 1) //FlipH
	      return vec2(-1, 1) * tex + vec2(1, 0);
	else if (flipTexture == 2) //FlipV
	      return vec2(1, -1) * tex + vec2(0, 1);
	else if (flipTexture == 3) //Rotate90
	      return rotateUV(tex, radians(90.0));
	else if (flipTexture == 4) //Rotate180
	      return rotateUV(tex, radians(180.0));
	else if (flipTexture == 5) //Rotate270
	      return rotateUV(tex, radians(270.0));

	return outTexCoord;
}

vec2 SetTexCoordType(int type, vec2 tex)
{
     vec2 outTexCoord = tex;
	 switch (type)
	 {
	     case 0: return tex; //Tex0
	     case 1: return tex; //Tex1
	     case 2: return tex; //Tex2
	     case 3: return tex; //Ortho
	     case 4: return tex; //Pane based
	     case 5: return tex; //Proj
	 }
	return outTexCoord;
}

void main()
{
	gl_FrontColor = gl_Color;
	vec2 texCoord0 = vec2(0.5, 0.5) + uvScale0 * (gl_MultiTexCoord0.xy + (uvTranslate0 / uvScale0 - 0.5));
	texCoord0 = SetTexCoordType(texCoords0GenType, texCoord0);

	gl_TexCoord[0].st = SetFlip(texCoord0);
	gl_Position = gl_ModelViewProjectionMatrix * rotationMatrix * gl_Vertex;
}