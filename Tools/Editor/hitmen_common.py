"""Hitmen editor scripting helpers shared by the content and level builders.

Run the builders headless (editor closed):
    UnrealEditor-Cmd.exe Hitmen.uproject -run=pythonscript -script="Tools/Editor/hitmen_build_all.py"
Every builder is idempotent: existing assets are loaded and updated, never duplicated.
"""
import unreal

EAL = unreal.EditorAssetLibrary
AT = unreal.AssetToolsHelpers.get_asset_tools()
MEL = unreal.MaterialEditingLibrary
ROOT = "/Game/Hitmen"

LOG = []


def log(*parts):
    line = " ".join(str(p) for p in parts)
    LOG.append(line)
    unreal.log("[Hitmen] " + line)


def path(*parts):
    return "/".join([ROOT] + [p for p in parts if p])


def load(asset_path):
    return EAL.load_asset(asset_path) if EAL.does_asset_exist(asset_path) else None


def ensure_folder(folder):
    if not EAL.does_directory_exist(folder):
        EAL.make_directory(folder)


def create(name, folder, cls, factory):
    ensure_folder(folder)
    full = folder + "/" + name
    if EAL.does_asset_exist(full):
        return EAL.load_asset(full)
    asset = AT.create_asset(name, folder, cls, factory)
    if asset is None:
        log("FAILED to create", full)
    return asset


def make_blueprint(name, folder, parent_class):
    factory = unreal.BlueprintFactory()
    factory.set_editor_property("parent_class", parent_class)
    return create(name, folder, unreal.Blueprint, factory)


def make_widget(name, folder, parent_class):
    factory = unreal.WidgetBlueprintFactory()
    factory.set_editor_property("parent_class", parent_class)
    # No designer root: the C++ class builds its default layout.
    try:
        factory.set_editor_property("root_widget_class", None)
    except Exception:
        pass
    return create(name, folder, unreal.WidgetBlueprint, factory)


def make_data(name, folder, cls):
    factory = unreal.DataAssetFactory()
    factory.set_editor_property("data_asset_class", cls)
    return create(name, folder, cls, factory)


def gen_class(bp):
    return bp.generated_class() if bp else None


def cdo(bp):
    unreal.BlueprintEditorLibrary.compile_blueprint(bp)
    return unreal.get_default_object(bp.generated_class())


def set_props(obj, **props):
    for key, value in props.items():
        try:
            obj.set_editor_property(key, value)
        except Exception as error:  # keep building, report at the end
            log("WARN", obj.get_name(), key, error)


def component(obj, name):
    try:
        return obj.get_editor_property(name)
    except Exception as error:
        log("WARN no component", name, "on", obj.get_name(), error)
        return None


def set_mesh(comp, mesh_path, scale=None, material=None, location=None, rotation=None):
    if comp is None:
        return
    mesh = unreal.load_asset(mesh_path) if isinstance(mesh_path, str) else mesh_path
    if isinstance(comp, unreal.StaticMeshComponent) and mesh:
        comp.set_editor_property("static_mesh", mesh)
    if scale is not None:
        comp.set_editor_property("relative_scale3d", unreal.Vector(*scale))
    if location is not None:
        comp.set_editor_property("relative_location", unreal.Vector(*location))
    if rotation is not None:
        comp.set_editor_property("relative_rotation", unreal.Rotator(pitch=rotation[0], yaw=rotation[1], roll=rotation[2]))
    if material is not None:
        mat = unreal.load_asset(material) if isinstance(material, str) else material
        if mat:
            comp.set_material(0, mat)


def save(asset):
    if asset:
        EAL.save_loaded_asset(asset, only_if_is_dirty=False)


def key(name):
    k = unreal.Key()
    k.import_text(name)
    return k


SHAPES = {
    "cube": "/Engine/BasicShapes/Cube.Cube",
    "sphere": "/Engine/BasicShapes/Sphere.Sphere",
    "cylinder": "/Engine/BasicShapes/Cylinder.Cylinder",
    "cone": "/Engine/BasicShapes/Cone.Cone",
    "plane": "/Engine/BasicShapes/Plane.Plane",
}
