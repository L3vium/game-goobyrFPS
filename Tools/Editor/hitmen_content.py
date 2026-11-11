"""Hitmen content builder: input, materials, data assets and Blueprint children of every
C++ gameplay class, with placeholder engine shapes the team replaces later."""
import os
import unreal
from hitmen_common import *

# ---------------------------------------------------------------------------------------------
# Input (AZERTY defaults, every key player-mappable)
# ---------------------------------------------------------------------------------------------

ACTIONS = {
    # name: (value type, [(mapping name, display, key, modifiers)])
    "IA_Move": ("2d", [("MoveForward", "Move forward", "Z", ["swizzle"]),
                       ("MoveBackward", "Move backward", "S", ["swizzle", "negate"]),
                       ("MoveLeft", "Move left", "Q", ["negate"]),
                       ("MoveRight", "Move right", "D", [])]),
    "IA_Look": ("2d", [("Look", "Look", "Mouse2D", [])]),
    "IA_Jump": ("bool", [("Jump", "Jump / climb", "SpaceBar", [])]),
    "IA_Crouch": ("bool", [("Crouch", "Crouch / slide", "LeftControl", [])]),
    "IA_Sprint": ("bool", [("Sprint", "Sprint", "LeftShift", [])]),
    "IA_Fire": ("bool", [("Fire", "Fire / punch", "LeftMouseButton", [])]),
    "IA_Aim": ("bool", [("Aim", "Aim", "RightMouseButton", [])]),
    "IA_Interact": ("bool", [("Interact", "Interact / throw", "F", [])]),
    "IA_MoveAbility": ("bool", [("MoveAbility", "Move ability", "E", [])]),
    "IA_ManipulationAbility": ("bool", [("ManipulationAbility", "Manipulation ability", "A", [])]),
    "IA_SwitchSlot": ("1d", [("SwitchSlot", "Switch weapon", "MouseWheelAxis", [])]),
    "IA_Sightroom": ("bool", [("Sightroom", "Sightroom", "Tab", [])]),
    "IA_EmoteWheel": ("bool", [("EmoteWheel", "Emote wheel", "R", [])]),
    "IA_Pause": ("bool", [("Pause", "Pause", "Escape", []), ("PauseAlt", "Pause (alt)", "P", [])]),
    "IA_SpectatorMode": ("bool", [("SpectatorMode", "Spectator view", "V", [])]),
}

VALUE_TYPES = {
    "bool": unreal.InputActionValueType.BOOLEAN,
    "1d": unreal.InputActionValueType.AXIS1D,
    "2d": unreal.InputActionValueType.AXIS2D,
}


def build_input():
    folder = path("Input", "Actions")
    actions = {}
    for name, (value_type, _) in ACTIONS.items():
        ia = create(name, folder, unreal.InputAction, unreal.InputAction_Factory())
        set_props(ia, value_type=VALUE_TYPES[value_type])
        save(ia)
        actions[name] = ia

    imc = create("IMC_Gameplay", path("Input", "Contexts"), unreal.InputMappingContext, unreal.InputMappingContext_Factory())
    imc.unmap_all()
    for name, (_, mappings) in ACTIONS.items():
        for mapping_name, display, key_name, modifiers in mappings:
            imc.map_key(actions[name], key(key_name))
    # Second pass: modifiers and player-mappable settings on each mapping.
    data = imc.get_editor_property("default_key_mappings")
    mapping_list = list(data.get_editor_property("mappings"))
    lookup = {}
    for name, (_, mappings) in ACTIONS.items():
        for mapping_name, display, key_name, modifiers in mappings:
            lookup[(name, key_name)] = (mapping_name, display, modifiers)
    rebuilt = []
    for mapping in mapping_list:
        action = mapping.get_editor_property("action")
        key_name = mapping.get_editor_property("key").get_editor_property("key_name")
        entry = lookup.get((action.get_name(), str(key_name)))
        if entry:
            mapping_name, display, modifiers = entry
            mods = []
            for mod in modifiers:
                if mod == "swizzle":
                    mods.append(unreal.new_object(unreal.InputModifierSwizzleAxis, outer=imc))
                elif mod == "negate":
                    mods.append(unreal.new_object(unreal.InputModifierNegate, outer=imc))
            mapping.set_editor_property("modifiers", mods)
            settings = unreal.new_object(unreal.PlayerMappableKeySettings, outer=imc)
            settings.set_editor_property("name", mapping_name)
            settings.set_editor_property("display_name", display)
            settings.set_editor_property("display_category", "Hitmen")
            mapping.set_editor_property("setting_behavior", unreal.PlayerMappableKeySettingBehaviors.OVERRIDE_SETTINGS)
            mapping.set_editor_property("player_mappable_key_settings", settings)
        rebuilt.append(mapping)
    data.set_editor_property("mappings", rebuilt)
    imc.set_editor_property("default_key_mappings", data)
    save(imc)

    spectator = create("IMC_Spectator", path("Input", "Contexts"), unreal.InputMappingContext, unreal.InputMappingContext_Factory())
    save(spectator)
    log("input: actions", len(actions), "mappings", len(rebuilt))
    return actions, imc, spectator


# ---------------------------------------------------------------------------------------------
# Physical materials and materials
# ---------------------------------------------------------------------------------------------

def build_physical_materials():
    folder = path("Physics")
    surfaces = {"PM_Grass": unreal.PhysicalSurface.SURFACE_TYPE1, "PM_Stone": unreal.PhysicalSurface.SURFACE_TYPE2,
                "PM_Sand": unreal.PhysicalSurface.SURFACE_TYPE3, "PM_Wood": unreal.PhysicalSurface.SURFACE_TYPE4,
                "PM_Metal": unreal.PhysicalSurface.SURFACE_TYPE5}
    result = {}
    for name, surface in surfaces.items():
        pm = create(name, folder, unreal.PhysicalMaterial, unreal.PhysicalMaterialFactoryNew())
        set_props(pm, surface_type=surface)
        save(pm)
        result[name] = pm
    return result


def new_material(name, folder):
    mat = create(name, folder, unreal.Material, unreal.MaterialFactoryNew())
    MEL.delete_all_material_expressions(mat)
    return mat


def build_materials(physical):
    folder = path("Materials", "Master")
    inst_folder = path("Materials", "Instances")

    # Flat colour, opaque (placeholder surfaces).
    color = new_material("M_Hitmen_Color", folder)
    param = MEL.create_material_expression(color, unreal.MaterialExpressionVectorParameter, -400, 0)
    set_props(param, parameter_name="Color", default_value=unreal.LinearColor(0.8, 0.8, 0.8, 1.0))
    MEL.connect_material_property(param, "", unreal.MaterialProperty.MP_BASE_COLOR)
    rough = MEL.create_material_expression(color, unreal.MaterialExpressionScalarParameter, -400, 200)
    set_props(rough, parameter_name="Roughness", default_value=0.7)
    MEL.connect_material_property(rough, "", unreal.MaterialProperty.MP_ROUGHNESS)
    MEL.recompile_material(color)
    save(color)

    instances = {}
    palette = {
        "MI_Red": (0.75, 0.05, 0.04), "MI_Yellow": (0.95, 0.7, 0.05), "MI_Green": (0.1, 0.5, 0.15),
        "MI_Blue": (0.08, 0.2, 0.6), "MI_White": (0.9, 0.9, 0.9), "MI_Dark": (0.05, 0.05, 0.06),
        "MI_Wood": (0.35, 0.2, 0.08), "MI_Metal": (0.4, 0.42, 0.45), "MI_Grass": (0.15, 0.35, 0.08),
        "MI_Sand": (0.7, 0.6, 0.38), "MI_Stone": (0.45, 0.45, 0.43), "MI_Gold": (0.83, 0.62, 0.2),
        "MI_Glass": (0.6, 0.8, 0.9), "MI_Mountain": (0.32, 0.3, 0.28),
    }
    for name, rgb in palette.items():
        mi = create(name, inst_folder, unreal.MaterialInstanceConstant, unreal.MaterialInstanceConstantFactoryNew())
        set_props(mi, parent=color)
        MEL.set_material_instance_vector_parameter_value(mi, "Color", unreal.LinearColor(rgb[0], rgb[1], rgb[2], 1.0))
        surface = {"MI_Grass": "PM_Grass", "MI_Sand": "PM_Sand", "MI_Wood": "PM_Wood", "MI_Metal": "PM_Metal", "MI_Stone": "PM_Stone"}.get(name)
        if surface:
            set_props(mi, phys_material=physical[surface])
        save(mi)
        instances[name] = mi
        # Checker (prototype grid) twin for level geometry: same tint, same physical surface.
        grid = create("MI_Grid_" + name[3:], inst_folder, unreal.MaterialInstanceConstant, unreal.MaterialInstanceConstantFactoryNew())
        set_props(grid, parent=load("/Game/LevelPrototyping/Materials/M_PrototypeGrid"))
        tint = unreal.LinearColor(rgb[0], rgb[1], rgb[2], 1.0)
        for param in ("SurfaceColor", "TopSurfaceColor"):
            MEL.set_material_instance_vector_parameter_value(grid, param, tint)
        MEL.set_material_instance_vector_parameter_value(grid, "GridColor", unreal.LinearColor(rgb[0] * 0.35, rgb[1] * 0.35, rgb[2] * 0.35, 1.0))
        MEL.set_material_instance_vector_parameter_value(grid, "SubGridColor", unreal.LinearColor(rgb[0] * 0.7, rgb[1] * 0.7, rgb[2] * 0.7, 1.0))
        if surface:
            set_props(grid, phys_material=physical[surface])
        save(grid)
        instances["MI_Grid_" + name[3:]] = grid

    # Translucent unlit "Color" (camera cones, retrieval beacons).
    cone = new_material("M_Hitmen_TranslucentColor", folder)
    set_props(cone, blend_mode=unreal.BlendMode.BLEND_TRANSLUCENT, shading_model=unreal.MaterialShadingModel.MSM_UNLIT, two_sided=True)
    cparam = MEL.create_material_expression(cone, unreal.MaterialExpressionVectorParameter, -500, 0)
    set_props(cparam, parameter_name="Color", default_value=unreal.LinearColor(1, 1, 1, 0.25))
    MEL.connect_material_property(cparam, "", unreal.MaterialProperty.MP_EMISSIVE_COLOR)
    MEL.connect_material_property(cparam, "A", unreal.MaterialProperty.MP_OPACITY)
    MEL.recompile_material(cone)
    save(cone)

    # Decal footprint of a camera cone.
    decal = new_material("M_Hitmen_ConeDecal", folder)
    set_props(decal, material_domain=unreal.MaterialDomain.MD_DEFERRED_DECAL, blend_mode=unreal.BlendMode.BLEND_TRANSLUCENT)
    dparam = MEL.create_material_expression(decal, unreal.MaterialExpressionVectorParameter, -500, 0)
    set_props(dparam, parameter_name="Color", default_value=unreal.LinearColor(1, 1, 1, 0.35))
    MEL.connect_material_property(dparam, "", unreal.MaterialProperty.MP_BASE_COLOR)
    MEL.connect_material_property(dparam, "", unreal.MaterialProperty.MP_EMISSIVE_COLOR)
    # Round footprint: a disc in decal UV space, soft inside, brighter rim.
    d_uv = MEL.create_material_expression(decal, unreal.MaterialExpressionTextureCoordinate, -700, 250)
    d_disc = MEL.create_material_expression(decal, unreal.MaterialExpressionCustom, -350, 200)
    d_inputs = []
    for input_name in ("UV", "A"):
        ci = unreal.CustomInput()
        ci.set_editor_property("input_name", input_name)
        d_inputs.append(ci)
    set_props(d_disc, output_type=unreal.CustomMaterialOutputType.CMOT_FLOAT1, description="Disc", inputs=d_inputs,
              code="float d = length(UV - 0.5) * 2.0;\nfloat disc = saturate((1.0 - d) * 30.0);\nfloat rim = saturate(1.0 - abs(d - 0.94) * 20.0);\nreturn disc * A * 0.6 + rim * saturate(A * 2.5);")
    MEL.connect_material_expressions(d_uv, "", d_disc, "UV")
    MEL.connect_material_expressions(dparam, "A", d_disc, "A")
    MEL.connect_material_property(d_disc, "", unreal.MaterialProperty.MP_OPACITY)
    MEL.recompile_material(decal)
    save(decal)

    # Monitor screen showing a render target.
    screen = new_material("M_Hitmen_Screen", folder)
    set_props(screen, shading_model=unreal.MaterialShadingModel.MSM_UNLIT)
    tex = MEL.create_material_expression(screen, unreal.MaterialExpressionTextureSampleParameter2D, -500, 0)
    set_props(tex, parameter_name="ScreenTexture")
    MEL.connect_material_property(tex, "RGB", unreal.MaterialProperty.MP_EMISSIVE_COLOR)
    MEL.recompile_material(screen)
    save(screen)

    # Sightroom post-process: an expanding scan wave from the player, a world grid on the floor
    # behind it, a cool desaturated world, highlighted actors through walls. Driven by C++:
    # ScanOrigin, ScanRadius, ScanProgress, Fade.
    pp = new_material("PP_Sightroom", path("Materials", "PostProcess"))
    set_props(pp, material_domain=unreal.MaterialDomain.MD_POST_PROCESS)
    scene = MEL.create_material_expression(pp, unreal.MaterialExpressionSceneTexture, -900, -200)
    set_props(scene, scene_texture_id=unreal.SceneTextureId.PPI_POST_PROCESS_INPUT0)
    stencil = MEL.create_material_expression(pp, unreal.MaterialExpressionSceneTexture, -900, 0)
    set_props(stencil, scene_texture_id=unreal.SceneTextureId.PPI_CUSTOM_STENCIL)
    world_pos = MEL.create_material_expression(pp, unreal.MaterialExpressionWorldPosition, -900, 200)
    params = {}
    for i, (pname, kind, default) in enumerate((("ScanOrigin", "vector", None), ("ScanRadius", "scalar", 0.0),
                                                 ("ScanProgress", "scalar", 0.0), ("Fade", "scalar", 0.0))):
        if kind == "vector":
            node = MEL.create_material_expression(pp, unreal.MaterialExpressionVectorParameter, -900, 350 + i * 120)
            set_props(node, parameter_name=pname, default_value=unreal.LinearColor(0, 0, 0, 0))
        else:
            node = MEL.create_material_expression(pp, unreal.MaterialExpressionScalarParameter, -900, 350 + i * 120)
            set_props(node, parameter_name=pname, default_value=default)
        params[pname] = node
    custom = MEL.create_material_expression(pp, unreal.MaterialExpressionCustom, -350, 0)
    code = (
        "float s = round(Stencil.r);\n"
        "float3 base = Scene.rgb;\n"
        "float lum = dot(base, float3(0.3, 0.59, 0.11));\n"
        "float d = length(WP.xy - Origin.xy) + abs(WP.z - Origin.z) * 0.35;\n"
        "float inside = saturate((Radius - d) / 250.0 + step(0.999, Progress));\n"
        "float3 cool = lum.xxx * float3(0.42, 0.5, 0.62);\n"
        "float3 c = lerp(base, lerp(base * 0.85, cool, 0.9), inside);\n"
        "float front = exp(-abs(d - Radius) / 45.0) * (1.0 - Progress * 0.85);\n"
        "float2 g = abs(frac(WP.xy / 160.0) - 0.5);\n"
        "float grid = saturate(1.0 - min(g.x, g.y) * 55.0) * inside * saturate(1.0 - d / 3500.0);\n"
        "float flatFloor = saturate((Origin.z + 60.0 - WP.z) / 120.0);\n"
        "c += float3(0.55, 0.78, 1.0) * (front * 0.9 + grid * 0.16 * flatFloor);\n"
        "if (s > 0.5 && s < 1.5) c = lerp(c, float3(1.0, 1.0, 1.0), 0.85);\n"
        "if (s > 1.5 && s < 2.5) c = lerp(c, float3(0.95, 0.97, 1.0), 0.55);\n"
        "if (s > 2.5 && s < 3.5) c = float3(1.0, 0.06, 0.05);\n"
        "if (s > 3.5 && s < 4.5) c = float3(1.0, 0.78, 0.1);\n"
        "return lerp(base, c, Fade);"
    )
    inputs = []
    for input_name in ("Scene", "Stencil", "WP", "Origin", "Radius", "Progress", "Fade"):
        ci = unreal.CustomInput()
        ci.set_editor_property("input_name", input_name)
        inputs.append(ci)
    set_props(custom, code=code, output_type=unreal.CustomMaterialOutputType.CMOT_FLOAT3, description="Sightroom", inputs=inputs)
    MEL.connect_material_expressions(scene, "Color", custom, "Scene")
    MEL.connect_material_expressions(stencil, "Color", custom, "Stencil")
    MEL.connect_material_expressions(world_pos, "", custom, "WP")
    MEL.connect_material_expressions(params["ScanOrigin"], "", custom, "Origin")
    MEL.connect_material_expressions(params["ScanRadius"], "", custom, "Radius")
    MEL.connect_material_expressions(params["ScanProgress"], "", custom, "Progress")
    MEL.connect_material_expressions(params["Fade"], "", custom, "Fade")
    MEL.connect_material_property(custom, "", unreal.MaterialProperty.MP_EMISSIVE_COLOR)
    MEL.recompile_material(pp)
    save(pp)

    # Pill: wavy, colour-shifted view. Weight = blendable weight driven by the status component.
    pill = new_material("PP_Pill", path("Materials", "PostProcess"))
    set_props(pill, material_domain=unreal.MaterialDomain.MD_POST_PROCESS)
    pill_scene = MEL.create_material_expression(pill, unreal.MaterialExpressionSceneTexture, -700, 0)
    set_props(pill_scene, scene_texture_id=unreal.SceneTextureId.PPI_POST_PROCESS_INPUT0)
    pill_uv = MEL.create_material_expression(pill, unreal.MaterialExpressionScreenPosition, -1100, 0)
    pill_time = MEL.create_material_expression(pill, unreal.MaterialExpressionTime, -1100, 200)

    def custom_node(material, x, y, names, output, code, description):
        node = MEL.create_material_expression(material, unreal.MaterialExpressionCustom, x, y)
        node_inputs = []
        for input_name in names:
            ci = unreal.CustomInput()
            ci.set_editor_property("input_name", input_name)
            node_inputs.append(ci)
        set_props(node, output_type=output, description=description, inputs=node_inputs, code=code)
        return node

    # Wavy UVs -> scene colour sampled there -> hue rotation that breathes over time.
    pill_warp = custom_node(pill, -900, 0, ("UV", "T"), unreal.CustomMaterialOutputType.CMOT_FLOAT2,
                            "return UV + float2(sin(UV.y * 18 + T * 2.3), cos(UV.x * 14 + T * 1.7)) * 0.006;", "PillWarp")
    pill_shift = custom_node(pill, -300, 0, ("C", "T"), unreal.CustomMaterialOutputType.CMOT_FLOAT3,
                             "float3 s = float3(C.g, C.b, C.r);\nreturn lerp(C.rgb, s, 0.35 + 0.15 * sin(T * 1.3)) * 1.1;", "PillShift")
    MEL.connect_material_expressions(pill_uv, "ViewportUV", pill_warp, "UV")
    MEL.connect_material_expressions(pill_time, "", pill_warp, "T")
    MEL.connect_material_expressions(pill_warp, "", pill_scene, "UVs")
    MEL.connect_material_expressions(pill_scene, "Color", pill_shift, "C")
    MEL.connect_material_expressions(pill_time, "", pill_shift, "T")
    MEL.connect_material_property(pill_shift, "", unreal.MaterialProperty.MP_EMISSIVE_COLOR)
    MEL.recompile_material(pill)
    save(pill)

    # Flashbang blinding: the scene washes out to white.
    blind = new_material("PP_Blind", path("Materials", "PostProcess"))
    set_props(blind, material_domain=unreal.MaterialDomain.MD_POST_PROCESS)
    blind_scene = MEL.create_material_expression(blind, unreal.MaterialExpressionSceneTexture, -700, 0)
    set_props(blind_scene, scene_texture_id=unreal.SceneTextureId.PPI_POST_PROCESS_INPUT0)
    blind_white = MEL.create_material_expression(blind, unreal.MaterialExpressionConstant3Vector, -700, 200)
    set_props(blind_white, constant=unreal.LinearColor(1.6, 1.6, 1.6, 1))
    blind_lerp = MEL.create_material_expression(blind, unreal.MaterialExpressionLinearInterpolate, -300, 0)
    blind_alpha = MEL.create_material_expression(blind, unreal.MaterialExpressionConstant, -700, 350)
    set_props(blind_alpha, r=0.92)
    MEL.connect_material_expressions(blind_scene, "Color", blind_lerp, "A")
    MEL.connect_material_expressions(blind_white, "", blind_lerp, "B")
    MEL.connect_material_expressions(blind_alpha, "", blind_lerp, "Alpha")
    MEL.connect_material_property(blind_lerp, "", unreal.MaterialProperty.MP_EMISSIVE_COLOR)
    MEL.recompile_material(blind)
    save(blind)

    # Smoke puffs: translucent grey, faded through the "Opacity" parameter.
    smoke = new_material("M_Smoke", folder)
    set_props(smoke, blend_mode=unreal.BlendMode.BLEND_TRANSLUCENT, shading_model=unreal.MaterialShadingModel.MSM_UNLIT, two_sided=True)
    smoke_color = MEL.create_material_expression(smoke, unreal.MaterialExpressionVectorParameter, -500, 0)
    set_props(smoke_color, parameter_name="Color", default_value=unreal.LinearColor(0.55, 0.55, 0.58, 1))
    smoke_opacity = MEL.create_material_expression(smoke, unreal.MaterialExpressionScalarParameter, -500, 200)
    set_props(smoke_opacity, parameter_name="Opacity", default_value=0.85)
    MEL.connect_material_property(smoke_color, "", unreal.MaterialProperty.MP_EMISSIVE_COLOR)
    MEL.connect_material_property(smoke_opacity, "", unreal.MaterialProperty.MP_OPACITY)
    MEL.recompile_material(smoke)
    save(smoke)

    # Monitor of a disabled camera: animated grey static.
    no_signal = new_material("M_Monitor_NoSignal", folder)
    set_props(no_signal, shading_model=unreal.MaterialShadingModel.MSM_UNLIT)
    ns_uv = MEL.create_material_expression(no_signal, unreal.MaterialExpressionTextureCoordinate, -900, 0)
    ns_time = MEL.create_material_expression(no_signal, unreal.MaterialExpressionTime, -900, 200)
    ns_noise = MEL.create_material_expression(no_signal, unreal.MaterialExpressionCustom, -400, 0)
    ns_inputs = []
    for input_name in ("UV", "T"):
        ci = unreal.CustomInput()
        ci.set_editor_property("input_name", input_name)
        ns_inputs.append(ci)
    set_props(ns_noise, output_type=unreal.CustomMaterialOutputType.CMOT_FLOAT3, description="NoSignal", inputs=ns_inputs,
              code="float2 p = floor(UV * 160 + frac(T * 7.0) * 97.0);\nfloat n = frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);\nreturn n.xxx * 0.35;")
    MEL.connect_material_expressions(ns_uv, "", ns_noise, "UV")
    MEL.connect_material_expressions(ns_time, "", ns_noise, "T")
    MEL.connect_material_property(ns_noise, "", unreal.MaterialProperty.MP_EMISSIVE_COLOR)
    MEL.recompile_material(no_signal)
    save(no_signal)

    # Focus highlight: additive white fresnel drawn over whatever the player looks at (overlay material).
    focus = new_material("M_FocusFresnel", folder)
    # Used on skeletal guns too (launchers): without the usage flag the overlay draws nothing there.
    set_props(focus, blend_mode=unreal.BlendMode.BLEND_ADDITIVE, shading_model=unreal.MaterialShadingModel.MSM_UNLIT, used_with_skeletal_mesh=True)
    fresnel = MEL.create_material_expression(focus, unreal.MaterialExpressionFresnel, -700, 0)
    set_props(fresnel, exponent=2.6, base_reflect_fraction=0.06)
    focus_color = MEL.create_material_expression(focus, unreal.MaterialExpressionVectorParameter, -700, 200)
    set_props(focus_color, parameter_name="Color", default_value=unreal.LinearColor(1.0, 1.0, 1.0, 1))
    focus_strength = MEL.create_material_expression(focus, unreal.MaterialExpressionScalarParameter, -700, 350)
    set_props(focus_strength, parameter_name="Strength", default_value=0.8)
    focus_pulse = MEL.create_material_expression(focus, unreal.MaterialExpressionCustom, -450, 350)
    focus_time = MEL.create_material_expression(focus, unreal.MaterialExpressionTime, -700, 500)
    pulse_input = unreal.CustomInput()
    pulse_input.set_editor_property("input_name", "T")
    set_props(focus_pulse, output_type=unreal.CustomMaterialOutputType.CMOT_FLOAT1, description="Pulse", inputs=[pulse_input],
              code="return 0.8 + 0.2 * sin(T * 4.0);")
    MEL.connect_material_expressions(focus_time, "", focus_pulse, "T")
    focus_mul = MEL.create_material_expression(focus, unreal.MaterialExpressionMultiply, -250, 0)
    focus_mul2 = MEL.create_material_expression(focus, unreal.MaterialExpressionMultiply, -250, 200)
    focus_mul3 = MEL.create_material_expression(focus, unreal.MaterialExpressionMultiply, -100, 100)
    MEL.connect_material_expressions(fresnel, "", focus_mul, "A")
    MEL.connect_material_expressions(focus_color, "", focus_mul, "B")
    MEL.connect_material_expressions(focus_strength, "", focus_mul2, "A")
    MEL.connect_material_expressions(focus_pulse, "", focus_mul2, "B")
    MEL.connect_material_expressions(focus_mul, "", focus_mul3, "A")
    MEL.connect_material_expressions(focus_mul2, "", focus_mul3, "B")
    MEL.connect_material_property(focus_mul3, "", unreal.MaterialProperty.MP_EMISSIVE_COLOR)
    MEL.recompile_material(focus)
    save(focus)

    remote = create("MI_FocusFresnel_Remote", inst_folder, unreal.MaterialInstanceConstant, unreal.MaterialInstanceConstantFactoryNew())
    set_props(remote, parent=focus)
    MEL.set_material_instance_vector_parameter_value(remote, "Color", unreal.LinearColor(0.35, 0.75, 1.0, 1))
    MEL.set_material_instance_scalar_parameter_value(remote, "Strength", 2.0)
    save(remote)
    laser = create("MI_Laser", inst_folder, unreal.MaterialInstanceConstant, unreal.MaterialInstanceConstantFactoryNew())
    set_props(laser, parent=cone)
    MEL.set_material_instance_vector_parameter_value(laser, "Color", unreal.LinearColor(4.0, 0.05, 0.03, 0.85))
    save(laser)

    log("materials:", len(instances), "instances")
    return {"remote": remote, "laser": laser, "color": color, "cone": cone, "decal": decal, "screen": screen, "pp": pp, "mi": instances,
            "pp_pill": pill, "pp_blind": blind, "smoke": smoke, "no_signal": no_signal, "focus": focus}


# ---------------------------------------------------------------------------------------------
# Animation
# ---------------------------------------------------------------------------------------------

ANIMS = "/Game/Characters/Mannequins/Anims"


def montage(name, source):
    seq = load(source)
    if not seq:
        log("WARN missing anim", source)
        return None
    factory = unreal.AnimMontageFactory()
    factory.set_editor_property("source_animation", seq)
    factory.set_editor_property("target_skeleton", seq.get_editor_property("skeleton"))
    m = create(name, path("Characters", "Shared", "Montages"), unreal.AnimMontage, factory)
    save(m)
    return m



def set_footstep_sounds(character_cdo):
    """Footstep sounds per physical surface (PM_Grass / Stone / Sand / Wood / Metal) and a default."""
    steps1 = ESS + "SFX_Essentials_11_FOOTSTEPS_01/Cues/FEETHmn_InsJ_Footsteps_"
    steps2 = ESS + "SFX_Essentials_20_FOOTSTEPS_02/Cues/FEETHmn_InsJ_Footsteps_"
    P = unreal.PhysicalSurface
    table = {P.SURFACE_TYPE1: (steps1 + "Grass_Running_Trainers_01-01_Single-01_Cue", 0.8),
             P.SURFACE_TYPE2: (steps2 + "Stones_Running_Trainers_01-01_Single-01_Cue", 1.0),
             P.SURFACE_TYPE3: (steps2 + "Dirt_Running_Trainers_01-01_Single-01_Cue", 0.6),
             P.SURFACE_TYPE4: (steps2 + "Wood_Jogging_Trainers_02-01_Single_01_Cue", 1.1),
             P.SURFACE_TYPE5: (steps2 + "Glass_Running_Trainers_03-01_Single-01_Cue", 1.4)}
    comp = component(character_cdo, "footstep_component")
    if not comp:
        return
    feedback = {}
    for surface, (sound_path, noise) in table.items():
        f = unreal.HitmenSurfaceFeedback()
        f.set_editor_property("sound", load(sound_path))
        f.set_editor_property("noise_multiplier", noise)
        feedback[surface] = f
    default = unreal.HitmenSurfaceFeedback()
    default.set_editor_property("sound", load(steps1 + "Concrete_Jogging_Trainers_01-01_Single-01_Cue"))
    set_props(comp, surface_feedback=feedback, default_feedback=default)

def build_animation():
    folder = path("Characters", "Shared", "Anims")
    ensure_folder(folder)
    abp_path = folder + "/ABP_HitmenCharacter"
    if not EAL.does_asset_exist(abp_path):
        EAL.duplicate_asset(ANIMS + "/Unarmed/ABP_Unarmed", abp_path)
    abp = load(abp_path)
    if abp:
        unreal.BlueprintEditorLibrary.reparent_blueprint(abp, unreal.HitmenAnimInstance.static_class())
    if abp:
        unreal.BlueprintEditorLibrary.compile_blueprint(abp)
        save(abp)

    punch = montage("AM_Punch", ANIMS + "/Unarmed/Attack/MM_Attack_01")
    kill = montage("AM_StealthKill", ANIMS + "/Unarmed/Attack/MM_Attack_03")
    deaths = [montage("AM_Death_" + suffix, ANIMS + "/Death/MM_Death_" + suffix) for suffix in ("Front_01", "Back_01", "Left_01", "Right_01")]
    wave = montage("AM_Emote_Wave", ANIMS + "/Unarmed/Attack/MM_Attack_02")
    charged = montage("AM_Emote_Taunt", ANIMS + "/Unarmed/Attack/MM_ChargedAttack")
    dash = montage("AM_Emote_Hop", ANIMS + "/Unarmed/Jump/MM_Dash")

    anim_set = make_data("DA_AnimSet_Mannequin", path("Data", "AnimSets"), unreal.HitmenAnimSet)
    emotes = []
    for display, m in (("Wave", wave), ("Taunt", charged), ("Hop", dash)):
        e = unreal.HitmenEmote()
        e.set_editor_property("display_name", display)
        e.set_editor_property("montage", m)
        emotes.append(e)
    set_props(anim_set,
              idle=load(ANIMS + "/Unarmed/MM_Idle"),
              locomotion=load(ANIMS + "/Unarmed/BS_Idle_Walk_Run"),
              jump_start=load(ANIMS + "/Unarmed/Jump/MM_Jump"),
              fall_loop=load(ANIMS + "/Unarmed/Jump/MM_Fall_Loop"),
              land=load(ANIMS + "/Unarmed/Jump/MM_Land"),
              punch_montage=punch, stealth_kill_attacker_montage=kill,
              first_person_unarmed_pose=load(ANIMS + "/Unarmed/MM_Idle"),
              death_montages=[d for d in deaths if d], emotes=emotes,
              menu_idles=[load(ANIMS + "/Unarmed/MM_Idle")])
    save(anim_set)

    if abp:
        # The ABP reads every animation from the anim set (UHitmenAnimInstance variables), plays
        # montages through a slot, and previews the mannequin set in the editor.
        try:
            unreal.get_default_object(abp.generated_class()).set_editor_property("anim_set", anim_set)
        except Exception as error:
            log("WARN anim set default on ABP:", error)
        unreal.HitmenAnimGraphTools.ensure_slot_before_output(abp, "DefaultSlot", False)
        wired = unreal.HitmenAnimGraphTools.bind_asset_pins_to_anim_set(abp, anim_set, True)
        log("animation: ABP asset pins bound to the anim set:", wired)
        save(abp)

    locomotion = load(ANIMS + "/Unarmed/BS_Idle_Walk_Run")
    if locomotion:
        # Only the dominant clip of the blend fires footsteps (walk and run would both step).
        try:
            set_props(locomotion, notify_trigger_mode=unreal.NotifyTriggerMode.HIGHEST_WEIGHTED_ANIMATION)
            save(locomotion)
        except Exception as error:
            log("WARN blend space notify mode:", error)
    add_footstep_notifies()
    return abp, anim_set


def add_footstep_notifies():
    """Two steps per cycle (left at 0, right at half) on every walk / jog clip; idempotent."""
    count = 0
    for gait in ("Walk", "Jog"):
        for direction in ("Fwd", "Bwd", "Left", "Right", "Fwd_Left", "Fwd_Right", "Bwd_Left", "Bwd_Right"):
            seq = load("%s/Unarmed/%s/MF_Unarmed_%s_%s" % (ANIMS, gait, gait, direction))
            if not seq or unreal.AnimationLibrary.is_valid_anim_notify_track_name(seq, "Footsteps"):
                continue
            unreal.AnimationLibrary.add_animation_notify_track(seq, "Footsteps")
            length = unreal.AnimationLibrary.get_sequence_length(seq)
            for time, bone in ((0.0, "foot_l"), (length * 0.5, "foot_r")):
                notify = unreal.AnimationLibrary.add_animation_notify_event(seq, "Footsteps", time, unreal.AnimNotify_HitmenFootstep)
                if notify:
                    notify.set_editor_property("foot_bone", bone)
            save(seq)
            count += 1
    log("animation: footstep notifies added to", count, "clips")


# ---------------------------------------------------------------------------------------------
# Projectiles, weapons
# ---------------------------------------------------------------------------------------------

FX = "/Game/Vefects/Flipbook_VFX/Shared/Particles/"
SFX = "/Game/Vefects/Flipbook_VFX/Shared/Audio/"
ESS = "/Game/SFX_Essentials_FREE_SAMPLE/"


def fx(rel):
    return load(FX + rel)


def projectile_bp(name, parent, mesh="sphere", scale=(0.1, 0.1, 0.1), material=None, rotation=None, **props):
    bp = make_blueprint(name, path("Weapons", "Projectiles"), parent)
    d = cdo(bp)
    # Cylinders are modelled along Z: lay them along the flight axis (X).
    set_mesh(component(d, "mesh"), SHAPES[mesh], scale, material, rotation=rotation or ((90, 0, 0) if mesh in ("cylinder", "cone") else None))
    movement_props = props.pop("movement", {})
    set_props(d, **props)
    if movement_props:
        set_props(component(d, "movement"), **movement_props)
    save(bp)
    return bp


def build_weapons(mats):
    mi = mats["mi"]
    folder = path("Weapons", "Blueprints")
    hitscan = make_blueprint("BP_Weapon_Hitscan", folder, unreal.HitmenWeapon_Hitscan)
    projectile = make_blueprint("BP_Weapon_Projectile", folder, unreal.HitmenWeapon_Projectile)
    melee = make_blueprint("BP_Weapon_Melee", folder, unreal.HitmenWeapon_Melee)
    shotgun = make_blueprint("BP_Weapon_Shotgun", folder, unreal.HitmenWeapon_Hitscan)
    set_props(cdo(shotgun), pellet_spread=6.0)
    pickup = make_blueprint("BP_WeaponPickup", path("Weapons", "Pickups"), unreal.HitmenWeaponPickup)
    for bp in (hitscan, projectile, melee, shotgun, pickup):
        save(bp)

    # Explosions: 3D toon fire and smoke (Anime_Stylized_VFX), never the 2D flipbook sheets (they read as comic book).
    toon = "/Game/Vefects/Anime_Stylized_VFX/VFX/Particles/"
    explosion_fx = load(toon + "NS_Bomb_Explosion")
    ground_fx = load(toon + "NS_Explosion_Fire_Ground")
    explosion_sfx = load(SFX + "SFX_ExplosionFire")
    impact_fx = fx("Impact/OneShot/NS_Impact_01_OS")
    impact_sfx = load(SFX + "SFX_Impact_01")
    smoke_trail = load("/Game/Vefects/Essential_Trails_VFX/Shared/Particles/NS_Trail_Simple_02")
    arrow = projectile_bp("BP_Projectile_Arrow", unreal.HitmenProjectile, "cylinder", (0.025, 0.025, 0.7), mi["MI_Wood"],
                          stick_on_impact=True, impact_noise_radius=300.0, impact_effect=None, impact_sound=impact_sfx,
                          movement={"projectile_gravity_scale": 0.6, "rotation_follows_velocity": True})
    rocket = projectile_bp("BP_Projectile_Rocket", unreal.HitmenProjectile, "cylinder", (0.12, 0.12, 0.6), mi["MI_Dark"],
                           explode_on_impact=True, explosion_noise_radius=1500.0, explosion_radius=500.0, explosion_damage=140.0, explosion_effect=explosion_fx,
                           explosion_sound=explosion_sfx, explosion_effect_scale=1.6, trail_effect=smoke_trail,
                           movement={"projectile_gravity_scale": 0.0, "rotation_follows_velocity": True})
    grenade = projectile_bp("BP_Projectile_Grenade", unreal.HitmenProjectile, "sphere", (0.15, 0.15, 0.15), mi["MI_Green"],
                            fuse_time=2.0, explosion_noise_radius=1500.0, explosion_radius=450.0, explosion_damage=120.0, explosion_effect=ground_fx,
                            explosion_sound=explosion_sfx, explosion_effect_scale=1.3,
                            movement={"should_bounce": True, "bounciness": 0.4})
    blackhole = projectile_bp("BP_Projectile_BlackHole", unreal.HitmenProjectile_BlackHole, "sphere", (0.3, 0.3, 0.3), mi["MI_Dark"],
                              movement={"projectile_gravity_scale": 0.0})
    stone = projectile_bp("BP_Projectile_Stone", unreal.HitmenProjectile, "sphere", (0.08, 0.08, 0.08), mi["MI_Stone"],
                          impact_noise_radius=1600.0, impact_noise_type=unreal.HitmenNoiseType.DISTRACTION)
    dart = projectile_bp("BP_Projectile_SleepDart", unreal.HitmenProjectile, "cylinder", (0.02, 0.02, 0.2), mi["MI_Blue"],
                         status_on_hit=unreal.HitmenStatusEffect.ASLEEP, status_duration=25.0, movement={"projectile_gravity_scale": 0.0})
    flash = projectile_bp("BP_Projectile_Flashbang", unreal.HitmenProjectile_Flashbang, "cylinder", (0.08, 0.08, 0.15), mi["MI_White"],
                          explosion_effect=load(toon + "NS_Explosion_Ice_Omni"), explosion_sound=load(SFX + "SFX_ExplosionClassic"),
                          explosion_effect_scale=0.8, explosion_shake_intensity=1.2)
    smoke = projectile_bp("BP_Projectile_Smoke", unreal.HitmenProjectile_Smoke, "cylinder", (0.08, 0.08, 0.15), mi["MI_Metal"],
                          explosion_sound=load(SFX + "SFX_DustPuff_Cue"), explosion_shake_intensity=0.0)
    c4 = projectile_bp("BP_Projectile_C4", unreal.HitmenC4Charge, "cube", (0.15, 0.1, 0.05), mi["MI_Red"], explosion_effect=explosion_fx, explosion_effect_scale=1.6)
    # Tank shell: a glowing slug that flies straight (no gravity, near-instant), then a big blast.
    shell = projectile_bp("BP_Projectile_TankShell", unreal.HitmenProjectile, "sphere", (0.35, 0.35, 0.35), mats["laser"],
                          explode_on_impact=True, explosion_noise_radius=1500.0, explosion_radius=1200.0, explosion_damage=260.0, explosion_effect=explosion_fx,
                          explosion_sound=explosion_sfx, explosion_effect_scale=3.5, explosion_shake_intensity=6.0, trail_effect=smoke_trail,
                          movement={"projectile_gravity_scale": 0.0, "rotation_follows_velocity": True})

    # Gun pack (Weapons_Free) for firearms and melee; the launchers keep the template launcher.
    free = "/Game/Weapons_Free/Meshes/"
    pistol_mesh = load(free + "SM_pistol_001")
    rifle_mesh = load(free + "SM_rifle_001")
    shotgun_mesh = load(free + "SM_shotgun_001")
    sniper_mesh = load(free + "SM_sniper_rifle_001")
    knife_mesh = load(free + "SM_knife_001")
    pistol_skel = rifle_skel = None

    def pack(scale, yaw=90.0, loc=(0.0, 0.0, 0.0)):
        t = unreal.Transform()
        t.translation = unreal.Vector(*loc)
        t.rotation = unreal.Rotator(roll=0.0, pitch=0.0, yaw=yaw).quaternion()
        t.scale3d = unreal.Vector(scale, scale, scale)
        return t
    gl_mesh = load("/Game/Weapons/GrenadeLauncher/Meshes/SM_GrenadeLauncher")
    gl_skel = load("/Game/Weapons/GrenadeLauncher/Meshes/SKM_GrenadeLauncher")
    pistol_pose = load(ANIMS + "/Pistol/MF_Pistol_Idle_ADS")
    rifle_pose = load(ANIMS + "/Rifle/MF_Rifle_Idle_ADS")
    pistol_fire = montage("AM_Pistol_Fire", ANIMS + "/Pistol/MM_Pistol_Fire")
    rifle_fire = montage("AM_Rifle_Fire", ANIMS + "/Rifle/MM_Rifle_Fire")
    pistol_equip = montage("AM_Pistol_Equip", ANIMS + "/Pistol/MM_Pistol_Equip")
    rifle_equip = montage("AM_Rifle_Equip", ANIMS + "/Rifle/MM_Rifle_Equip")
    # NPCs in combat hold these aim poses (looped in code) between shots.
    pistol_aim = montage("AM_Pistol_Aim", ANIMS + "/Pistol/MF_Pistol_Idle_ADS")
    rifle_aim = montage("AM_Rifle_Aim", ANIMS + "/Rifle/MF_Rifle_Idle_ADS")
    # Guns carry no VFX (no muzzle flash, impact, dust or blood): the slots stay for the team's own effects.
    gun_fx = dict(muzzle_fx=None, impact_fx=None, surface_impact_fx=None, flesh_impact_fx=None,
                  fire_sound=load(SFX + "SFX_GunShot_Cue"), impact_sound=load(SFX + "SFX_Impact_01"),
                  dry_fire_sound=load(ESS + "SFX_Essentials_02_CLOCKS/Cues/CLOCKTick_InsJ_Clock-01_Ticking_Single-01_Cue"))
    # On the first-person arms the gun is turned so its barrel runs along the view and its top is up. Solved in
    # PIE by Tools/Editor/hitmen_fp_solve.py (the same for every gun, their VisualTransforms share one convention).
    fp_offset = unreal.Transform()
    fp_offset.rotation = unreal.Rotator(roll=9.5, pitch=0.9, yaw=-3.3).quaternion()
    gun_fx["first_person_attach_offset"] = fp_offset
    pistol_vis = dict(first_person_pose=pistol_pose, fire_montage=pistol_fire, equip_montage=pistol_equip, aim_montage=pistol_aim, **gun_fx)
    pistol_vis["fire_sound"] = load(SFX + "SFX_DustPuff_Cue")
    # First person, every gun uses the two-hand pistol hold (the arms offsets are tuned for it); the body keeps the
    # rifle montages. Swap first_person_pose per weapon once dedicated first-person rifle poses exist.
    rifle_vis = dict(first_person_pose=pistol_pose, fire_montage=rifle_fire, equip_montage=rifle_equip, aim_montage=rifle_aim, **gun_fx)
    # Long guns: same hold, a little smaller so the stock does not fill the view.
    long_offset = unreal.Transform()
    long_offset.rotation = fp_offset.rotation
    long_offset.scale3d = unreal.Vector(0.75, 0.75, 0.75)
    rifle_vis["first_person_attach_offset"] = long_offset
    launcher_vis = dict(rifle_vis)
    launcher_offset = unreal.Transform()
    launcher_offset.rotation = fp_offset.rotation
    launcher_offset.scale3d = unreal.Vector(0.55, 0.55, 0.55)
    launcher_vis["first_person_attach_offset"] = launcher_offset
    knife_offset = unreal.Transform()
    knife_offset.rotation = unreal.Rotator(roll=174.8, pitch=-80.4, yaw=-88.1).quaternion()
    bow_offset = unreal.Transform()
    bow_offset.rotation = unreal.Rotator(roll=-89.1, pitch=-9.5, yaw=86.6).quaternion()
    bow_offset.scale3d = unreal.Vector(0.5, 0.5, 0.5)
    launcher_vis["fire_sound"] = load("/Game/Weapons/GrenadeLauncher/Audio/FirstPersonTemplateWeaponFire02")

    A = unreal.HitmenFireMode
    weapons = {
        # name: (display, class bp, props). Recoil is in degrees (kick up, then recovers).
        "Pistol": ("Silenced pistol", hitscan, dict(fire_mode=A.SINGLE, rounds_per_minute=260.0, damage=34.0, headshot_multiplier=3.0,
                   silenced=True, noise_radius=0.0, impact_noise_radius=300.0, start_ammo=24, max_ammo=72, ammo_per_pickup=12, hip_spread=1.2, aim_spread=0.25,
                   recoil_pitch=1.6, recoil_yaw_random=0.35, static_mesh=pistol_mesh, skeletal_mesh=None, visual_transform=pack(0.45), **pistol_vis)),
        "SMG": ("SMG", hitscan, dict(fire_mode=A.AUTOMATIC, rounds_per_minute=900.0, damage=13.0, start_ammo=90, max_ammo=240,
                ammo_per_pickup=45, hip_spread=3.0, aim_spread=1.2, noise_radius=600.0, impact_noise_radius=500.0, recoil_pitch=0.55, recoil_yaw_random=0.35,
                static_mesh=rifle_mesh, skeletal_mesh=None, visual_transform=pack(0.5), **rifle_vis)),
        "AssaultRifle": ("Assault rifle", hitscan, dict(fire_mode=A.AUTOMATIC, rounds_per_minute=620.0, damage=22.0, start_ammo=90,
                         max_ammo=210, ammo_per_pickup=30, hip_spread=2.0, aim_spread=0.5, noise_radius=700.0, impact_noise_radius=600.0, recoil_pitch=0.8,
                         recoil_yaw_random=0.3, static_mesh=rifle_mesh, skeletal_mesh=None, visual_transform=pack(0.5), **rifle_vis)),
        "BurstRifle": ("Burst rifle", hitscan, dict(fire_mode=A.BURST, burst_count=3, rounds_per_minute=780.0, damage=24.0, start_ammo=72,
                       max_ammo=180, ammo_per_pickup=24, hip_spread=1.8, aim_spread=0.4, noise_radius=700.0, impact_noise_radius=600.0, recoil_pitch=0.7,
                       recoil_yaw_random=0.25, static_mesh=rifle_mesh, skeletal_mesh=None, visual_transform=pack(0.5), **rifle_vis)),
        "Shotgun": ("Shotgun", shotgun, dict(fire_mode=A.SINGLE, rounds_per_minute=70.0, damage=12.0, pellet_count=9, range=2500.0,
                    start_ammo=16, max_ammo=40, ammo_per_pickup=6, hip_spread=1.0, aim_spread=0.6, noise_radius=800.0, impact_noise_radius=600.0,
                    recoil_pitch=3.5, recoil_yaw_random=0.6, static_mesh=shotgun_mesh, skeletal_mesh=None, visual_transform=pack(0.5), **rifle_vis)),
        "Minigun": ("Minigun", hitscan, dict(fire_mode=A.AUTOMATIC, rounds_per_minute=1500.0, damage=9.0, spin_up_time=0.8, start_ammo=300,
                    max_ammo=600, ammo_per_pickup=150, hip_spread=3.0, aim_spread=2.0, noise_radius=900.0, impact_noise_radius=700.0, recoil_pitch=0.3,
                    recoil_yaw_random=0.4, static_mesh=rifle_mesh, skeletal_mesh=None, visual_transform=pack(0.5), **rifle_vis)),
        "Sniper": ("Sniper rifle", hitscan, dict(fire_mode=A.SINGLE, rounds_per_minute=40.0, damage=120.0, headshot_multiplier=3.0,
                   range=30000.0, start_ammo=10, max_ammo=30, ammo_per_pickup=5, hip_spread=4.0, aim_spread=0.0, aim_fov=20.0,
                   recoil_pitch=5.0, recoil_yaw_random=0.5, noise_radius=800.0, impact_noise_radius=700.0, scope_overlay=True,
                   static_mesh=sniper_mesh, skeletal_mesh=None, visual_transform=pack(0.55), **rifle_vis)),
        "Bow": ("Bow", projectile, dict(fire_mode=A.SINGLE, rounds_per_minute=60.0, damage=90.0, charge_to_fire=True, full_charge_time=1.0,
                projectile_class=gen_class(arrow), projectile_speed=5000.0, silenced=True, noise_radius=0.0, start_ammo=12, max_ammo=30,
                ammo_per_pickup=6, recoil_pitch=0.8, first_person_pose=pistol_pose, first_person_attach_offset=bow_offset)),
        "RocketLauncher": ("Rocket launcher", projectile, dict(fire_mode=A.SINGLE, rounds_per_minute=40.0, projectile_class=gen_class(rocket),
                           projectile_speed=3000.0, start_ammo=4, max_ammo=8, ammo_per_pickup=2, noise_radius=1200.0, impact_noise_radius=0.0, recoil_pitch=4.0,
                           back_blast_fx=None,
                           static_mesh=gl_mesh, skeletal_mesh=gl_skel, **launcher_vis)),
        "GrenadeLauncher": ("Grenade launcher", projectile, dict(fire_mode=A.SINGLE, rounds_per_minute=70.0, projectile_class=gen_class(grenade),
                            projectile_speed=2200.0, start_ammo=6, max_ammo=12, ammo_per_pickup=3, noise_radius=1000.0, impact_noise_radius=0.0, recoil_pitch=2.5,
                            static_mesh=gl_mesh, skeletal_mesh=gl_skel, **launcher_vis)),
        "BlackHole": ("Black hole gun", projectile, dict(fire_mode=A.SINGLE, rounds_per_minute=20.0, projectile_class=gen_class(blackhole),
                      projectile_speed=1800.0, start_ammo=2, max_ammo=4, ammo_per_pickup=1, noise_radius=500.0, impact_noise_radius=0.0, recoil_pitch=2.0,
                      static_mesh=gl_mesh, skeletal_mesh=gl_skel, **launcher_vis)),
        "Sword": ("Knife", melee, dict(fire_mode=A.SINGLE, rounds_per_minute=90.0, damage=75.0, uses_ammo=False, melee_range=190.0,
                  melee_radius=45.0, silenced=True, noise_radius=0.0, static_mesh=knife_mesh, skeletal_mesh=None, visual_transform=pack(0.5, 0.0),
                  first_person_pose=pistol_pose, first_person_attach_offset=knife_offset)),
    }
    datas = {}
    for name, (display, bp, props) in weapons.items():
        data = make_data("DA_Weapon_" + name, path("Data", "Weapons"), unreal.HitmenWeaponData)
        set_props(data, display_name=display, weapon_class=gen_class(bp), pickup_class=gen_class(pickup), **props)
        save(data)
        datas[name] = data
    log("weapons:", len(datas))
    return datas, {"stone": stone, "dart": dart, "flash": flash, "smoke": smoke, "c4": c4, "shell": shell}


# ---------------------------------------------------------------------------------------------
# Abilities
# ---------------------------------------------------------------------------------------------

def build_abilities(projectiles, mats):
    mi = mats["mi"]
    actor_folder = path("Abilities", "Actors")
    smoke_cloud = make_blueprint("BP_SmokeCloud", actor_folder, unreal.HitmenSmokeCloud)
    smoke_cdo = cdo(smoke_cloud)
    set_props(smoke_cdo, puff_material=mats["smoke"], puff_mesh=load(SHAPES["sphere"]))
    smoke_effect = component(smoke_cdo, "effect")
    if smoke_effect:
        set_props(smoke_effect, asset=load("/Game/Vefects/Smoke_Bombs_VFX/Shared/Particles/NS_SmokeBomb_Classic_L"))
    save(smoke_cloud)
    trap = make_blueprint("BP_Trap", actor_folder, unreal.HitmenTrap)
    set_mesh(component(cdo(trap), "mesh"), SHAPES["cylinder"], (0.8, 0.8, 0.05), mi["MI_Metal"])
    wind = make_blueprint("BP_WindMine", actor_folder, unreal.HitmenWindMine)
    set_mesh(component(cdo(wind), "mesh"), SHAPES["cylinder"], (0.4, 0.4, 0.08), mi["MI_Blue"])
    ability_pickup = make_blueprint("BP_AbilityPickup", path("Abilities", "Pickups"), unreal.HitmenAbilityPickup)
    set_mesh(component(cdo(ability_pickup), "mesh"), SHAPES["sphere"], (0.3, 0.3, 0.3), mi["MI_Gold"], location=(0, 0, 40))
    for bp in (smoke_cloud, trap, wind, ability_pickup):
        save(bp)

    M = unreal.HitmenAbilitySlot.MOVE
    P = unreal.HitmenAbilitySlot.MANIPULATION
    abilities = [
        ("Blink", M, unreal.HitmenAbility_Blink, "Hold to aim, release to teleport.", {}),
        ("DoubleJump", M, unreal.HitmenAbility_DoubleJump, "Jump a second time in the air.", {}),
        ("Jetpack", M, unreal.HitmenAbility_Jetpack, "Hold to fly while fuel lasts.", {}),
        ("Dash", M, unreal.HitmenAbility_Dash, "Burst of speed.", {}),
        ("Hook", M, unreal.HitmenAbility_Hook, "Grapple and get pulled to a surface.", {}),
        ("Transposition", M, unreal.HitmenAbility_Transposition, "Swap places with the aimed character.", {}),
        ("Pill", M, unreal.HitmenAbility_Pill, "Damage, speed and resistance boost. Strange vision.", {}),
        ("Invisibility", M, unreal.HitmenAbility_Invisibility, "Become invisible for a while.", {}),
        ("Armor", M, unreal.HitmenAbility_Armor, "Armor plates and damage resistance.", {}),
        ("C4", M, unreal.HitmenAbility_C4, "Throw a charge, press again to detonate.", {"charge_class": gen_class(projectiles["c4"])}),
        ("Stone", P, unreal.HitmenAbility_Stone, "Throw a stone to make a noise.", {"projectile_class": gen_class(projectiles["stone"])}),
        ("RemoteDistraction", P, unreal.HitmenAbility_RemoteDistraction, "Trigger a distraction from afar.", {}),
        ("SleepDart", P, unreal.HitmenAbility_SleepDart, "Put one target to sleep.", {"projectile_class": gen_class(projectiles["dart"])}),
        ("Charm", P, unreal.HitmenAbility_Charm, "The target fights for you.", {}),
        ("Flashbang", P, unreal.HitmenAbility_Flashbang, "Stuns everyone around.", {"projectile_class": gen_class(projectiles["flash"])}),
        ("SmokeGrenade", P, unreal.HitmenAbility_SmokeGrenade, "Blocks sight.", {"projectile_class": gen_class(projectiles["smoke"])}),
        ("Lighter", P, unreal.HitmenAbility_Lighter, "Make a body vanish.", {}),
        ("MultiKill", P, unreal.HitmenAbility_MultiKill, "Mark targets: kill one, kill all.", {}),
        ("Trap", P, unreal.HitmenAbility_Trap, "Place a trap that holds whoever steps on it.", {"device_class": gen_class(trap)}),
        ("WindMine", P, unreal.HitmenAbility_WindMine, "Place a mine that blows everyone away.", {"device_class": gen_class(wind)}),
        ("PoisonVial", P, unreal.HitmenAbility_PoisonVial, "Poison a drink or a vent.", {}),
    ]
    move, manip = [], []
    for name, slot, cls, description, props in abilities:
        sub = "Move" if slot == M else "Manipulation"
        bp = make_blueprint("BP_Ability_" + name, path("Abilities", sub, name), cls)
        if props:
            set_props(cdo(bp), **props)
        save(bp)
        data = make_data("DA_Ability_" + name, path("Data", "Abilities"), unreal.HitmenAbilityData)
        display = "".join(" " + c if c.isupper() and i > 0 else c for i, c in enumerate(name))
        set_props(data, display_name=display, description=description, slot=slot, ability_class=gen_class(bp), pickup_class=gen_class(ability_pickup))
        save(data)
        (move if slot == M else manip).append(data)
    log("abilities:", len(move), "move,", len(manip), "manipulation")
    return move, manip


# ---------------------------------------------------------------------------------------------
# Cosmetics, catalog, rules, maps
# ---------------------------------------------------------------------------------------------

def build_cosmetics(mats):
    mi = mats["mi"]
    S = unreal.HitmenCosmeticSlot
    # Three slots (head / body / legs), placeholder basic shapes the team replaces with real meshes.
    items = [
        ("Fedora", S.HEAD, "cylinder", (0.32, 0.32, 0.08), "head", (0, 10, 18), mi["MI_Dark"]),
        ("Beanie", S.HEAD, "sphere", (0.24, 0.24, 0.14), "head", (0, 8, 14), mi["MI_Red"]),
        ("Cap", S.HEAD, "cylinder", (0.26, 0.26, 0.05), "head", (0, 10, 16), mi["MI_Blue"]),
        ("Moustache", S.HEAD, "cube", (0.06, 0.012, 0.012), "head", (10, 0, -3), mi["MI_Dark"]),
        ("Glasses", S.HEAD, "cube", (0.16, 0.02, 0.03), "head", (11, 0, 4), mi["MI_Glass"]),
        ("Scarf", S.BODY, "cylinder", (0.22, 0.22, 0.06), "neck_01", (0, 0, 0), mi["MI_Yellow"]),
        ("Backpack", S.BODY, "cube", (0.3, 0.15, 0.4), "spine_03", (0, -25, 0), mi["MI_Green"]),
        ("Vest", S.BODY, "cube", (0.42, 0.28, 0.45), "spine_03", (0, 0, 0), mi["MI_Dark"]),
        ("Belt", S.LEGS, "cylinder", (0.32, 0.32, 0.04), "pelvis", (0, 0, 0), mi["MI_Wood"]),
        ("Holster", S.LEGS, "cube", (0.1, 0.06, 0.2), "thigh_r", (0, 12, -10), mi["MI_Dark"]),
        ("KneePad", S.LEGS, "sphere", (0.12, 0.12, 0.12), "calf_r", (0, 8, 0), mi["MI_Metal"]),
    ]
    result = []
    for name, slot, shape, scale, socket, offset, material in items:
        data = make_data("DA_Cosmetic_" + name, path("Data", "Cosmetics"), unreal.HitmenCosmeticItem)
        transform = unreal.Transform(unreal.Vector(*offset), unreal.Rotator(0, 0, 0), unreal.Vector(*scale))
        set_props(data, display_name=name, slot=slot, static_mesh=load(SHAPES[shape]), attach_socket=socket, attach_offset=transform)
        save(data)
        result.append(data)
    return result


def build_rules():
    rules = make_data("DA_MatchRules_Default", path("Data", "Rules"), unreal.HitmenMatchRules)
    D = unreal.HitmenDifficulty

    def preset(retrieval, weapons, items, sight, suspicion, damage, aim):
        p = unreal.HitmenDifficultyPreset()
        set_props(p, retrieval_point_count=retrieval, weapon_spawn_count=weapons, item_spawn_count=items, npc_sight_range_multiplier=sight,
                  npc_suspicion_rate_multiplier=suspicion, npc_damage_multiplier=damage, npc_aim_error_degrees=aim)
        return p

    # Damage stays low (with the slow paced fire of the match rules, a fight lasts): Hitman-like.
    set_props(rules, difficulty_presets={D.EASY: preset(4, 10, 10, 0.8, 0.7, 0.25, 7.0),
                                         D.NORMAL: preset(3, 6, 6, 1.0, 1.0, 0.4, 4.0),
                                         D.HARD: preset(2, 4, 4, 1.2, 1.4, 0.6, 2.5)})
    save(rules)
    return rules


FONT_SOURCE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Fonts", "BarlowCondensed")
FONT_FACES = ("Light", "Regular", "Medium", "SemiBold", "Bold", "Italic", "SemiBoldItalic")


def build_fonts():
    """FNT_Barlow: Barlow Condensed (OFL, Tools/Fonts) as one composite font with named typefaces."""
    folder = path("UI", "Fonts")
    ensure_folder(folder)
    faces = []
    for face in FONT_FACES:
        asset_path = folder + "/FF_Barlow_" + face
        if not EAL.does_asset_exist(asset_path):
            task = unreal.AssetImportTask()
            set_props(task, filename=os.path.abspath(os.path.join(FONT_SOURCE, "BarlowCondensed-%s.ttf" % face)),
                      destination_path=folder, destination_name="FF_Barlow_" + face, automated=True, replace_existing=True, save=True)
            AT.import_asset_tasks([task])
        faces.append(load(asset_path))
    if not all(faces):
        log("WARN font faces missing", [f for f, a in zip(FONT_FACES, faces) if not a])
        return None
    font = unreal.HitmenContentTools.build_composite_font(folder + "/FNT_Barlow", faces, list(FONT_FACES))
    save(font)
    log("fonts: FNT_Barlow with", len(faces), "typefaces")
    return font


def build_sound_set():
    """DA_SoundSet: placeholder sounds for every EHitmenSound (Project Settings > Hitmen > Sound Set)."""
    S = unreal.HitmenSound
    hb = "/Game/HeartBeats/"
    clock = ESS + "SFX_Essentials_02_CLOCKS/Cues/CLOCKTick_InsJ_Clock-01_Ticking_Single-0"
    chains = ESS + "SFX_Essentials_04_CHAINS/Cues/CHAINImpt_InsJ_Chains_Metal_"
    whoosh = ESS + "SFX_Essentials_08_SWOOSH/Cues/WHSH_InsJ_Whooshing_Bamboo_01-01_Cue"
    machine = ESS + "SFX_Essentials_12_MACHINES/Cues/MACHOffi_InsJ_Machine-01_Turning-On_01_Cue"
    steps = ESS + "SFX_Essentials_20_FOOTSTEPS_02/Cues/FEETHmn_InsJ_Footsteps_"
    entries = {
        S.UI_HOVER: (clock + "2_Cue", 0.35), S.UI_CLICK: (clock + "1_Cue", 0.6), S.UI_BACK: (whoosh, 0.35),
        # Confirm (team change, ready): the same short tick as a click, a little softer. No match-start sting.
        S.UI_CONFIRM: (clock + "1_Cue", 0.45), S.COUNTDOWN: (clock + "1_Cue", 0.9),
        S.HEARTBEAT: (hb + "Action/Cue/LowHealth_Cue", 0.8), S.PUNCH_WHOOSH: (whoosh, 0.7),
        S.DOOR_OPEN: (chains + "Lifting_Close_02-01_Cue", 0.35), S.DOOR_CLOSE: (chains + "Dropping_Close_01-01_Cue", 0.35),
        S.WINDOW_OPEN: (chains + "Lifting_Close_02-01_Cue", 0.3), S.CONTAINER_OPEN: (chains + "Dropping_Close_01-01_Cue", 0.4),
        S.LOCKER_ENTER: (chains + "Dropping_Close_01-01_Cue", 0.45), S.LOCKER_EXIT: (chains + "Lifting_Close_02-01_Cue", 0.45),
        S.ZIPLINE_RIDE: (whoosh, 0.8), S.CLIMB_GRAB: (steps + "Wood_Jogging_Trainers_02-01_Single_01_Cue", 0.6),
        S.KEYCARD_PICKUP: ("/Game/Vefects/Item_Pickup_VFX/Audio/SND_Item_Rare_L_Cue", 0.5),
        S.WEAPON_PICKUP: (chains + "Lifting_Close_02-01_Cue", 0.4), S.ITEM_PICKUP: ("/Game/Vefects/Item_Pickup_VFX/Audio/SND_Item_Common_L_Cue", 0.5),
        S.EXPLOSION: (SFX + "SFX_ExplosionFire", 1.0), S.SPOTTED: (hb + "Action/Cue/Adrenaline1_Cue", 0.6),
        S.CAMERA_ALARM: (hb + "Futuristic/Cue/AICorePulse1_Cue", 0.7), S.BREAKER_SWITCH: (machine, 0.8),
        S.HACK: (hb + "Futuristic/Cue/Cybernetic1_Cue", 0.6), S.PICKPOCKET: (whoosh, 0.4),
        S.SIGHTROOM_ON: (hb + "Ambient/Cue/ReversedEcho1_Cue", 0.6), S.SIGHTROOM_OFF: (hb + "Ambient/Cue/ReversedEcho2_Cue", 0.4),
        S.LANDING: (steps + "Stones_Running_Trainers_01-01_Single-01_Cue", 0.8), S.PARACHUTE: (whoosh, 0.6),
        S.DEATH: (hb + "Action/Cue/Slowdown1_Cue", 0.8), S.TURRET_LOCK_ON: (hb + "Futuristic/Cue/NeonPulse1_Cue", 0.6),
        S.HEADSHOT_HIT: (chains + "Dropping_Close_01-01_Cue", 0.5), S.HACK_LOOP: (hb + "Futuristic/Cue/Cybernetic1_Cue", 0.45),
        S.HACK_COMPLETE: ("/Game/Vefects/Item_Pickup_VFX/Audio/SND_Item_Rare_L_Cue", 0.55),
        S.RADIO_LOOP: (ESS + "SFX_Essentials_05_GLITCH/Cues/COMRadio_InsJ_Radio_Noise_Long-Wave_01-01_LOOP_Cue", 0.7),
        S.MOUNTED_GUN_FIRE: (SFX + "SFX_GunShot_Cue", 0.8), S.TANK_FIRE: (SFX + "SFX_ExplosionClassic", 1.0),
        S.LANDING_HARD: (steps + "Stones_Running_Trainers_01-01_Single-01_Cue", 1.0),
    }
    data = make_data("DA_SoundSet", path("Data"), unreal.HitmenSoundSet)
    sounds, volumes = {}, {}
    for event, (asset_path, volume) in entries.items():
        asset = load(asset_path)
        if asset:
            sounds[event] = asset
            volumes[event] = volume
        else:
            log("WARN missing sound", asset_path)
    set_props(data, sounds=sounds, volumes=volumes)
    save(data)
    return data


def build_catalog(weapons, move, manip, cosmetics):
    catalog = make_data("DA_Catalog", path("Data"), unreal.HitmenCatalog)
    loadout = [weapons[n] for n in ("Pistol", "SMG", "AssaultRifle", "BurstRifle", "Shotgun", "Sniper", "Bow", "Sword")]
    set_props(catalog, weapons=list(weapons.values()), loot_weapons=[w for k, w in weapons.items() if k != "Pistol"],
              move_abilities=move, manipulation_abilities=manip, cosmetics=cosmetics)
    save(catalog)
    return catalog


def build_maps():
    maps = []
    for name, display, carousel, half in (("LDTest", "San Lazaro", True, 16500.0), ("Empty_01", "Empty 01", True, 10000.0),
                                          ("Empty_02", "Empty 02", True, 10000.0), ("Gym", "Debug gym", False, 3800.0)):
        data = make_data("DA_Map_" + name, path("Data", "Maps"), unreal.HitmenMapDefinition)
        level = unreal.SoftObjectPath(path("Maps", "L_" + name) + ".L_" + name)
        bounds = unreal.Box2D(unreal.Vector2D(-half, -half), unreal.Vector2D(half, half))
        set_props(data, display_name=display, show_in_carousel=carousel, world_bounds=bounds)
        try:
            data.set_editor_property("level", level)
        except Exception as error:
            log("WARN map level", name, error)
        save(data)
        maps.append(data)
    return maps


# ---------------------------------------------------------------------------------------------
# Characters, controllers, game modes, HUD, widgets
# ---------------------------------------------------------------------------------------------

def build_widgets():
    folder_menu = path("UI", "Menus")
    folder_hud = path("UI", "HUD")
    w = {}
    for name, cls, folder in (("WBP_TitleScreen", unreal.HitmenTitleScreenWidget, folder_menu), ("WBP_Lobby", unreal.HitmenLobbyWidget, folder_menu),
                              ("WBP_Options", unreal.HitmenOptionsWidget, folder_menu), ("WBP_PauseMenu", unreal.HitmenPauseMenuWidget, folder_menu),
                              ("WBP_Results", unreal.HitmenResultsWidget, folder_menu), ("WBP_HUD", unreal.HitmenHUDWidget, folder_hud),
                              ("WBP_EmoteWheel", unreal.HitmenEmoteWheelWidget, folder_hud), ("WBP_Spectator", unreal.HitmenSpectatorWidget, folder_hud),
                              ("WBP_AlertIndicator", unreal.HitmenAlertIndicatorWidget, folder_hud)):
        bp = make_widget(name, folder, cls)
        if bp:
            unreal.BlueprintEditorLibrary.compile_blueprint(bp)
            save(bp)
        w[name] = bp
    return w


def build_framework(actions, imc, spectator_imc, abp, anim_set, widgets, mats, weapons):
    core = path("Core")
    pc = make_blueprint("BP_PlayerController", core, unreal.HitmenPlayerController)
    # Last-known-position silhouette: a translucent mannequin ghost in an idle pose.
    ghost_mi = create("MI_LastKnownGhost", path("Materials", "Instances"), unreal.MaterialInstanceConstant, unreal.MaterialInstanceConstantFactoryNew())
    if ghost_mi:
        MEL.set_material_instance_parent(ghost_mi, mats["cone"])
        # Hitman-like: a faint white silhouette.
        MEL.set_material_instance_vector_parameter_value(ghost_mi, "Color", unreal.LinearColor(0.95, 0.97, 1.0, 0.15))
        save(ghost_mi)
    marker = make_blueprint("BP_LastKnownGhost", path("Characters", "Player"), unreal.SkeletalMeshActor)
    skel = component(cdo(marker), "skeletal_mesh_component")
    if skel:
        manny_ghost = load("/Game/Characters/Mannequins/Meshes/SKM_Manny_Simple")
        set_props(skel, skeletal_mesh_asset=manny_ghost, cast_shadow=False, mobility=unreal.ComponentMobility.MOVABLE,
                  animation_mode=unreal.AnimationMode.ANIMATION_SINGLE_NODE)
        skel.set_collision_profile_name("NoCollision")
        idle = load("/Game/Characters/Mannequins/Anims/Unarmed/MM_Idle")
        if idle:
            data = unreal.SingleAnimationPlayData()
            set_props(data, anim_to_play=idle, saved_looping=True, saved_playing=False)
            set_props(skel, animation_data=data)
        if ghost_mi and manny_ghost:
            set_props(skel, override_materials=[ghost_mi] * max(1, len(manny_ghost.get_editor_property("materials"))))
    save(marker)

    # The agent standing in the front-end scene (title backdrop, "Your agent" screen).
    preview = make_blueprint("BP_AgentPreview", path("Characters", "Player"), unreal.HitmenAgentPreview)
    preview_body = component(cdo(preview), "body")
    if preview_body:
        set_props(preview_body, skeletal_mesh_asset=load("/Game/Characters/Mannequins/Meshes/SKM_Manny_Simple"))
    set_props(cdo(preview), idle_animation=load("/Game/Characters/Mannequins/Anims/Unarmed/MM_Idle"))
    save(preview)

    set_props(cdo(pc), gameplay_mapping_contexts=[imc], spectator_mapping_context=spectator_imc,
              move_action=actions["IA_Move"], look_action=actions["IA_Look"], jump_action=actions["IA_Jump"],
              crouch_action=actions["IA_Crouch"], sprint_action=actions["IA_Sprint"], fire_action=actions["IA_Fire"],
              aim_action=actions["IA_Aim"], interact_action=actions["IA_Interact"], move_ability_action=actions["IA_MoveAbility"],
              manipulation_ability_action=actions["IA_ManipulationAbility"], switch_slot_action=actions["IA_SwitchSlot"],
              sightroom_action=actions["IA_Sightroom"], emote_wheel_action=actions["IA_EmoteWheel"], pause_action=actions["IA_Pause"],
              spectator_mode_action=actions["IA_SpectatorMode"], last_known_position_marker_class=gen_class(marker))
    save(pc)

    manny = load("/Game/Characters/Mannequins/Meshes/SKM_Manny_Simple")
    quinn = load("/Game/Characters/Mannequins/Meshes/SKM_Quinn_Simple")
    fp_abp = build_first_person_abp(manny)

    player = make_blueprint("BP_PlayerCharacter", path("Characters", "Player"), unreal.HitmenPlayerCharacter)
    pcdo = cdo(player)
    body = component(pcdo, "mesh")
    if body:
        set_props(body, skeletal_mesh_asset=manny, anim_class=gen_class(abp))
    fp = component(pcdo, "first_person_mesh")
    if fp:
        set_props(fp, skeletal_mesh_asset=manny, anim_class=gen_class(fp_abp) if fp_abp else gen_class(abp))
    set_props(pcdo, anim_set=anim_set)
    set_footstep_sounds(pcdo)
    interaction = component(pcdo, "interaction_component")
    if interaction:
        set_props(interaction, focus_overlay_material=mats["focus"])
    abilities_comp = component(pcdo, "ability_component")
    if abilities_comp:
        set_props(abilities_comp, hint_overlay_material=mats["remote"])
    sight = component(pcdo, "sightroom_component")
    if sight:
        set_props(sight, post_process_material=mats["pp"])
    status = component(pcdo, "status_effect_component")
    if status:
        set_props(status, pill_post_process_material=mats["pp_pill"], blind_post_process_material=mats["pp_blind"])
    save(player)

    ai = make_blueprint("BP_AIController", path("AI"), unreal.HitmenAIController)
    bt = load(path("AI", "BehaviorTrees", "BT_NPC"))
    if bt:
        set_props(cdo(ai), behavior_tree=bt)
    save(ai)
    # A charmed player is possessed by the same behavior-tree controller as the NPCs.
    set_props(cdo(player), charmed_controller_class=gen_class(ai))
    save(player)

    npc = make_blueprint("BP_NPC_Guard", path("Characters", "NPC"), unreal.HitmenNPC)
    ncdo = cdo(npc)
    nbody = component(ncdo, "mesh")
    if nbody:
        set_props(nbody, skeletal_mesh_asset=quinn, anim_class=gen_class(abp))
    keycard_bp = load(path("World", "Interactables", "Keycard", "BP_KeycardPickup"))
    set_props(ncdo, anim_set=anim_set, ai_controller_class=gen_class(ai), default_weapon=weapons["Pistol"],
              keycard_pickup_class=gen_class(keycard_bp) if keycard_bp else None)
    set_mesh(component(ncdo, "aim_laser"), SHAPES["cylinder"], None, mats["laser"])
    set_mesh(component(ncdo, "keycard_mesh"), SHAPES["cube"], None, mats["mi"]["MI_Blue"])
    alert = component(ncdo, "alert_widget")
    if alert and widgets.get("WBP_AlertIndicator"):
        set_props(alert, widget_class=gen_class(widgets["WBP_AlertIndicator"]))
    save(npc)
    sniper = make_blueprint("BP_NPC_Sniper", path("Characters", "NPC"), gen_class(npc))
    set_props(cdo(sniper), is_sniper=True, default_weapon=weapons["Sniper"])
    save(sniper)
    vip = make_blueprint("BP_NPC_VIPCandidate", path("Characters", "NPC"), gen_class(npc))
    set_props(cdo(vip), vip_candidate=True, default_weapon=None)
    vbody = component(cdo(vip), "mesh")
    if vbody:
        set_props(vbody, skeletal_mesh_asset=manny)
    save(vip)
    # Civilians do not exist in Hitmen: every NPC is a guard (patrol, stationary, sentry, sniper) or a VIP candidate.
    civilian_path = path("Characters", "NPC", "BP_NPC_Civilian")
    if EAL.does_asset_exist(civilian_path):
        EAL.delete_asset(civilian_path)

    huds = {}
    hud_folder = path("UI", "HUD")
    for name, props in (("BP_HUD_Menu", dict(primary_widget_class=gen_class(widgets["WBP_TitleScreen"]), primary_is_menu=True, options_class=gen_class(widgets["WBP_Options"]))),
                        ("BP_HUD_Lobby", dict(primary_widget_class=gen_class(widgets["WBP_TitleScreen"]), primary_is_menu=True, options_class=gen_class(widgets["WBP_Options"]))),
                        ("BP_HUD_Match", dict(primary_widget_class=gen_class(widgets["WBP_HUD"]), pause_menu_class=gen_class(widgets["WBP_PauseMenu"]),
                                              options_class=gen_class(widgets["WBP_Options"]), emote_wheel_class=gen_class(widgets["WBP_EmoteWheel"]),
                                              spectator_class=gen_class(widgets["WBP_Spectator"]), results_class=gen_class(widgets["WBP_Results"])))):
        bp = make_blueprint(name, hud_folder, unreal.HitmenHUD)
        set_props(cdo(bp), **props)
        save(bp)
        huds[name] = bp

    game_state = make_blueprint("BP_GameState", core, unreal.HitmenGameState)
    save(game_state)
    player_state = make_blueprint("BP_PlayerState", core, unreal.HitmenPlayerState)
    save(player_state)
    spectator_pawn = make_blueprint("BP_SpectatorPawn", core, unreal.HitmenSpectatorPawn)
    save(spectator_pawn)
    build_game_instance(core)

    modes = {}
    for name, cls, hud, pawn in (("BP_GM_Menu", unreal.HitmenMenuGameMode, "BP_HUD_Menu", None), ("BP_GM_Lobby", unreal.HitmenLobbyGameMode, "BP_HUD_Lobby", None),
                                 ("BP_GM_Match", unreal.HitmenMatchGameMode, "BP_HUD_Match", player), ("BP_GM_Gym", unreal.HitmenGymGameMode, "BP_HUD_Match", player)):
        bp = make_blueprint(name, core, cls)
        props = dict(hud_class=gen_class(huds[hud]), player_controller_class=gen_class(pc),
                     game_state_class=gen_class(game_state), player_state_class=gen_class(player_state),
                     spectator_class=gen_class(spectator_pawn))
        if pawn:
            props["default_pawn_class"] = gen_class(pawn)
        set_props(cdo(bp), **props)
        save(bp)
        modes[name] = bp
    log("framework: game modes", len(modes))
    return modes, {"player": player, "npc": npc, "sniper": sniper, "vip": vip}


def build_first_person_abp(manny):
    """ABP_FirstPersonArms: hold pose (weapon in hand) -> DefaultSlot (fire, equip, punch) -> output."""
    abp_path = path("Characters", "Player", "Anims", "ABP_FirstPersonArms")
    abp = load(abp_path)
    if not abp:
        factory = unreal.AnimBlueprintFactory()
        factory.set_editor_property("target_skeleton", manny.get_editor_property("skeleton"))
        factory.set_editor_property("parent_class", unreal.HitmenFirstPersonAnimInstance)
        abp = create("ABP_FirstPersonArms", path("Characters", "Player", "Anims"), unreal.AnimBlueprint, factory)
    if not abp:
        return None
    preview = load(ANIMS + "/Pistol/MF_Pistol_Idle_ADS")
    unreal.HitmenAnimGraphTools.build_pose_slot_graph(abp, preview, "HoldPose", True)
    try:
        unreal.get_default_object(abp.generated_class()).set_editor_property("default_hold_pose", preview)
    except Exception as error:
        log("WARN fp default pose:", error)
    save(abp)
    return abp


def build_game_instance(core):
    """BP_GameInstance with the Options volume mix (engine Master / Music / SFX / Voice classes)."""
    mix = create("SCM_Volume", path("Audio", "Mixes"), unreal.SoundMix, unreal.SoundMixFactory())
    save(mix)
    for sub_folder in ("SFX", "Ambience", "Music"):
        ensure_folder(path("Audio", sub_folder))
    gi = make_blueprint("BP_GameInstance", core, unreal.HitmenGameInstance)
    set_props(cdo(gi), volume_sound_mix=mix,
              master_sound_class=load("/Engine/EngineSounds/Master"), music_sound_class=load("/Engine/EngineSounds/Music"),
              sfx_sound_class=load("/Engine/EngineSounds/SFX"), voice_sound_class=load("/Engine/EngineSounds/Voice"))
    save(gi)
    return gi


# ---------------------------------------------------------------------------------------------
# World fixtures (placeholder engine shapes)
# ---------------------------------------------------------------------------------------------

def build_world(mats, weapons):
    mi = mats["mi"]
    W = path("World", "Interactables")
    S = path("World", "Spawning")
    V = path("World", "Vehicles")
    bps = {}

    def fixture(name, cls, folder, meshes=None, props=None):
        bp = make_blueprint(name, folder, cls)
        d = cdo(bp)
        for comp_name, (shape, scale, material, location) in (meshes or {}).items():
            set_mesh(component(d, comp_name), SHAPES[shape], scale, material, location)
        if props:
            set_props(d, **props)
        save(bp)
        bps[name] = bp
        return bp

    # Doors fill the 110 x 230 openings the level builder cuts.
    fixture("BP_Door", unreal.HitmenDoor, W + "/Door", {"mesh": ("cube", (1.1, 0.1, 2.3), mi["MI_Wood"], (55, 0, 115))})
    fixture("BP_Door_Keycard1", unreal.HitmenDoor, W + "/Door", {"mesh": ("cube", (1.1, 0.1, 2.3), mi["MI_Blue"], (55, 0, 115))}, {"required_keycard": 1})
    window = fixture("BP_Window", unreal.HitmenWindow, W + "/Window", {"mesh": ("cube", (1, 1, 1), mi["MI_White"], None)})
    for leaf in ("left_shutter", "right_shutter"):
        set_mesh(component(cdo(window), leaf), SHAPES["cube"], None, mi["MI_Glass"])
    save(window)
    fixture("BP_VentHatch", unreal.HitmenVentHatch, W + "/Vent", {"mesh": ("cube", (1.2, 0.05, 1.2), mi["MI_Metal"], (0, 0, 60))})
    fixture("BP_KeycardPickup", unreal.HitmenKeycardPickup, W + "/Keycard", {"mesh": ("cube", (0.15, 0.1, 0.02), mi["MI_Blue"], (0, 0, 10))})
    fixture("BP_Ladder", unreal.HitmenLadder, W + "/Ladder", {"mesh": ("cube", (0.1, 0.6, 4.0), mi["MI_Metal"], (5, 0, 200))})
    fixture("BP_Gutter", unreal.HitmenLadder, W + "/Ladder", {"mesh": ("cylinder", (0.12, 0.12, 4.0), mi["MI_Metal"], (5, 0, 200))})
    fixture("BP_Zipline", unreal.HitmenZipline, W + "/Zipline", {"mesh": ("cylinder", (0.05, 0.05, 1.0), mi["MI_Dark"], None)})
    fixture("BP_JumpPad", unreal.HitmenJumpPad, W + "/JumpPad", {"mesh": ("cylinder", (1.6, 1.6, 0.1), mi["MI_Yellow"], (0, 0, 5))})
    fixture("BP_Teleporter", unreal.HitmenTeleporter, W + "/Teleporter", {"mesh": ("cylinder", (1.4, 1.4, 0.05), mi["MI_Blue"], (0, 0, 2))})
    fixture("BP_Locker", unreal.HitmenLocker, W + "/Locker", {"mesh": ("cube", (1, 1, 1), mi["MI_Metal"], None)})
    fixture("BP_Container", unreal.HitmenContainer, W + "/Container", {"mesh": ("cube", (1, 1, 1), mi["MI_Wood"], None)},
            {"contents_mode": unreal.HitmenContainerContents.FIXED})
    fixture("BP_Container_Random", unreal.HitmenContainer, W + "/Container", {"mesh": ("cube", (1, 1, 1), mi["MI_Wood"], None)},
            {"contents_mode": unreal.HitmenContainerContents.RANDOM})
    fixture("BP_TallGrass", unreal.HitmenTallGrass, W + "/Grass", {"mesh": ("cube", (6.0, 6.0, 1.1), mi["MI_Grass"], (0, 0, -5))})
    fixture("BP_Distraction_Radio", unreal.HitmenDistraction, W + "/Distraction", {"mesh": ("cube", (0.4, 0.2, 0.25), mi["MI_Red"], (0, 0, 12))})
    fixture("BP_SecurityCamera", unreal.HitmenSecurityCamera, W + "/Security",
            {"body": ("cube", (0.4, 0.2, 0.2), mi["MI_White"], None), "view_cone": ("cone", (1, 1, 1), mats["cone"], None)})
    decal_comp = component(cdo(bps["BP_SecurityCamera"]), "floor_decal")
    if decal_comp:
        decal_comp.set_decal_material(mats["decal"])
    save(bps["BP_SecurityCamera"])
    fixture("BP_SecurityMonitor", unreal.HitmenSecurityMonitor, W + "/Security", {"screen": ("cube", (0.8, 0.05, 0.5), mats["screen"], None)},
            {"no_signal_material": mats["no_signal"]})
    console = fixture("BP_ControlConsole", unreal.HitmenControlConsole, W + "/Security", {"mesh": ("cube", (0.7, 1.4, 0.9), mi["MI_Dark"], (0, 0, 45))},
                      {"no_signal_material": mats["no_signal"]})
    set_mesh(component(cdo(console), "screen"), SHAPES["cube"], None, mats["screen"])
    save(console)
    turret = fixture("BP_Turret", unreal.HitmenTurret, W + "/Security",
                     {"base": ("cylinder", (0.5, 0.5, 1.0), mi["MI_Metal"], (0, 0, 50)), "head": ("cube", (0.6, 0.45, 0.35), mi["MI_Dark"], (0, 0, 110))},
                     {"muzzle_fx": None, "impact_fx": None,
                      "fire_sound": load(SFX + "SFX_GunShot_Cue")})
    tcdo = cdo(turret)
    set_mesh(component(tcdo, "barrel"), SHAPES["cylinder"], None, mi["MI_Metal"])
    set_mesh(component(tcdo, "laser"), SHAPES["cylinder"], None, mats["laser"])
    save(turret)
    fixture("BP_HackPanel", unreal.HitmenHackPanel, W + "/Security", {"mesh": ("cube", (0.5, 0.1, 0.6), mi["MI_Yellow"], (0, 0, 130))})
    fixture("BP_CircuitBreaker", unreal.HitmenCircuitBreaker, W + "/Power", {"mesh": ("cube", (0.6, 0.2, 0.9), mi["MI_Metal"], (0, 0, 130))})
    fixture("BP_LightFixture", unreal.HitmenLightFixture, W + "/Power", {"mesh": ("sphere", (0.3, 0.3, 0.3), mi["MI_White"], None)})
    fixture("BP_BlackoutVolume", unreal.HitmenBlackoutVolume, W + "/Power")
    fixture("BP_RoomVolume", unreal.HitmenRoomVolume, W + "/Vent")
    fixture("BP_PoisonableDrink", unreal.HitmenPoisonableDrink, W + "/Drink", {"mesh": ("cylinder", (0.08, 0.08, 0.15), mi["MI_Glass"], (0, 0, 8))})
    fixture("BP_ExplosiveBarrel", unreal.HitmenExplosiveBarrel, W + "/Barrel", {"mesh": ("cylinder", (0.6, 0.6, 1.0), mi["MI_Red"], (0, 0, 50))},
            {"explosion_effect": load("/Game/Vefects/Anime_Stylized_VFX/VFX/Particles/NS_Explosion_Fire_Ground"), "explosion_effect_scale": 1.6})
    fixture("BP_BreakableWall", unreal.HitmenBreakableWall, W + "/Breakable", {"mesh": ("cube", (3.0, 0.3, 3.0), mi["MI_Stone"], (0, 0, 150))})
    mounted = fixture("BP_MountedGun", unreal.HitmenMountedGun, V, {"mesh": ("cylinder", (0.12, 0.12, 1.4), mi["MI_Dark"], (70, 0, 0)),
                                                          "tripod": ("cone", (0.9, 0.9, 0.85), mi["MI_Metal"], (0, 0, -35)),
                                                          "shield": ("cube", (0.06, 1.1, 0.7), mi["MI_Metal"], (25, 0, 15))})
    # The barrel cylinder is modelled along Z: pitch it onto the gun's forward axis.
    mounted_mesh = component(cdo(mounted), "mesh")
    if mounted_mesh:
        mounted_mesh.set_editor_property("relative_rotation", unreal.Rotator(pitch=-90.0, yaw=0.0, roll=0.0))
    save(mounted)
    tank = fixture("BP_Tank", unreal.HitmenTank, V, {"mesh": ("cube", (6.0, 3.6, 1.6), mi["MI_Green"], None), "turret": ("cube", (2.6, 2.0, 0.8), mi["MI_Green"], None),
                                                    "barrel": ("cylinder", (0.32, 0.32, 3.4), mi["MI_Dark"], None)},
                   {"shell_class": gen_class(load(path("Weapons", "Projectiles", "BP_Projectile_TankShell"))), "shell_speed": 20000.0})
    fixture("BP_RetrievalPoint", unreal.HitmenRetrievalPoint, S, {"mesh": ("cylinder", (4.0, 4.0, 0.1), mi["MI_Yellow"], (0, 0, 5))})
    fixture("BP_LootSpawn_Weapon", unreal.HitmenLootSpawnPoint, S, None, {"category": unreal.HitmenLootCategory.WEAPON})
    fixture("BP_LootSpawn_Item", unreal.HitmenLootSpawnPoint, S, None, {"category": unreal.HitmenLootCategory.ANY_ITEM})
    fixture("BP_TeamStart", unreal.HitmenTeamStart, S)
    fixture("BP_PatrolRoute", unreal.HitmenPatrolRoute, path("AI"))
    fixture("BP_GuardGroup", unreal.HitmenGuardGroup, path("AI"))
    log("world fixtures:", len(bps))
    return bps


def run():
    actions, imc, spectator_imc = build_input()
    physical = build_physical_materials()
    mats = build_materials(physical)
    abp, anim_set = build_animation()
    weapons, projectiles = build_weapons(mats)
    move, manip = build_abilities(projectiles, mats)
    cosmetics = build_cosmetics(mats)
    rules = build_rules()
    build_sound_set()
    build_fonts()
    catalog = build_catalog(weapons, move, manip, cosmetics)
    maps = build_maps()
    if not EAL.does_asset_exist(path("AI", "BehaviorTrees", "BT_NPC")):
        unreal.HitmenEditorTools.build_npc_behavior_trees(path("AI", "BehaviorTrees"))
        EAL.save_directory(path("AI", "BehaviorTrees"), only_if_is_dirty=False)
    widgets = build_widgets()
    modes, characters = build_framework(actions, imc, spectator_imc, abp, anim_set, widgets, mats, weapons)
    world = build_world(mats, weapons)
    return {"weapons": weapons, "move": move, "manip": manip, "modes": modes, "characters": characters, "world": world,
            "rules": rules, "catalog": catalog, "maps": maps, "mats": mats}
