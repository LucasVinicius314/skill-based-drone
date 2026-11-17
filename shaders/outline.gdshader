shader_type spatial;
render_mode unshaded, cull_front, depth_draw_opaque;

// a selection-style outline: the mesh is drawn again, back faces only, pushed outward by a fixed number of screen
// pixels, so the model keeps a solid red edge at any distance. vertices move away from the mesh centre (not along
// the normal), so the hard corners of box meshes stay connected
uniform vec4 outline_color : source_color = vec4(1.0, 0.1, 0.08, 1.0);
uniform float width_px = 3.0;

void vertex() {
  vec4 clip = PROJECTION_MATRIX * MODELVIEW_MATRIX * vec4(VERTEX, 1.0);
  vec3 away = normalize(mat3(MODELVIEW_MATRIX) * normalize(VERTEX));
  vec2 dir = (PROJECTION_MATRIX * vec4(away, 0.0)).xy;
  clip.xy += normalize(dir + vec2(0.00001)) * width_px * 2.0 / VIEWPORT_SIZE * clip.w;
  POSITION = clip;
}

void fragment() {
  ALBEDO = outline_color.rgb;
}
