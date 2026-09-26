bl_info = {
    "name": "FairyAI Image to 3D",
    "author": "FairyAI",
    "version": (1, 0, 0),
    "blender": (3, 6, 0),
    "location": "View3D > Sidebar > FairyAI",
    "description": "Convert images to 3D models without screen reading. Supports depth-based displacement and color mapping.",
    "category": "Object",
}

import bpy
import bmesh
import numpy as np
import os
import sys
import json
from pathlib import Path


class FAIRYAI_OT_image_to_3d(bpy.types.Operator):
    """Convert an image to a 3D model using displacement mapping"""
    bl_idname = "fairyai.image_to_3d"
    bl_label = "Image to 3D"
    bl_options = {'REGISTER', 'UNDO'}

    filepath: bpy.props.StringProperty(subtype='FILE_PATH')
    depth_scale: bpy.props.FloatProperty(name="Depth Scale", default=0.3, min=0.01, max=5.0)
    subdivisions: bpy.props.IntProperty(name="Subdivisions", default=128, min=16, max=512)
    output_path: bpy.props.StringProperty(subtype='FILE_PATH', default="")
    output_format: bpy.props.EnumProperty(
        name="Format",
        items=[
            ('GLTF', "GLTF (.glb)", "Export as GLTF binary"),
            ('FBX', "FBX (.fbx)", "Export as FBX"),
            ('OBJ', "OBJ (.obj)", "Export as OBJ"),
        ],
        default='GLTF'
    )

    def execute(self, context):
        if not self.filepath or not os.path.exists(self.filepath):
            self.report({'ERROR'}, f"Image not found: {self.filepath}")
            return {'CANCELLED'}

        try:
            result = convert_image_to_3d(
                self.filepath,
                depth_scale=self.depth_scale,
                subdivisions=self.subdivisions,
                output_path=self.output_path,
                output_format=self.output_format
            )
            self.report({'INFO'}, f"3D model created: {result}")
            return {'FINISHED'}
        except Exception as e:
            self.report({'ERROR'}, f"Conversion failed: {str(e)}")
            return {'CANCELLED'}


class FAIRYAI_PT_panel(bpy.types.Panel):
    """FairyAI sidebar panel"""
    bl_label = "FairyAI"
    bl_idname = "FAIRYAI_PT_panel"
    bl_space_type = 'VIEW_3D'
    bl_region_type = 'UI'
    bl_category = 'FairyAI'

    def draw(self, context):
        layout = self.layout
        layout.label(text="Image to 3D Converter")
        layout.operator("fairyai.image_to_3d", text="Convert Image", icon='MESH_DATA')
        layout.separator()
        layout.label(text="Use with FairyAI assistant:")
        layout.label(text="  fairyai convert <image> -o model.glb")


def convert_image_to_3d(image_path, depth_scale=0.3, subdivisions=128,
                         output_path="", output_format='GLTF'):
    """Core function: convert image to 3D displacement mesh."""

    # Clear scene
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)

    # Load image
    img = bpy.data.images.load(image_path)
    width, height = img.size

    # Get pixel data as numpy array
    pixels = np.array(img.pixels[:], dtype=np.float32).reshape(height, width, 4)

    # Create luminance (grayscale) for height map
    luminance = 0.299 * pixels[:, :, 0] + 0.587 * pixels[:, :, 1] + 0.114 * pixels[:, :, 2]
    # Normalize to 0-1
    l_min, l_max = luminance.min(), luminance.max()
    if l_max - l_min > 0:
        luminance = (luminance - l_min) / (l_max - l_min)

    # Create a grid mesh
    bpy.ops.mesh.primitive_grid_add(x_subdivisions=subdivisions,
                                     y_subdivisions=subdivisions,
                                     size=2)
    obj = context_active = bpy.context.active_object
    obj.name = "FairyAI_3D_Model"

    # Apply displacement based on luminance
    mesh = obj.data
    for vert in mesh.vertices:
        x, y = vert.co.x, vert.co.y
        # Map from -1,1 to 0,1 for image coordinates
        img_x = int((x + 1) / 2 * (width - 1))
        img_y = int((y + 1) / 2 * (height - 1))
        img_x = max(0, min(width - 1, img_x))
        img_y = max(0, min(height - 1, img_y))

        # Z displacement from luminance
        vert.co.z = luminance[height - 1 - img_y, img_x] * depth_scale

    # Update mesh
    mesh.update()

    # Add smooth shading
    bpy.ops.object.shade_smooth()

    # Create material with image texture
    mat = bpy.data.materials.new(name="FairyAI_Material")
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links

    # Clear default nodes
    for node in nodes:
        nodes.remove(node)

    # Create texture node
    tex_image = nodes.new('ShaderNodeTexImage')
    tex_image.image = img
    tex_image.location = (-400, 0)

    # Principled BSDF
    bsdf = nodes.new('ShaderNodeBsdfPrincipled')
    bsdf.location = (0, 0)

    # Material output
    output = nodes.new('ShaderNodeOutputMaterial')
    output.location = (300, 0)

    links.new(tex_image.outputs['Color'], bsdf.inputs['Base Color'])
    links.new(bsdf.outputs['BSDF'], output.inputs['Surface'])

    obj.data.materials.append(mat)

    # Determine output path
    if not output_path:
        base = os.path.splitext(image_path)[0]
        ext = { 'GLTF': '.glb', 'FBX': '.fbx', 'OBJ': '.obj' }.get(output_format, '.glb')
        output_path = base + "_3d" + ext

    # Export
    if output_format == 'GLTF':
        bpy.ops.export_scene.gltf(filepath=output_path, export_format='GLB')
    elif output_format == 'FBX':
        bpy.ops.export_scene.fbx(filepath=output_path)
    elif output_format == 'OBJ':
        bpy.ops.wm.obj_export(filepath=output_path)

    print(f"[FairyAI] Exported 3D model to: {output_path}")
    return output_path


def headless_convert(image_path, output_path="", output_format='GLTF',
                      depth_scale=0.3, subdivisions=128):
    """Headless entry point for CLI usage."""
    return convert_image_to_3d(image_path, depth_scale, subdivisions,
                                output_path, output_format)


# CLI support: blender --background --python fairyai_image_to_3d.py -- --input img.png --output model.glb
if __name__ == "__main__":
    # Parse arguments after "--"
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

    input_path = ""
    output_path = ""
    fmt = "GLTF"
    dscale = 0.3
    subdiv = 128

    i = 0
    while i < len(argv):
        if argv[i] == "--input" and i + 1 < len(argv):
            input_path = argv[i + 1]; i += 2
        elif argv[i] == "--output" and i + 1 < len(argv):
            output_path = argv[i + 1]; i += 2
        elif argv[i] == "--format" and i + 1 < len(argv):
            fmt = argv[i + 1].upper(); i += 2
        elif argv[i] == "--depth" and i + 1 < len(argv):
            dscale = float(argv[i + 1]); i += 2
        elif argv[i] == "--subdiv" and i + 1 < len(argv):
            subdiv = int(argv[i + 1]); i += 2
        else:
            i += 1

    if input_path:
        result = headless_convert(input_path, output_path, fmt, dscale, subdiv)
        # Output JSON result for FairyAI to parse
        print(f"FAIRYAI_RESULT:{json.dumps({'output': result, 'status': 'ok'})}")
    else:
        print("FAIRYAI_RESULT:" + json.dumps({'status': 'error', 'message': 'No --input provided'}))


def register():
    bpy.utils.register_class(FAIRYAI_OT_image_to_3d)
    bpy.utils.register_class(FAIRYAI_PT_panel)


def unregister():
    bpy.utils.unregister_class(FAIRYAI_OT_image_to_3d)
    bpy.utils.unregister_class(FAIRYAI_PT_panel)
