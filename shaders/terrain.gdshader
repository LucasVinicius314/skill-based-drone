shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;

// the terrain's look: the mesh carries a base colour per vertex (grass, dirt, rock, snow); this adds the detail that
// stops it looking like clay. blotches and fine grain at three scales, grass streaks, rock strata, and a bumpy normal,
// all procedural and anchored to world position, fading out with distance so far hills stay clean

uniform float detail_range_near = 120.0;
uniform float detail_range_far = 520.0;
uniform float bump_strength = 1.1;

varying vec3 world_pos;
varying vec3 world_normal;
varying vec3 base_color;

float hash(vec2 p) {
	p = fract(p * vec2(123.34, 456.21));
	p += dot(p, p + 45.32);
	return fract(p.x * p.y);
}

float value_noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x),
			mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y);
}

float fbm(vec2 p) {
	float sum = 0.0;
	float amp = 0.5;
	for (int i = 0; i < 4; i++) {
		sum += value_noise(p) * amp;
		p = p * 2.03 + vec2(17.1, 9.2);
		amp *= 0.5;
	}
	return sum;
}

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
	// vertex colours are authored as sRGB
	base_color = pow(COLOR.rgb, vec3(2.2));
}

void fragment() {
	float dist = length(world_pos - CAMERA_POSITION_WORLD);
	float detail = 1.0 - smoothstep(detail_range_near, detail_range_far, dist);

	vec2 p = world_pos.xz;
	float blotch = fbm(p * 0.045);
	float grain = value_noise(p * 2.2);
	float fine = value_noise(p * 9.0);
	float streak = value_noise(vec2(p.x * 7.0, p.y * 1.3) + blotch * 4.0);

	float steep = 1.0 - clamp(world_normal.y, 0.0, 1.0);
	float rocky = smoothstep(0.25, 0.5, steep);

	// grass: light and dark patches, fine grain, a few streaks. rock: layers that follow the height
	float tone = mix(0.5, 1.45, smoothstep(0.2, 0.8, blotch)) * mix(0.85, 1.15, grain) * mix(0.8, 1.2, fine);
	tone *= mix(1.0, mix(0.92, 1.08, streak), 1.0 - rocky);
	float strata = sin(world_pos.y * 1.7 + blotch * 6.0) * 0.5 + 0.5;
	tone *= mix(1.0, mix(0.8, 1.15, strata), rocky);
	// a gentle variation survives at any distance, the rest fades
	tone = mix(mix(0.8, 1.2, blotch), tone, detail);

	// dry earth showing through the grass in big patches, and darker where it is steep
	float dry = smoothstep(0.58, 0.72, fbm(p * 0.018 + vec2(31.0, 7.0))) * (1.0 - rocky);
	vec3 earth = vec3(0.17, 0.11, 0.06) * mix(0.7, 1.3, grain);
	vec3 albedo = mix(base_color * tone, earth, dry * 0.8 * mix(0.5, 1.0, detail));

	// bumps: the slope of the grain noise tilts the normal
	float e = 0.35;
	float h0 = value_noise(p * 2.2) * 0.6 + value_noise(p * 7.0) * 0.4;
	float hx = value_noise((p + vec2(e, 0.0)) * 2.2) * 0.6 + value_noise((p + vec2(e, 0.0)) * 7.0) * 0.4;
	float hz = value_noise((p + vec2(0.0, e)) * 2.2) * 0.6 + value_noise((p + vec2(0.0, e)) * 7.0) * 0.4;
	vec3 bumped = normalize(world_normal + vec3(h0 - hx, 0.0, h0 - hz) * bump_strength * detail * (1.0 + rocky * 1.5));

	ALBEDO = albedo;
	NORMAL = normalize((VIEW_MATRIX * vec4(bumped, 0.0)).xyz);
	ROUGHNESS = 0.95;
	SPECULAR = 0.15;
}
