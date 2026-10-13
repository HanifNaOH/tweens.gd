// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Moritz Voss
// Multiplies the frame's edges toward a warm ink, so the eye stays on the middle of the page.
shader_type canvas_item;
render_mode blend_mul;

uniform vec3 tint : source_color = vec3(0.6, 0.45, 0.36);
uniform float strength : hint_range(0.0, 1.0) = 0.5;
// Where darkening starts and where it is complete, as shares of the half-diagonal.
uniform float inner = 0.35;
uniform float outer = 1.15;

void fragment() {
	vec2 aspect = vec2(SCREEN_PIXEL_SIZE.y / SCREEN_PIXEL_SIZE.x, 1.0);
	float distance_out = length((UV - 0.5) * aspect) / length(0.5 * aspect);
	float amount = strength * smoothstep(inner, outer, distance_out);
	// Dithering keeps the soft gradient from banding in 8 bits.
	amount += (fract(sin(dot(FRAGCOORD.xy, vec2(12.9898, 78.233))) * 43758.5453) - 0.5) / 255.0;
	COLOR = vec4(mix(vec3(1.0), tint, amount), 1.0);
}
