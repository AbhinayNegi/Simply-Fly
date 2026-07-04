import bpy
import math
from mathutils import Vector

# Cessna 172-inspired low-poly game asset.
# Blender units are meters. Aircraft faces -Y in Blender and +Z after Unity FBX export.

OUT_BLEND = r"C:\Users\gamer\Simply Fly\Assets\Models\Cessna_LowPoly\Cessna_172_LowPoly.blend"
OUT_FBX = r"C:\Users\gamer\Simply Fly\Assets\Models\Cessna_LowPoly\Cessna_172_LowPoly.fbx"

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
for datablocks in (bpy.data.meshes, bpy.data.curves, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
    pass

bpy.context.scene.unit_settings.system = "METRIC"
bpy.context.scene.unit_settings.scale_length = 1.0
bpy.context.scene.render.engine = "BLENDER_EEVEE"
bpy.context.scene.render.resolution_x = 1280
bpy.context.scene.render.resolution_y = 720
bpy.context.scene.render.resolution_percentage = 70
bpy.context.scene.world.color = (0.035, 0.05, 0.075)

def mat(name, color, metallic=0.0, roughness=0.55, alpha=1.0):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color, alpha)
    m.use_nodes = True
    bs = m.node_tree.nodes.get("Principled BSDF")
    bs.inputs["Base Color"].default_value = (*color, 1)
    bs.inputs["Metallic"].default_value = metallic
    bs.inputs["Roughness"].default_value = roughness
    if alpha < 1:
        bs.inputs["Alpha"].default_value = alpha
        bs.inputs["Transmission Weight"].default_value = 0.12
        m.surface_render_method = "DITHERED"
    return m

WHITE = mat("Paint_White", (0.82, 0.86, 0.88), 0.08, 0.32)
BLUE = mat("Paint_DeepBlue", (0.025, 0.12, 0.27), 0.22, 0.28)
RED = mat("Accent_Red", (0.68, 0.035, 0.025), 0.12, 0.3)
DARK = mat("Cockpit_Dark", (0.025, 0.03, 0.035), 0.0, 0.72)
PANEL = mat("Instrument_Panel", (0.09, 0.105, 0.11), 0.05, 0.5)
LEATHER = mat("Seat_Leather", (0.23, 0.105, 0.055), 0.0, 0.76)
GLASS = mat("Glass_BlueTint", (0.12, 0.28, 0.38), 0.05, 0.16, 0.32)
RUBBER = mat("Rubber", (0.018, 0.018, 0.02), 0.0, 0.9)
METAL = mat("Metal", (0.28, 0.31, 0.33), 0.75, 0.24)
GAUGE = mat("Gauge_Face", (0.018, 0.022, 0.025), 0.1, 0.25)
EMISSIVE = mat("Gauge_Marks", (0.62, 0.78, 0.66), 0, 0.3)

def finish(obj, name, material, parent=None, bevel=0.0):
    obj.name = name
    if material:
        obj.data.materials.append(material)
    if bevel > 0 and hasattr(obj.data, "polygons"):
        mod = obj.modifiers.new("Soft_Edges", "BEVEL")
        mod.width = bevel
        mod.segments = 2
    if hasattr(obj.data, "polygons"):
        for p in obj.data.polygons:
            p.use_smooth = False
    if parent:
        obj.parent = parent
    return obj

def cube(name, loc, scale, material, parent=None, bevel=0.04, rot=(0,0,0)):
    bpy.ops.mesh.primitive_cube_add(location=loc, rotation=rot)
    o = bpy.context.object
    o.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish(o, name, material, parent, bevel)

def uv(name, loc, scale, material, parent=None, segments=12, rings=6, rot=(0,0,0), bevel=0):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, location=loc, rotation=rot)
    o = bpy.context.object
    o.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish(o, name, material, parent, bevel)

def cyl(name, loc, radius, depth, material, parent=None, vertices=12, rot=(0,0,0), bevel=0.02):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=loc, rotation=rot)
    return finish(bpy.context.object, name, material, parent, bevel)

def mesh_obj(name, verts, faces, material, parent=None, bevel=0.02):
    me = bpy.data.meshes.new(name + "_Mesh")
    me.from_pydata(verts, [], faces)
    me.update()
    return finish(bpy.data.objects.new(name, me), name, material, parent, bevel)

def add_to_scene(o):
    if o.name not in bpy.context.collection.objects:
        bpy.context.collection.objects.link(o)
    return o

def tapered_box(name, y0, y1, z, halfw0, halfw1, halfh0, halfh1, material, parent):
    v = [
        (-halfw0,y0,z-halfh0),(halfw0,y0,z-halfh0),(halfw0,y0,z+halfh0),(-halfw0,y0,z+halfh0),
        (-halfw1,y1,z-halfh1),(halfw1,y1,z-halfh1),(halfw1,y1,z+halfh1),(-halfw1,y1,z+halfh1)
    ]
    f = [(0,1,2,3),(4,7,6,5),(0,4,5,1),(3,2,6,7),(0,3,7,4),(1,5,6,2)]
    return add_to_scene(mesh_obj(name,v,f,material,parent,0.055))

# Root and semantic anchors
root = bpy.data.objects.new("Cessna_172_Root", None)
bpy.context.collection.objects.link(root)
root["units"] = "meters"
root["dimensions_m"] = "span 11.0, length 8.3, height 2.65"
root["unity_forward"] = "+Z"

for name, loc in [
    ("CockpitCamera", (0, -1.15, 1.43)),
    ("CenterOfMass", (0, 0.05, 0.85)),
    ("PilotSeatAnchor", (-0.42, -0.55, 0.78)),
]:
    e = bpy.data.objects.new(name, None)
    e.empty_display_type = "ARROWS"
    e.empty_display_size = 0.25
    e.location = loc
    e.rotation_euler = (math.radians(90), 0, 0) if name == "CockpitCamera" else (0,0,0)
    e.parent = root
    bpy.context.collection.objects.link(e)

# Fuselage, nose and tail (nose at negative Y)
tapered_box("Cabin_Fuselage", -1.65, 1.55, 0.88, 0.72, 0.58, 0.72, 0.58, WHITE, root)
tapered_box("Engine_Cowling", -3.15, -1.65, 0.88, 0.34, 0.70, 0.43, 0.62, WHITE, root)
tapered_box("Tail_Cone", 1.55, 4.15, 0.92, 0.58, 0.10, 0.56, 0.17, WHITE, root)
uv("Nose_Cap", (0,-3.17,0.91), (0.34,0.18,0.40), WHITE, root, 12, 6)
cube("Blue_Belly_Stripe", (0,-0.05,0.43), (0.725,1.57,0.10), BLUE, root, .025)
cube("Red_Side_Stripe_L", (-0.725,-0.10,0.87), (.018,1.48,.075), RED, root, .01)
cube("Red_Side_Stripe_R", (0.725,-0.10,0.87), (.018,1.48,.075), RED, root, .01)

# High wings with tapered tips and dihedral
for side in (-1,1):
    sx = side
    verts = [
        (0.42,-0.75,1.78),(0.42,0.70,1.78),(5.50,0.44,2.02),(5.50,-0.42,2.02),
        (0.42,-0.75,1.91),(0.42,0.70,1.91),(5.50,0.44,2.10),(5.50,-0.42,2.10)
    ]
    verts = [(x*sx,y,z) for x,y,z in verts]
    wing = add_to_scene(mesh_obj(f"Wing_{'L' if side<0 else 'R'}", verts,
        [(0,3,2,1),(4,5,6,7),(0,4,7,3),(1,2,6,5),(0,1,5,4),(3,7,6,2)], WHITE, root,.035))
    cube(f"Wingtip_{side}", (5.47*sx,0.0,2.06), (.08,.42,.12), RED, root,.04)
    # Flaps and ailerons as dark seam panels
    cube(f"Aileron_{side}", (3.65*sx,0.48,2.025), (1.25,.018,.035), BLUE, root,.01, rot=(0,0,0))
    cube(f"Flap_{side}", (1.35*sx,0.61,1.91), (.72,.018,.04), BLUE, root,.01)
    # Wing strut
    length = math.sqrt((2.8)**2 + (1.20)**2)
    angle = math.atan2(2.8,1.20)
    cyl(f"Wing_Strut_{side}", (1.9*sx,0.15,1.25), .035, length, METAL, root, 8,
        rot=(0, angle*side, 0), bevel=.012)

# Tailplane and vertical stabilizer
for side in (-1,1):
    v=[(0,3.15,1.10),(0,4.08,1.08),(2.05*side,3.88,1.12),(2.05*side,3.30,1.13),
       (0,3.15,1.19),(0,4.08,1.17),(2.05*side,3.88,1.20),(2.05*side,3.30,1.21)]
    add_to_scene(mesh_obj(f"Horizontal_Stabilizer_{side}",v,
      [(0,3,2,1),(4,5,6,7),(0,4,7,3),(1,2,6,5),(0,1,5,4),(3,7,6,2)],WHITE,root,.025))
    cube(f"Elevator_Accent_{side}", (1.1*side,3.92,1.19), (.72,.03,.035), BLUE, root,.008)
v=[(-.08,3.15,1.12),(.08,3.15,1.12),(.07,3.96,1.15),(.05,3.72,3.02),(-.05,3.72,3.02),(-.07,3.96,1.15)]
add_to_scene(mesh_obj("Vertical_Stabilizer",v,[(0,1,2,3,4,5)],WHITE,root,.035))
cube("Rudder_Accent",(0,3.78,2.10),(.065,.035,.66),RED,root,.015,rot=(math.radians(-6),0,0))

# Cockpit glass: front, roof and side panes, plus sturdy frames
cube("Windshield", (0,-1.53,1.49), (.56,.025,.37), GLASS, root,.018,rot=(math.radians(-10),0,0))
cube("Rear_Window", (0,1.48,1.45), (.48,.025,.29), GLASS, root,.02,rot=(math.radians(8),0,0))
for side in (-1,1):
    cube(f"Door_Window_{side}", (.711*side,-.50,1.45), (.018,.64,.34), GLASS, root,.015)
    cube(f"Rear_Side_Window_{side}", (.63*side,.85,1.43), (.018,.47,.28), GLASS, root,.015)
    cube(f"A_Pillar_{side}", (.59*side,-1.50,1.48), (.055,.05,.43), DARK, root,.018,rot=(0,0,math.radians(7*side)))
    cube(f"B_Pillar_{side}", (.712*side,.16,1.46), (.045,.055,.40), DARK, root,.015)
    cube(f"Door_Sill_{side}", (.713*side,-.52,1.10), (.045,.69,.045), DARK, root,.015)
cube("Windshield_Top_Frame",(0,-1.52,1.84),(.63,.055,.045),DARK,root,.018)
cube("Windshield_Base_Frame",(0,-1.57,1.14),(.65,.06,.055),DARK,root,.018)
cube("Cabin_Roof",(0,.03,1.84),(.68,1.40,.075),WHITE,root,.035)

# Interior floor, dashboard, glare shield and center console
cube("Cockpit_Floor",(0,-.20,.50),(.60,1.22,.055),DARK,root,.03)
cube("Instrument_Panel",(0,-1.34,1.12),(.59,.105,.30),PANEL,root,.035,rot=(math.radians(-8),0,0))
cube("Glare_Shield",(0,-1.42,1.40),(.62,.22,.045),DARK,root,.025,rot=(math.radians(-5),0,0))
cube("Center_Console",(0,-.93,.76),(.13,.50,.18),PANEL,root,.025,rot=(math.radians(6),0,0))

# Gauges visible from pilot position
gauge_positions=[(-.37,1.23),(-.13,1.23),(.13,1.23),(.37,1.23),(-.28,1.00),(0,1.00),(.28,1.00)]
for i,(x,z) in enumerate(gauge_positions):
    cyl(f"Gauge_{i}",(x,-1.452,z),.095,.025,GAUGE,root,16,rot=(math.radians(90),0,0),bevel=.008)
    cyl(f"Gauge_Rim_{i}",(x,-1.47,z),.105,.018,METAL,root,16,rot=(math.radians(90),0,0),bevel=.006)
    cube(f"Gauge_Needle_{i}",(x,-1.482,z),(.008,.008,.065),EMISSIVE,root,.004,rot=(0,math.radians((i*31)%90-45),0))

# Avionics stack with colored screens/controls
for i,z in enumerate((1.28,1.13,.98)):
    cube(f"Avionics_{i}",(0,-1.47,z),(.105,.025,.055),DARK,root,.012)
    cube(f"Avionics_Display_{i}",(0,-1.498,z),(.07,.012,.028),EMISSIVE,root,.004)
for x in (-.18,.18):
    for z in (.82,.72):
        cyl("Panel_Knob",(x,-1.47,z),.025,.04,METAL,root,10,rot=(math.radians(90),0,0),bevel=.005)

# Yokes, throttle and mixture
for side in (-1,1):
    x=.34*side
    cyl(f"Yoke_Stem_{side}",(x,-1.05,1.03),.025,.34,METAL,root,10,rot=(math.radians(90),0,0))
    cyl(f"Yoke_Center_{side}",(x,-.86,1.03),.07,.06,DARK,root,10,rot=(math.radians(90),0,0))
    cube(f"Yoke_Bar_{side}",(x,-.89,1.03),(.18,.03,.025),DARK,root,.02)
    for dx in (-.17,.17):
        cube(f"Yoke_Grip_{side}_{dx}",(x+dx,-.89,.99),(.035,.035,.10),DARK,root,.025,rot=(0,0,math.radians(-12 if dx<0 else 12)))
cyl("Throttle",(0.10,-1.00,.86),.032,.18,DARK,root,12,rot=(math.radians(90),0,0))
cyl("Mixture",(-0.10,-1.00,.86),.032,.18,RED,root,12,rot=(math.radians(90),0,0))

# Four seats; front seats have visible backs/headrests from cockpit.
for row,y in enumerate((-.40,.82)):
    for side in (-1,1):
        x=.38*side
        cube(f"Seat_Base_{row}_{side}",(x,y,.68),(.25,.31,.10),LEATHER,root,.08)
        cube(f"Seat_Back_{row}_{side}",(x,y+.24,1.00),(.25,.10,.34),LEATHER,root,.08,rot=(math.radians(-8),0,0))
        cube(f"Headrest_{row}_{side}",(x,y+.28,1.36),(.15,.09,.13),LEATHER,root,.065)
        cyl(f"Seat_Rail_{row}_{side}",(x-.14,y,.54),.018,.55,METAL,root,8,rot=(math.radians(90),0,0),bevel=.006)
        cyl(f"Seat_Rail2_{row}_{side}",(x+.14,y,.54),.018,.55,METAL,root,8,rot=(math.radians(90),0,0),bevel=.006)

# Landing gear
for side in (-1,1):
    cyl(f"Main_Gear_Strut_{side}",(.92*side,.20,.13),.035,1.18,METAL,root,10,rot=(0,math.radians(55*side),0),bevel=.012)
    cyl(f"Main_Wheel_{side}",(1.28*side,.26,-.02),.25,.13,RUBBER,root,12,rot=(0,math.radians(90),0),bevel=.025)
    cyl(f"Main_Hub_{side}",(1.28*side,.26,-.02),.10,.145,METAL,root,12,rot=(0,math.radians(90),0),bevel=.01)
cyl("Nose_Gear_Strut",(0,-2.52,.22),.035,.68,METAL,root,10,bevel=.012)
cyl("Nose_Wheel",(0,-2.55,-.08),.19,.11,RUBBER,root,12,rot=(0,math.radians(90),0),bevel=.02)
cyl("Nose_Hub",(0,-2.55,-.08),.075,.125,METAL,root,12,rot=(0,math.radians(90),0),bevel=.008)

# Spinner and propeller, kept as separate named pieces for animation.
spinner = uv("Propeller_Spinner",(0,-3.39,.91),(.18,.28,.18),METAL,root,12,6)
prop_root=bpy.data.objects.new("PropellerPivot",None)
prop_root.location=(0,-3.55,.91)
prop_root.parent=root
bpy.context.collection.objects.link(prop_root)
cyl("Prop_Hub",(0,-3.55,.91),.12,.10,METAL,prop_root,12,rot=(math.radians(90),0,0))
for angle in (18,198):
    blade=cube("Propeller_Blade",(0,-3.62,.91),(.10,.035,1.22),DARK,prop_root,.055)
    blade.rotation_euler[1]=math.radians(angle)
    blade.data.materials.clear(); blade.data.materials.append(DARK)

# Door handles, belts and a few close-view details
for side in (-1,1):
    cube(f"Door_Handle_{side}",(.742*side,-.18,1.08),(.028,.10,.025),METAL,root,.012)
    cube(f"Seatbelt_{side}",(.38*side,-.38,.91),(.025,.25,.018),RED,root,.008,rot=(math.radians(-10),0,math.radians(22*side)))

# Ground shadow display plane is excluded from export.
ground=cube("Preview_Ground",(0,.2,-.31),(7,7,.04),mat("Ground",(0.055,.07,.075),0,.95),None,.02)
ground.hide_render=False
ground["exclude_from_export"]=True

# Preview camera: exterior beauty angle
bpy.ops.object.camera_add(location=(9,-11,6.6))
cam=bpy.context.object
cam.name="Preview_Camera"
cam.data.lens=52
cam.data.sensor_width=36
cam.rotation_euler=(math.radians(67),0,math.radians(39))
def look_at(obj, target):
    obj.rotation_euler=(Vector(target)-obj.location).to_track_quat("-Z","Y").to_euler()
look_at(cam,(0,.1,.9))
bpy.context.scene.camera=cam

bpy.ops.object.light_add(type="AREA", location=(-4,-5,9))
key=bpy.context.object; key.name="Key_Light"; key.data.energy=1400; key.data.shape="DISK"; key.data.size=6
look_at(key,(0,0,0.8))
bpy.ops.object.light_add(type="AREA", location=(6,2,4))
fill=bpy.context.object; fill.name="Fill_Light"; fill.data.energy=850; fill.data.size=5
look_at(fill,(0,0,1))
bpy.ops.object.light_add(type="AREA", location=(0,6,7))
rim=bpy.context.object; rim.name="Rim_Light"; rim.data.energy=1100; rim.data.size=4
look_at(rim,(0,1,1.3))

# Organize collections.
preview_col=bpy.data.collections.new("PREVIEW_ONLY")
bpy.context.scene.collection.children.link(preview_col)
for o in (ground,cam,key,fill,rim):
    for c in list(o.users_collection):
        c.objects.unlink(o)
    preview_col.objects.link(o)

asset_col=bpy.data.collections.new("CESSNA_ASSET")
bpy.context.scene.collection.children.link(asset_col)
for o in list(bpy.context.scene.collection.objects):
    if o not in (ground,cam,key,fill,rim):
        bpy.context.scene.collection.objects.unlink(o)
        asset_col.objects.link(o)

# Save blend and export FBX. Selection excludes preview helpers.
bpy.ops.wm.save_as_mainfile(filepath=OUT_BLEND)
bpy.ops.object.select_all(action="DESELECT")
for o in bpy.context.scene.objects:
    if o.type in {"MESH", "EMPTY"} and o != ground:
        o.select_set(True)
bpy.context.view_layer.objects.active=root
bpy.ops.export_scene.fbx(
    filepath=OUT_FBX,
    use_selection=True,
    apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_UNITS",
    axis_forward="-Z",
    axis_up="Y",
    object_types={"MESH","EMPTY"},
    use_mesh_modifiers=True,
    mesh_smooth_type="FACE",
    add_leaf_bones=False,
    bake_anim=False,
    path_mode="AUTO",
)

# Return to a clean exterior preview.
bpy.ops.object.select_all(action="DESELECT")
root.select_set(True)
bpy.context.view_layer.objects.active=root
bpy.ops.wm.save_as_mainfile(filepath=OUT_BLEND)
print("CESSNA_DONE", OUT_BLEND, OUT_FBX)
