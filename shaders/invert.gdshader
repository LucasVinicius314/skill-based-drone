shader_type canvas_item;
uniform sampler2D SCREEN_TEXTURE : hint_screen_texture, filter_linear_mipmap;

void fragment() {
    vec4 bg = textureLod(SCREEN_TEXTURE, SCREEN_UV, 0.0);
    float brightness = dot(bg.rgb, vec3(0.299, 0.587, 0.114));
    COLOR = vec4(vec3(step(0.5, 1.0 - brightness)), 1.0);
}
