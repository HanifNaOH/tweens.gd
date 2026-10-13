// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Moritz Voss
// A leaf for GPU particles: a pointed oval facing the camera, fluttering as if it turned about its midrib. Particle
// colors tint it. It sinks into the page instead of showing through it.
shader_type spatial;
render_mode unshaded, cull_disabled, alpha_to_coverage;

// The leaf's length over its width; the quad mesh has the same proportions.
uniform float shape = 1.8;
// The range of flutter rates, in radians per second.
uniform float flutter_min = 3.0;
uniform float flutter_max = 8.0;

varying float turn;

float hash(float n) {
	return fract(sin(n * 12.9898) * 43758.5453);
}

void vertex() {
	float seed = float(INSTANCE_ID);
	turn = cos(TIME * mix(flutter_min, flutter_max, hash(seed)) + 6.2832 * hash(seed + 1.7));
	VERTEX.x *= max(abs(turn), 0.08);
	// A particle billboard that keeps the particle's spin and scale, as BaseMaterial3D builds it.
	mat4 world = mat4(normalize(INV_VIEW_MATRIX[0]), normalize(INV_VIEW_MATRIX[1]), normalize(INV_VIEW_MATRIX[2]), MODEL_MATRIX[3]);
	float spin = INSTANCE_CUSTOM.x;
	world = world * mat4(vec4(cos(spin), -sin(spin), 0.0, 0.0), vec4(sin(spin), cos(spin), 0.0, 0.0), vec4(0.0, 0.0, 1.0, 0.0), vec4(0.0, 0.0, 0.0, 1.0));
	mat4 scale = mat4(vec4(length(MODEL_MATRIX[0].xyz), 0.0, 0.0, 0.0), vec4(0.0, length(MODEL_MATRIX[1].xyz), 0.0, 0.0),
			vec4(0.0, 0.0, length(MODEL_MATRIX[2].xyz), 0.0), vec4(0.0, 0.0, 0.0, 1.0));
	MODELVIEW_MATRIX = VIEW_MATRIX * world * scale;
	MODELVIEW_NORMAL_MATRIX = mat3(MODELVIEW_MATRIX);
}

void fragment() {
	if ((INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).y < 0.0) {
		discard;
	}
	// The overlap of two circles whose edges meet at the leaf's tips.
	vec2 p = (UV - 0.5) * vec2(1.0, shape);
	float c = (shape * shape - 1.0) / 4.0;
	float edge = max(length(p - vec2(c, 0.0)), length(p + vec2(c, 0.0))) - (c + 0.5);
	ALPHA = clamp(0.5 - edge / fwidth(edge), 0.0, 1.0) * COLOR.a;
	ALPHA_SCISSOR_THRESHOLD = 0.5;
	ALPHA_ANTIALIASING_EDGE = 0.3;
	ALPHA_TEXTURE_COORDINATE = UV * 32.0;
	// The far side is a little darker.
	ALBEDO = COLOR.rgb * mix(0.78, 1.0, smoothstep(-0.2, 0.2, turn));
}
