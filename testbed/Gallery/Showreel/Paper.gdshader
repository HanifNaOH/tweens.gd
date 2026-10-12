// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Moritz Voss
// Ruled paper. Each pixel averages the rules under its footprint, so they stay crisp up close and calm in the distance.
shader_type spatial;
// Drawn first and never written to depth, so the page cannot clip anything on or below it.
render_mode depth_draw_never, diffuse_lambert, specular_disabled;

uniform vec3 paper : source_color = vec3(0.965, 0.945, 0.906);
uniform vec3 rule : source_color = vec3(0.922, 0.886, 0.827);
// The side of a ruled square and the width of a rule, in world units. Rules run along whole squares of the world.
uniform float cell = 2.0;
uniform float rule_width = 0.03125;

varying vec2 page;

void vertex() {
	page = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xz / cell;
}

void fragment() {
	// Box-filters the rules over the pixel footprint: iquilezles.org/articles/filterableprocedurals.
	float n = cell / rule_width;
	vec2 w = max(fwidth(page), vec2(1e-5));
	vec2 a = page + 0.5 * w;
	vec2 b = page - 0.5 * w;
	vec2 ruled = (floor(a) + min(fract(a) * n, 1.0) - floor(b) - min(fract(b) * n, 1.0)) / (n * w);
	ALBEDO = mix(rule, paper, (1.0 - ruled.x) * (1.0 - ruled.y));
	ROUGHNESS = 1.0;
}
