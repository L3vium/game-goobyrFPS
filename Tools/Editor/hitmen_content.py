"""Hitmen content builder: input, materials, data assets and Blueprint children of every
C++ gameplay class, with placeholder engine shapes the team replaces later."""
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
    MEL.connect_material_property(dparam, "A", unreal.MaterialProperty.MP_OPACITY)
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

    # Sightroom post-process: darkens the world, paints custom-depth stencils.
    pp = new_material("PP_Sightroom", path("Materials", "PostProcess"))
    set_props(pp, material_domain=unreal.MaterialDomain.MD_POST_PROCESS)
    scene = MEL.create_material_expression(pp, unreal.MaterialExpressionSceneTexture, -700, -100)
    set_props(scene, scene_texture_id=unreal.SceneTextureId.PPI_POST_PROCESS_INPUT0)
    stencil = MEL.create_material_expression(pp, unreal.MaterialExpressionSceneTexture, -700, 150)
    set_props(stencil, scene_texture_id=unreal.SceneTextureId.PPI_CUSTOM_STENCIL)
    custom = MEL.create_material_expression(pp, unreal.MaterialExpressionCustom, -300, 0)
    code = (
        "float s = round(Stencil.r);\n"
        "float3 c = Scene.rgb * 0.35;\n"
        "if (s > 0.5 && s < 1.5) return lerp(c, float3(1,1,1), 0.85);\n"
        "if (s > 1.5 && s < 2.5) return lerp(c, float3(1,1,1), 0.55);\n"
        "if (s > 2.5 && s < 3.5) return float3(1.0, 0.05, 0.05);\n"
        "if (s > 3.5 && s < 4.5) return float3(1.0, 0.8, 0.1);\n"
        "return c;"
    )
    inputs = []
    for input_name in ("Scene", "Stencil"):
        ci = unreal.CustomInput()
        ci.set_editor_property("input_name", input_name)
        inputs.append(ci)
    set_props(custom, code=code, output_type=unreal.CustomMaterialOutputType.CMOT_FLOAT3, description="Sightroom", inputs=inputs)
    MEL.connect_material_expressions(scene, "Color", custom, "Scene")
    MEL.connect_material_expressions(stencil, "Color", custom, "Stencil")
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

    log("materials:", len(instances), "instances")
    return {"color": color, "cone": cone, "decal": decal, "screen": screen, "pp": pp, "mi": instances,
            "pp_pill": pill, "pp_blind": blind, "smoke": smoke, "no_signal": no_signal}


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

def projectile_bp(name, parent, mesh="sphere", scale=(0.1, 0.1, 0.1), material=None, **props):
    bp = make_blueprint(name, path("Weapons", "Projectiles"), parent)
    d = cdo(bp)
    set_mesh(component(d, "mesh"), SHAPES[mesh], scale, material)
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

    arrow = projectile_bp("BP_Projectile_Arrow", unreal.HitmenProjectile, "cylinder", (0.03, 0.03, 0.6), mi["MI_Wood"],
                          stick_on_impact=True, impact_noise_radius=300.0, movement={"projectile_gravity_scale": 0.6})
    rocket = projectile_bp("BP_Projectile_Rocket", unreal.HitmenProjectile, "cylinder", (0.12, 0.12, 0.5), mi["MI_Dark"],
                           explode_on_impact=True, explosion_radius=500.0, explosion_damage=140.0, movement={"projectile_gravity_scale": 0.0})
    grenade = projectile_bp("BP_Projectile_Grenade", unreal.HitmenProjectile, "sphere", (0.15, 0.15, 0.15), mi["MI_Green"],
                            fuse_time=2.0, explosion_radius=450.0, explosion_damage=120.0,
                            movement={"should_bounce": True, "bounciness": 0.4})
    blackhole = projectile_bp("BP_Projectile_BlackHole", unreal.HitmenProjectile_BlackHole, "sphere", (0.3, 0.3, 0.3), mi["MI_Dark"],
                              movement={"projectile_gravity_scale": 0.0})
    stone = projectile_bp("BP_Projectile_Stone", unreal.HitmenProjectile, "sphere", (0.08, 0.08, 0.08), mi["MI_Stone"],
                          impact_noise_radius=1600.0, impact_noise_type=unreal.HitmenNoiseType.DISTRACTION)
    dart = projectile_bp("BP_Projectile_SleepDart", unreal.HitmenProjectile, "cylinder", (0.02, 0.02, 0.2), mi["MI_Blue"],
                         status_on_hit=unreal.HitmenStatusEffect.ASLEEP, status_duration=25.0, movement={"projectile_gravity_scale": 0.0})
    flash = projectile_bp("BP_Projectile_Flashbang", unreal.HitmenProjectile_Flashbang, "cylinder", (0.08, 0.08, 0.15), mi["MI_White"])
    smoke = projectile_bp("BP_Projectile_Smoke", unreal.HitmenProjectile_Smoke, "cylinder", (0.08, 0.08, 0.15), mi["MI_Metal"])
    c4 = projectile_bp("BP_Projectile_C4", unreal.HitmenC4Charge, "cube", (0.15, 0.1, 0.05), mi["MI_Red"])
    shell = projectile_bp("BP_Projectile_TankShell", unreal.HitmenProjectile, "sphere", (0.2, 0.2, 0.2), mi["MI_Dark"],
                          explode_on_impact=True, explosion_radius=650.0, explosion_damage=200.0, movement={"projectile_gravity_scale": 0.2})

    pistol_mesh = load("/Game/Weapons/Pistol/Meshes/SM_Pistol")
    pistol_skel = load("/Game/Weapons/Pistol/Meshes/SKM_Pistol")
    rifle_mesh = load("/Game/Weapons/Rifle/Meshes/SM_Rifle")
    rifle_skel = load("/Game/Weapons/Rifle/Meshes/SKM_Rifle")
    gl_mesh = load("/Game/Weapons/GrenadeLauncher/Meshes/SM_GrenadeLauncher")
    gl_skel = load("/Game/Weapons/GrenadeLauncher/Meshes/SKM_GrenadeLauncher")

    A = unreal.HitmenFireMode
    weapons = {
        # name: (display, class bp, props)
        "Pistol": ("Silenced pistol", hitscan, dict(fire_mode=A.SINGLE, rounds_per_minute=260.0, damage=34.0, headshot_multiplier=3.0,
                   silenced=True, noise_radius=0.0, start_ammo=24, max_ammo=72, ammo_per_pickup=12, hip_spread=1.2, aim_spread=0.25,
                   static_mesh=pistol_mesh, skeletal_mesh=pistol_skel)),
        "SMG": ("SMG", hitscan, dict(fire_mode=A.AUTOMATIC, rounds_per_minute=900.0, damage=13.0, start_ammo=90, max_ammo=240,
                ammo_per_pickup=45, hip_spread=3.5, aim_spread=1.5, noise_radius=2600.0, static_mesh=rifle_mesh, skeletal_mesh=rifle_skel)),
        "AssaultRifle": ("Assault rifle", hitscan, dict(fire_mode=A.AUTOMATIC, rounds_per_minute=620.0, damage=22.0, start_ammo=90,
                         max_ammo=210, ammo_per_pickup=30, hip_spread=2.5, aim_spread=0.6, noise_radius=3200.0, static_mesh=rifle_mesh, skeletal_mesh=rifle_skel)),
        "BurstRifle": ("Burst rifle", hitscan, dict(fire_mode=A.BURST, burst_count=3, rounds_per_minute=780.0, damage=24.0, start_ammo=72,
                       max_ammo=180, ammo_per_pickup=24, hip_spread=2.0, aim_spread=0.4, noise_radius=3200.0, static_mesh=rifle_mesh, skeletal_mesh=rifle_skel)),
        "Shotgun": ("Shotgun", shotgun, dict(fire_mode=A.SINGLE, rounds_per_minute=70.0, damage=12.0, pellet_count=9, range=2500.0,
                    start_ammo=16, max_ammo=40, ammo_per_pickup=6, hip_spread=1.0, aim_spread=0.6, noise_radius=3500.0, static_mesh=rifle_mesh, skeletal_mesh=rifle_skel)),
        "Minigun": ("Minigun", hitscan, dict(fire_mode=A.AUTOMATIC, rounds_per_minute=1500.0, damage=9.0, spin_up_time=0.8, start_ammo=300,
                    max_ammo=600, ammo_per_pickup=150, hip_spread=4.0, aim_spread=2.5, noise_radius=4500.0, static_mesh=rifle_mesh, skeletal_mesh=rifle_skel)),
        "Sniper": ("Sniper rifle", hitscan, dict(fire_mode=A.SINGLE, rounds_per_minute=40.0, damage=120.0, headshot_multiplier=3.0,
                   range=30000.0, start_ammo=10, max_ammo=30, ammo_per_pickup=5, hip_spread=4.0, aim_spread=0.0, aim_fov=25.0,
                   recoil_pitch=3.0, noise_radius=6000.0, static_mesh=rifle_mesh, skeletal_mesh=rifle_skel)),
        "Bow": ("Bow", projectile, dict(fire_mode=A.SINGLE, rounds_per_minute=60.0, damage=90.0, charge_to_fire=True, full_charge_time=1.0,
                projectile_class=gen_class(arrow), projectile_speed=5000.0, silenced=True, noise_radius=0.0, start_ammo=12, max_ammo=30, ammo_per_pickup=6)),
        "RocketLauncher": ("Rocket launcher", projectile, dict(fire_mode=A.SINGLE, rounds_per_minute=40.0, projectile_class=gen_class(rocket),
                           projectile_speed=3000.0, start_ammo=4, max_ammo=8, ammo_per_pickup=2, noise_radius=5000.0, static_mesh=gl_mesh, skeletal_mesh=gl_skel)),
        "GrenadeLauncher": ("Grenade launcher", projectile, dict(fire_mode=A.SINGLE, rounds_per_minute=70.0, projectile_class=gen_class(grenade),
                            projectile_speed=2200.0, start_ammo=6, max_ammo=12, ammo_per_pickup=3, noise_radius=3000.0, static_mesh=gl_mesh, skeletal_mesh=gl_skel)),
        "BlackHole": ("Black hole gun", projectile, dict(fire_mode=A.SINGLE, rounds_per_minute=20.0, projectile_class=gen_class(blackhole),
                      projectile_speed=1800.0, start_ammo=2, max_ammo=4, ammo_per_pickup=1, noise_radius=1500.0, static_mesh=gl_mesh, skeletal_mesh=gl_skel)),
        "Sword": ("Sword", melee, dict(fire_mode=A.SINGLE, rounds_per_minute=90.0, damage=75.0, uses_ammo=False, melee_range=190.0,
                  melee_radius=45.0, silenced=True, noise_radius=0.0)),
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
    set_props(cdo(smoke_cloud), puff_material=mats["smoke"], puff_mesh=load(SHAPES["sphere"]))
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

    def preset(weapons, items, sight, suspicion, damage, aim):
        p = unreal.HitmenDifficultyPreset()
        set_props(p, weapon_spawn_count=weapons, item_spawn_count=items, npc_sight_range_multiplier=sight,
                  npc_suspicion_rate_multiplier=suspicion, npc_damage_multiplier=damage, npc_aim_error_degrees=aim)
        return p

    set_props(rules, difficulty_presets={D.EASY: preset(10, 10, 0.8, 0.7, 0.6, 6.0),
                                         D.NORMAL: preset(6, 6, 1.0, 1.0, 1.0, 3.0),
                                         D.HARD: preset(4, 4, 1.2, 1.4, 1.3, 1.5)})
    save(rules)
    return rules


def build_catalog(weapons, move, manip, cosmetics):
    catalog = make_data("DA_Catalog", path("Data"), unreal.HitmenCatalog)
    loadout = [weapons[n] for n in ("Pistol", "SMG", "AssaultRifle", "BurstRifle", "Shotgun", "Sniper", "Bow", "Sword")]
    set_props(catalog, weapons=list(weapons.values()), loot_weapons=[w for k, w in weapons.items() if k != "Pistol"],
              move_abilities=move, manipulation_abilities=manip, cosmetics=cosmetics)
    save(catalog)
    return catalog


def build_maps():
    maps = []
    for name, display, carousel, half in (("LDTest", "San Lazaro (LD test)", True, 9000.0), ("Empty_01", "Empty 01", True, 10000.0),
                                          ("Empty_02", "Empty 02", True, 10000.0), ("Gym", "Debug gym", False, 12000.0)):
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
        MEL.set_material_instance_vector_parameter_value(ghost_mi, "Color", unreal.LinearColor(0.85, 0.9, 1.0, 0.35))
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
    fp_abp = load("/Game/FirstPerson/Anims/ABP_FP_Copy")

    player = make_blueprint("BP_PlayerCharacter", path("Characters", "Player"), unreal.HitmenPlayerCharacter)
    pcdo = cdo(player)
    body = component(pcdo, "mesh")
    if body:
        set_props(body, skeletal_mesh_asset=manny, anim_class=gen_class(abp))
    fp = component(pcdo, "first_person_mesh")
    if fp:
        set_props(fp, skeletal_mesh_asset=manny, anim_class=gen_class(fp_abp) if fp_abp else gen_class(abp))
    set_props(pcdo, anim_set=anim_set)
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
    set_props(ncdo, anim_set=anim_set, ai_controller_class=gen_class(ai), default_weapon=weapons["Pistol"])
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
    civilian = make_blueprint("BP_NPC_Civilian", path("Characters", "NPC"), gen_class(npc))
    set_props(cdo(civilian), default_weapon=None)
    save(civilian)

    huds = {}
    hud_folder = path("UI", "HUD")
    for name, props in (("BP_HUD_Menu", dict(primary_widget_class=gen_class(widgets["WBP_TitleScreen"]), primary_is_menu=True, options_class=gen_class(widgets["WBP_Options"]))),
                        ("BP_HUD_Lobby", dict(primary_widget_class=gen_class(widgets["WBP_Lobby"]), primary_is_menu=True, options_class=gen_class(widgets["WBP_Options"]))),
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
    return modes, {"player": player, "npc": npc, "sniper": sniper, "vip": vip, "civilian": civilian}


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

    fixture("BP_Door", unreal.HitmenDoor, W + "/Door", {"mesh": ("cube", (1.0, 0.1, 2.1), mi["MI_Wood"], (50, 0, 105))})
    fixture("BP_Door_Keycard1", unreal.HitmenDoor, W + "/Door", {"mesh": ("cube", (1.0, 0.1, 2.1), mi["MI_Blue"], (50, 0, 105))}, {"required_keycard": 1})
    fixture("BP_Window", unreal.HitmenWindow, W + "/Window", {"mesh": ("cube", (1.2, 0.04, 1.2), mi["MI_Glass"], (0, 0, 160))}, {"slide_distance": 110.0})
    fixture("BP_VentHatch", unreal.HitmenVentHatch, W + "/Vent", {"mesh": ("cube", (1.2, 0.05, 1.2), mi["MI_Metal"], (0, 0, 60))})
    fixture("BP_KeycardPickup", unreal.HitmenKeycardPickup, W + "/Keycard", {"mesh": ("cube", (0.15, 0.1, 0.02), mi["MI_Blue"], (0, 0, 10))})
    fixture("BP_Ladder", unreal.HitmenLadder, W + "/Ladder", {"mesh": ("cube", (0.1, 0.6, 4.0), mi["MI_Metal"], (5, 0, 200))})
    fixture("BP_Gutter", unreal.HitmenLadder, W + "/Ladder", {"mesh": ("cylinder", (0.12, 0.12, 4.0), mi["MI_Metal"], (5, 0, 200))})
    fixture("BP_Zipline", unreal.HitmenZipline, W + "/Zipline", {"mesh": ("cylinder", (0.05, 0.05, 1.0), mi["MI_Dark"], None)})
    fixture("BP_JumpPad", unreal.HitmenJumpPad, W + "/JumpPad", {"mesh": ("cylinder", (1.6, 1.6, 0.1), mi["MI_Yellow"], (0, 0, 5))})
    fixture("BP_Teleporter", unreal.HitmenTeleporter, W + "/Teleporter", {"mesh": ("cylinder", (1.4, 1.4, 0.05), mi["MI_Blue"], (0, 0, 2))})
    fixture("BP_Locker", unreal.HitmenLocker, W + "/Locker", {"mesh": ("cube", (0.7, 0.6, 2.0), mi["MI_Metal"], (0, 0, 100))})
    fixture("BP_TallGrass", unreal.HitmenTallGrass, W + "/Grass", {"mesh": ("cube", (6.0, 6.0, 1.1), mi["MI_Grass"], (0, 0, -5))})
    fixture("BP_Distraction_Radio", unreal.HitmenDistraction, W + "/Distraction", {"mesh": ("cube", (0.4, 0.2, 0.25), mi["MI_Red"], (0, 0, 12))})
    fixture("BP_HackableDistraction_TV", unreal.HitmenHackableDistraction, W + "/Distraction", {"mesh": ("cube", (1.2, 0.1, 0.7), mi["MI_Dark"], (0, 0, 120))})
    fixture("BP_SecurityCamera", unreal.HitmenSecurityCamera, W + "/Security",
            {"body": ("cube", (0.4, 0.2, 0.2), mi["MI_White"], None), "view_cone": ("cone", (1, 1, 1), mats["cone"], None)})
    decal_comp = component(cdo(bps["BP_SecurityCamera"]), "floor_decal")
    if decal_comp:
        decal_comp.set_decal_material(mats["decal"])
    save(bps["BP_SecurityCamera"])
    fixture("BP_SecurityMonitor", unreal.HitmenSecurityMonitor, W + "/Security", {"screen": ("cube", (0.8, 0.05, 0.5), mats["screen"], None)},
            {"no_signal_material": mats["no_signal"]})
    fixture("BP_ControlConsole", unreal.HitmenControlConsole, W + "/Security", {"mesh": ("cube", (1.2, 0.6, 1.0), mi["MI_Dark"], (0, 0, 50))})
    fixture("BP_Turret", unreal.HitmenTurret, W + "/Security",
            {"base": ("cylinder", (0.5, 0.5, 1.0), mi["MI_Metal"], (0, 0, 50)), "head": ("cube", (0.8, 0.3, 0.3), mi["MI_Dark"], (0, 0, 110))})
    fixture("BP_HackPanel", unreal.HitmenHackPanel, W + "/Security", {"mesh": ("cube", (0.5, 0.1, 0.6), mi["MI_Yellow"], (0, 0, 130))})
    fixture("BP_CircuitBreaker", unreal.HitmenCircuitBreaker, W + "/Power", {"mesh": ("cube", (0.6, 0.2, 0.9), mi["MI_Metal"], (0, 0, 130))})
    fixture("BP_LightFixture", unreal.HitmenLightFixture, W + "/Power", {"mesh": ("sphere", (0.3, 0.3, 0.3), mi["MI_White"], None)})
    fixture("BP_BlackoutVolume", unreal.HitmenBlackoutVolume, W + "/Power")
    fixture("BP_RoomVolume", unreal.HitmenRoomVolume, W + "/Vent")
    fixture("BP_PoisonableDrink", unreal.HitmenPoisonableDrink, W + "/Drink", {"mesh": ("cylinder", (0.08, 0.08, 0.15), mi["MI_Glass"], (0, 0, 8))})
    fixture("BP_ExplosiveBarrel", unreal.HitmenExplosiveBarrel, W + "/Barrel", {"mesh": ("cylinder", (0.6, 0.6, 1.0), mi["MI_Red"], (0, 0, 50))})
    fixture("BP_BreakableWall", unreal.HitmenBreakableWall, W + "/Breakable", {"mesh": ("cube", (3.0, 0.3, 3.0), mi["MI_Stone"], (0, 0, 150))})
    fixture("BP_MountedGun", unreal.HitmenMountedGun, V, {"mesh": ("cylinder", (0.12, 0.12, 1.4), mi["MI_Dark"], (40, 0, 0))})
    fixture("BP_Tank", unreal.HitmenTank, V, {"mesh": ("cube", (6.0, 3.6, 1.6), mi["MI_Green"], None), "turret": ("cube", (2.6, 2.0, 0.8), mi["MI_Green"], None)},
            {"shell_class": gen_class(load(path("Weapons", "Projectiles", "BP_Projectile_TankShell")))})
    fixture("BP_RetrievalPoint", unreal.HitmenRetrievalPoint, S, {"mesh": ("cylinder", (4.0, 4.0, 0.1), mi["MI_Yellow"], (0, 0, 5))})
    fixture("BP_LootSpawn_Weapon", unreal.HitmenLootSpawnPoint, S, None, {"category": unreal.HitmenLootCategory.WEAPON})
    fixture("BP_LootSpawn_Item", unreal.HitmenLootSpawnPoint, S, None, {"category": unreal.HitmenLootCategory.ANY_ITEM})
    fixture("BP_TeamStart", unreal.HitmenTeamStart, S)
    fixture("BP_PatrolRoute", unreal.HitmenPatrolRoute, path("AI"))
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
